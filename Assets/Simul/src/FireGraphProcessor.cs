using UnityEngine;
using System.Collections.Generic;

public static class FireGraphProcessor
{
    public static void Bake(FireGraph graph, float targetSize, bool enableSimplification, float clusterRadius)
    {
        var mf = graph.GetComponent<MeshFilter>();
        var combustible = graph.GetComponent<Combustible>();

        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError($"[FireGraph] Object {graph.name} has no MeshFilter or Mesh!");
            return;
        }

        var srcVertices = mf.sharedMesh.vertices;
        var srcTriangles = mf.sharedMesh.triangles;
        var tr = graph.transform;

        var spatialMap = new Dictionary<Vector3Int, int>();
        var nodes = new List<FireNode>();

        for (int i = 0; i < srcTriangles.Length; i += 3)
        {
            var v1 = tr.TransformPoint(srcVertices[srcTriangles[i]]);
            var v2 = tr.TransformPoint(srcVertices[srcTriangles[i + 1]]);
            var v3 = tr.TransformPoint(srcVertices[srcTriangles[i + 2]]);

            ProcessTriangleRecursively(v1, v2, v3, targetSize, nodes, spatialMap, combustible);
        }

        var originalCount = nodes.Count;

        if (enableSimplification && nodes.Count > 0)
        {
            nodes = ClusterNodesByProximity(nodes, clusterRadius, combustible);
            nodes = RemoveLinearNodes(nodes, combustible);
            nodes = RemoveDanglingNodes(nodes);
        }

        graph.SetNodeList(nodes);

