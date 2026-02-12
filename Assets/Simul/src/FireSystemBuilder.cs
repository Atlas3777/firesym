using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(FireSystem))]
public class FireSystemBuilder : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Размер ячейки для оптимизации поиска соседей")]
    [SerializeField] private float spatialCellSize = 0.5f;
    
    [Tooltip("Максимальное расстояние, на котором огонь перекидывается между объектами")]
    [SerializeField] private float interGraphDistance = 0.3f; // Дистанция "прыжка" огня
    
    [SerializeField] private bool buildOnStart = true;
    private FireSystem _fireSystem;

    private void Awake()
    {
        _fireSystem = GetComponent<FireSystem>();
    }

    private void Start()
    {
        if (buildOnStart) BuildAndInitialize();
    }

    public void BuildAndInitialize()
    {
        FireGraph[] graphs = FindObjectsOfType<FireGraph>();
        if (graphs.Length == 0) return;

        // 1. Глобальные списки
        List<SimNode> allNodes = new List<SimNode>();
        // Временное хранилище связей для каждого узла (List<List<SimEdge>>)
        // Мы не можем сразу писать в плоский массив, так как будем добавлять новые связи
        List<List<SimEdge>> tempEdges = new List<List<SimEdge>>(); 
        
        // Для поиска соседей (Spatial Grid)
        Dictionary<int, List<int>> spatialGrid = new Dictionary<int, List<int>>();

        int globalIndexOffset = 0;

        // --- ПРОХОД 1: Сбор всех узлов и ВНУТРЕННИХ связей ---
        foreach (var graph in graphs)
        {
            var sourceNodes = graph.Nodes;
            if (sourceNodes == null) continue;

            for (int i = 0; i < sourceNodes.Count; i++)
            {
                FireNode fn = sourceNodes[i];
                int globalIndex = globalIndexOffset + i;

                // Внутри цикла сборки в Builder:
                var profile = MaterialLibrary.GetMaterialProfile(fn.materialType);

                SimNode simNode = new SimNode
                {
                    position = fn.position,
                    visited = false,
                };
                
                allNodes.Add(simNode);

                // B. Собираем "родные" связи (внутри одного объекта)
                List<SimEdge> nodeEdges = new List<SimEdge>();
                if (fn.neighbors != null)
                {
                    foreach (var edge in fn.neighbors) 
                    {
                        // Важно: пересчитываем локальный индекс соседа в глобальный
                        int targetGlobal = globalIndexOffset + edge.targetIndex; //
                        
                        nodeEdges.Add(new SimEdge
                        {
                            targetIndex = targetGlobal,
                            conductivity = 1f
                        });
                    }
                }
                tempEdges.Add(nodeEdges);

                // C. Регистрируем в сетке для поиска (для шага 2)
                var cell = SpatialHash.WorldToCell(fn.position, spatialCellSize); //
                int key = SpatialHash.CellKey(cell.x, cell.y, cell.z); //

                if (!spatialGrid.ContainsKey(key))
                    spatialGrid[key] = new List<int>();
                
                spatialGrid[key].Add(globalIndex);
            }

            globalIndexOffset += sourceNodes.Count;
        }

        // --- ПРОХОД 2: "Сшивание" графов (Inter-Graph Linking) ---
        // Ищем узлы, которые рядом, но еще не соединены
        float distSq = interGraphDistance * interGraphDistance;

        for (int i = 0; i < allNodes.Count; i++)
        {
            Vector3 pos = allNodes[i].position;
            var cell = SpatialHash.WorldToCell(pos, spatialCellSize);

            // Проверяем соседние ячейки (3x3x3)
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                int key = SpatialHash.CellKey(cell.x + x, cell.y + y, cell.z + z);
                if (!spatialGrid.TryGetValue(key, out var neighborsInCell)) continue;

                foreach (int neighborIdx in neighborsInCell)
                {
                    if (i == neighborIdx) continue; // Это мы сами

                    // Проверка дистанции
                    float d2 = (allNodes[neighborIdx].position - pos).sqrMagnitude;
                    if (d2 > distSq) continue;

                    // Проверяем, есть ли уже связь (чтобы не дублировать)
                    bool alreadyConnected = false;
                    foreach (var e in tempEdges[i])
                    {
                        if (e.targetIndex == neighborIdx)
                        {
                            alreadyConnected = true;
                            break;
                        }
                    }

                    if (!alreadyConnected)
                    {
                        // СОЗДАЕМ МОСТ МЕЖДУ ОБЪЕКТАМИ
                        // Чем ближе объекты, тем быстрее передается огонь
                        float dist = Mathf.Sqrt(d2);
                        float airConductivity = 0.5f / Mathf.Max(0.1f, dist); 

                        tempEdges[i].Add(new SimEdge
                        {
                            targetIndex = neighborIdx,
                            conductivity = airConductivity
                        });
                        
                        // Связь должна быть двусторонней? 
                        // В данном цикле мы дойдем до соседа позже (или уже прошли), 
                        // поэтому он добавит связь к нам сам.
                    }
                }
            }
        }

        // --- ПРОХОД 3: Упаковка в CSR (Compressed Sparse Row) ---
        // FireSystem требует плоских массивов для скорости
        List<SimEdge> flatEdges = new List<SimEdge>();
        int[] offsets = new int[allNodes.Count];
        int[] counts = new int[allNodes.Count];

        for (int i = 0; i < allNodes.Count; i++)
        {
            offsets[i] = flatEdges.Count;
            counts[i] = tempEdges[i].Count;
            flatEdges.AddRange(tempEdges[i]);
        }

        _fireSystem.SetData(
            allNodes, 
            flatEdges, 
            offsets, 
            counts, 
            spatialGrid, 
            spatialCellSize
        );

        Debug.Log($"[Builder] Merged Graphs. Total Nodes: {allNodes.Count}, Edges: {flatEdges.Count} (Internal + Bridges)");
    }
}