        Debug.Log($"[FireGraph] Baked {graph.name}: {originalCount} -> {nodes.Count} nodes " +
                  $"({Mathf.RoundToInt((1f - (float)nodes.Count / originalCount) * 100)}% reduction)");
    }

    private static void ProcessTriangleRecursively(Vector3 p1, Vector3 p2, Vector3 p3, float targetLen,
        List<FireNode> nodes, Dictionary<Vector3Int, int> spatialMap, Combustible combustible)
    {
        var d12 = Vector3.Distance(p1, p2);
        var d23 = Vector3.Distance(p2, p3);
        var d31 = Vector3.Distance(p3, p1);

        if (d12 <= targetLen && d23 <= targetLen && d31 <= targetLen)
        {
            var a = GetOrAddNode(p1, nodes, spatialMap, combustible, targetLen);
            var b = GetOrAddNode(p2, nodes, spatialMap, combustible, targetLen);
            var c = GetOrAddNode(p3, nodes, spatialMap, combustible, targetLen);

            var profile = MaterialLibrary.GetMaterialProfile(combustible.Material);
            Link(nodes, a, b, profile.burnRate);
            Link(nodes, b, c, profile.burnRate);
            Link(nodes, c, a, profile.burnRate);
            return;
        }

        if (d12 >= d23 && d12 >= d31)
        {
            Vector3 m = (p1 + p2) * 0.5f;
            ProcessTriangleRecursively(p1, m, p3, targetLen, nodes, spatialMap, combustible);
            ProcessTriangleRecursively(m, p2, p3, targetLen, nodes, spatialMap, combustible);
        }
        else if (d23 >= d12 && d23 >= d31)
        {
            Vector3 m = (p2 + p3) * 0.5f;
            ProcessTriangleRecursively(p1, p2, m, targetLen, nodes, spatialMap, combustible);
            ProcessTriangleRecursively(p1, m, p3, targetLen, nodes, spatialMap, combustible);
        }
        else
        {
            Vector3 m = (p3 + p1) * 0.5f;
            ProcessTriangleRecursively(p1, p2, m, targetLen, nodes, spatialMap, combustible);
            ProcessTriangleRecursively(m, p2, p3, targetLen, nodes, spatialMap, combustible);
        }
    }

    private static int GetOrAddNode(Vector3 pos, List<FireNode> nodes, Dictionary<Vector3Int, int> map,
        Combustible combustible, float grid)
    {
        var eps = grid * 0.1f;
        Vector3Int key = new(Mathf.RoundToInt(pos.x / eps), Mathf.RoundToInt(pos.y / eps),
            Mathf.RoundToInt(pos.z / eps));

        if (map.TryGetValue(key, out int idx)) return idx;

        nodes.Add(new FireNode
        {
            position = pos,
            materialType = combustible != null ? combustible.Material : FireMaterial.Wood,
            neighbors = new List<FireEdge>()
        });

        map[key] = nodes.Count - 1;
        return nodes.Count - 1;
    }

    private static void Link(List<FireNode> nodes, int a, int b, float burnRate)
    {
        if (a == b) return;
        var d = Vector3.Distance(nodes[a].position, nodes[b].position);
        // Исправлено: проводимость = скорость / дистанция
        var conductivity = burnRate / Mathf.Max(0.01f, d);

        // Добавляем связь А -> B
        if (!nodes[a].neighbors.Exists(e => e.targetIndex == b))
            nodes[a].neighbors.Add(new FireEdge { targetIndex = b, conductivity = conductivity });

        // Добавляем связь B -> A (Двусторонность)
        if (!nodes[b].neighbors.Exists(e => e.targetIndex == a))
            nodes[b].neighbors.Add(new FireEdge { targetIndex = a, conductivity = conductivity });
    }

    private static List<FireNode> RebuildGraphWithoutNodes(List<FireNode> nodes, bool[] toRemove, float burnSpeed)
    {
        var newNodes = new List<FireNode>();
        var oldToNewMap = new int[nodes.Count];

        // 1. Создаем новые (выжившие) узлы
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!toRemove[i])
            {
                oldToNewMap[i] = newNodes.Count;
                newNodes.Add(new FireNode
                {
                    position = nodes[i].position,
                    materialType = nodes[i].materialType,
                    neighbors = new List<FireEdge>()
                });
            }
            else oldToNewMap[i] = -1;
        }

        // 2. Пробрасываем связи
        for (int i = 0; i < nodes.Count; i++)
        {
            // Нас интересуют только те узлы, которые МЫ ОСТАВИЛИ
            if (toRemove[i]) continue;
            var newIdxA = oldToNewMap[i];

            foreach (var edge in nodes[i].neighbors)
            {
                var neighborIdx = edge.targetIndex;

                // Если сосед тоже выжил — просто соединяем
                if (!toRemove[neighborIdx])
                {
                    var newIdxB = oldToNewMap[neighborIdx];
                    Link(newNodes, newIdxA, newIdxB, burnSpeed);
                }
                else
                {
                    // СЛОЖНЫЙ СЛУЧАЙ: Сосед удален. 
                    // Нужно найти ближайшего "выжившего" за этим удаленным узлом.
                    var survivorIdx = FindNextSurvivor(neighborIdx, i, nodes, toRemove);
                    if (survivorIdx != -1)
                    {
                        var newIdxB = oldToNewMap[survivorIdx];
                        Link(newNodes, newIdxA, newIdxB, burnSpeed);
                    }
                }
            }
        }

        return newNodes;
    }

    // Рекурсивный поиск следующей неудаленной ноды
    private static int FindNextSurvivor(int currentIdx, int cameFromIdx, List<FireNode> allNodes, bool[] toRemove)
    {
        // Если эта нода выжила — возвращаем её
        if (!toRemove[currentIdx]) return currentIdx;

        // Если нет — идем глубже по её связям (BFS/DFS поиск внутри удаленных)
        foreach (var edge in allNodes[currentIdx].neighbors)
        {
            if (edge.targetIndex == cameFromIdx) continue; // Не идем назад

            var found = FindNextSurvivor(edge.targetIndex, currentIdx, allNodes, toRemove);
            if (found != -1) return found;
        }

        return -1;
    }

    private static List<FireNode> ClusterNodesByProximity(List<FireNode> nodes, float radius, Combustible combustible)
    {
        if (nodes.Count == 0 || radius <= 0f) return nodes;
        var profile = MaterialLibrary.GetMaterialProfile(combustible.Material);
        var merged = new bool[nodes.Count];
        var result = new List<FireNode>();

        for (var i = 0; i < nodes.Count; i++)
        {
            if (merged[i]) continue;
            var sum = nodes[i].position;
            var count = 1;
            merged[i] = true;

            for (var j = i + 1; j < nodes.Count; j++)
            {
                if (!merged[j] && Vector3.Distance(nodes[i].position, nodes[j].position) <= radius)
                {
                    sum += nodes[j].position;
                    merged[j] = true;
                    count++;
                }
            }

            result.Add(new FireNode
                { position = sum / count, materialType = nodes[i].materialType, neighbors = new List<FireEdge>() });
        }

        var edges = new Dictionary<(int, int), float>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var a = FindCluster(nodes[i].position, result, radius);
            if (a < 0) continue;
            foreach (var e in nodes[i].neighbors)
            {
                var b = FindCluster(nodes[e.targetIndex].position, result, radius);
                if (b < 0 || a == b) continue;
                var d = Vector3.Distance(result[a].position, result[b].position);
                var key = (Mathf.Min(a, b), Mathf.Max(a, b));
                if (!edges.ContainsKey(key) || (d / profile.burnRate) < edges[key]) edges[key] = d / profile.burnRate;
            }
        }

        foreach (var kv in edges)
        {
            result[kv.Key.Item1].neighbors.Add(new FireEdge { targetIndex = kv.Key.Item2, conductivity = kv.Value });
            result[kv.Key.Item2].neighbors.Add(new FireEdge { targetIndex = kv.Key.Item1, conductivity = kv.Value });
        }

        return result;
    }

    private static int FindCluster(Vector3 pos, List<FireNode> clusters, float radius)
    {
        for (int i = 0; i < clusters.Count; i++)
            if (Vector3.Distance(pos, clusters[i].position) <= radius * 1.5f)
                return i;
        return -1;
    }

    private static List<FireNode> RemoveLinearNodes(List<FireNode> nodes, Combustible combustible)
    {
        var toRemove = new bool[nodes.Count];
        var profile = MaterialLibrary.GetMaterialProfile(combustible.Material);
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].neighbors.Count == 2)
                toRemove[i] = true;
        return RebuildGraphWithoutNodes(nodes, toRemove, profile.burnRate);
    }

    private static List<FireNode> RemoveDanglingNodes(List<FireNode> nodes)
    {
        var toRemove = new bool[nodes.Count];
        var found = false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].neighbors.Count <= 1)
            {
                toRemove[i] = true;
                found = true;
            }
        }

        return found ? RebuildGraphWithoutNodes(nodes, toRemove, 1f) : nodes;
    }

    private static bool HasNeighbor(FireNode node, int idx)
    {
        foreach (var e in node.neighbors)
            if (e.targetIndex == idx)
                return true;
        return false;
    }
}