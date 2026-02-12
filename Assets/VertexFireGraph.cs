using UnityEngine;
using System.Collections.Generic;

public class VertexFireGraph : MonoBehaviour
{
    public Transform fireSource;

    [Header("Settings")]
    public int maxNeighbors = 6;

    [Header("High Density (Props, Furniture)")]
    public float cellSize = 0.3f;
    public LayerMask HighDensity;

    [Header("Low Density (Walls, Floor, Ceiling)")]
    public float envCellSize = 0.6f;
    public LayerMask LowDensity;

    [Header("Vertex Deduplication")]
    [SerializeField] private float vertexMergeDistance = 0.1f; // Минимальное расстояние между узлами

    [Header("Heat Simulation")]
    public float maxTemperature = 1000f;
    public float diffusionRate = 5f;
    public float coolingRate = 0.5f;
    public float sourceTemperature = 1000f;

    [Header("Visualization")]
    public bool drawConnections = true;
    public Gradient heatGradient;


    [System.Serializable]
    public struct FireNode
    {
        public Vector3 position;
        public List<int> neighbors; // Для рантайма лучше массив, но пока оставим List
        public float temperature;
        public bool infiniteSource;
    }

    [SerializeField] public List<FireNode> nodes = new();
    private Dictionary<Vector3Int, int> _spatialGrid; // Единая структура для дедупликации и поиска
    private float _gridHashStep; // Шаг хэширования = vertexMergeDistance

    private void Awake()
    {
        // Предзагрузка настроек градиента по умолчанию
        if (heatGradient == null || heatGradient.colorKeys.Length == 0)
        {
            heatGradient = new Gradient();
            heatGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.blue, 0.25f),
                    new GradientColorKey(Color.red, 0.7f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
        }
    }

    /// Добавить в класс VertexFireGraph
    public void ApplyHeatToMeshes()
    {
        if (nodes.Count == 0) return;

        var filters = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        foreach (var filter in filters)
        {
            if (!filter.gameObject.activeInHierarchy) continue;
            if (!filter.TryGetComponent<MeshRenderer>(out var renderer)) continue;

            // Проверка материала на совместимость
            bool hasFireMaterial = false;
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat?.shader?.name == "Custom/FireSurface")
                {
                    hasFireMaterial = true;
                    break;
                }
            }
            if (!hasFireMaterial) continue;

            // Создаём уникальную копию меша для модификации цветов
            Mesh mesh = filter.mesh; // .mesh создаёт копию при первом доступе
            Vector3[] verts = mesh.vertices;
            Color32[] colors = new Color32[verts.Length];

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 worldPos = filter.transform.TransformPoint(verts[i]);
                float temp = GetTemperatureAt(worldPos, radius: 0.3f);
                float t = Mathf.InverseLerp(0f, maxTemperature, temp);
                // R-канал = температура, остальные для совместимости
                colors[i] = new Color32((byte)(t * 255), (byte)(t * 128), 0, 255);
            }

            mesh.colors32 = colors;
        }
    }

    // В Update вызывать после симуляции
    private void Update()
    {
        SimulateHeat(Time.deltaTime);
        if (Application.isPlaying)
            ApplyHeatToMeshes();
    }
    // Внутрь класса VertexFireGraph2
    public float GetTemperatureAt(Vector3 worldPos, float radius = 0.25f)
    {
        if (nodes == null || nodes.Count == 0) return 0f;

        // Быстрый поиск ближайшего узла (для прототипа достаточно)
        float maxTemp = 0f;
        foreach (var node in nodes)
        {
            float dist = Vector3.Distance(node.position, worldPos);
            if (dist <= radius)
            {
                // Линейное затухание влияния с расстоянием
                float influence = 1f - (dist / radius);
                maxTemp = Mathf.Max(maxTemp, node.temperature * influence);
            }
        }
        return maxTemp;
    }

    // =========================
    // ======== BAKE ===========
    // =========================
    [ContextMenu("Bake From Meshes")]
    public void Bake()
    {
        nodes.Clear();
        _gridHashStep = vertexMergeDistance;
        _spatialGrid = new Dictionary<Vector3Int, int>();

        var filters = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);

        foreach (var filter in filters)
        {
            if (!filter.gameObject.activeInHierarchy) continue;

            int layer = filter.gameObject.layer;
            bool isBurnable = ((1 << layer) & HighDensity) != 0;
            bool isEnvironment = ((1 << layer) & LowDensity) != 0;
            if (!isBurnable && !isEnvironment) continue;

            Mesh mesh = filter.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0) continue;

            var tx = filter.transform;

            if (isBurnable)
            {
                // Для пропов/мебели: только вершины меша со сливанием близких точек
                foreach (var v in mesh.vertices)
                    AddNodeDeduplicated(tx.TransformPoint(v));
            }
            else if (isEnvironment)
            {
                // Для стен/пола: равномерная сетка по поверхности треугольников
                var verts = mesh.vertices;
                var tris = mesh.triangles;

                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = tx.TransformPoint(verts[tris[i]]);
                    Vector3 b = tx.TransformPoint(verts[tris[i + 1]]);
                    Vector3 c = tx.TransformPoint(verts[tris[i + 2]]);
                    SampleTriangleSurface(a, b, c, envCellSize);
                }
            }
        }

        ConnectNodesOptimized();
        Debug.Log($"Baked {nodes.Count} fire nodes (merged within {vertexMergeDistance}m).");
    }

    // Умное добавление узла с дедупликацией через пространственный хэш
    private void AddNodeDeduplicated(Vector3 pos)
    {
        Vector3Int key = Vector3Int.FloorToInt(pos / _gridHashStep);

        // Если узел в этой ячейке уже есть — сливаем позиции для точности
        if (_spatialGrid.TryGetValue(key, out int existingIndex))
        {
            var existing = nodes[existingIndex];
            existing.position = (existing.position + pos) * 0.5f;
            nodes[existingIndex] = existing;
            return;
        }

        // Новый узел
        _spatialGrid[key] = nodes.Count;
        nodes.Add(new FireNode
        {
            position = pos,
            neighbors = new List<int>(maxNeighbors),
            temperature = 0f,
            infiniteSource = false
        });
    }

    // Заполнение треугольника равномерной сеткой (только для окружения)
    private void SampleTriangleSurface(Vector3 a, Vector3 b, Vector3 c, float step)
    {
        // Быстрая проверка на вырожденный треугольник
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        if (Vector3.Cross(edge1, edge2).sqrMagnitude < 0.001f) return;

        Vector3 min = Vector3.Min(a, Vector3.Min(b, c));
        Vector3 max = Vector3.Max(a, Vector3.Max(b, c));

        // Выравнивание сетки к мировым координатам
        float startX = Mathf.Floor(min.x / step) * step;
        float startY = Mathf.Floor(min.y / step) * step;
        float startZ = Mathf.Floor(min.z / step) * step;

        Vector3 normal = Vector3.Cross(edge1, edge2).normalized;
        float maxDistToPlane = step * 0.3f;

        for (float x = startX; x <= max.x; x += step)
            for (float y = startY; y <= max.y; y += step)
                for (float z = startZ; z <= max.z; z += step)
                {
                    Vector3 p = new Vector3(x, y, z);
                    float distToPlane = Mathf.Abs(Vector3.Dot(p - a, normal));

                    // Пропускаем точки далеко от плоскости треугольника
                    if (distToPlane > maxDistToPlane) continue;

                    // Проекция на плоскость треугольника
                    Vector3 proj = p - Vector3.Dot(p - a, normal) * normal;
                    if (IsPointInTriangle(proj, a, b, c))
                        AddNodeDeduplicated(proj);
                }
    }

    // Оптимизированное соединение узлов через пространственный хэш
    private void ConnectNodesOptimized()
    {
        float searchRadius = Mathf.Max(cellSize, envCellSize) * 1.2f;
        float searchRadiusSqr = searchRadius * searchRadius;
        int searchRange = Mathf.CeilToInt(searchRadius / _gridHashStep);

        // Временный буфер для кандидатов (избегаем аллокаций в цикле)
        List<(int idx, float distSqr)> candidates = new List<(int, float)>(maxNeighbors * 2);

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            Vector3 pos = node.position;
            Vector3Int centerKey = Vector3Int.FloorToInt(pos / _gridHashStep);

            candidates.Clear();

            // Поиск только в локальной 3D-окрестности хэш-сетки
            for (int dx = -searchRange; dx <= searchRange; dx++)
                for (int dy = -searchRange; dy <= searchRange; dy++)
                    for (int dz = -searchRange; dz <= searchRange; dz++)
                    {
                        Vector3Int key = centerKey + new Vector3Int(dx, dy, dz);
                        if (_spatialGrid.TryGetValue(key, out int neighborIdx) && neighborIdx != i)
                        {
                            float dSqr = (nodes[neighborIdx].position - pos).sqrMagnitude;
                            if (dSqr <= searchRadiusSqr)
                                candidates.Add((neighborIdx, dSqr));
                        }
                    }

            // Сортируем по дальности и берём только N ближайших
            candidates.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));
            int count = Mathf.Min(candidates.Count, maxNeighbors);
            for (int j = 0; j < count; j++)
                nodes[i].neighbors.Add(candidates[j].idx);
        }
    }

    // Оптимизированная проверка точки в треугольнике (барицентрические координаты)
    private bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = c - a;
        Vector3 v1 = b - a;
        Vector3 v2 = p - a;

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float denom = dot00 * dot11 - dot01 * dot01;
        if (Mathf.Abs(denom) < 1e-6f) return false;

        float invDenom = 1f / denom;
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        // Небольшой люфт для численной стабильности
        return u >= -0.1f && v >= -0.1f && (u + v) <= 1.1f;
    }

    // =========================
    // ======= HEAT ============
    // =========================

    public void SetInfiniteHeatSource(Vector3 worldPos, float radius = 0.2f)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (Vector3.Distance(nodes[i].position, worldPos) <= radius)
            {
                var n = nodes[i];
                n.infiniteSource = true;
                n.temperature = sourceTemperature;
                nodes[i] = n;
            }
        }
    }

    private void SimulateHeat(float dt)
    {
        if (nodes.Count == 0) return;

        float[] nextTemps = new float[nodes.Count];

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node.infiniteSource)
            {
                nextTemps[i] = sourceTemperature;
                continue;
            }

            float t = node.temperature;

            // Диффузия от соседей
            foreach (int nIdx in node.neighbors)
            {
                float neighborTemp = nodes[nIdx].temperature;
                t += (neighborTemp - t) * diffusionRate * dt;
            }

            // Охлаждение
            t -= coolingRate * dt;
            nextTemps[i] = Mathf.Clamp(t, 0f, maxTemperature);
        }

        // Применение результатов
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            n.temperature = nextTemps[i];
            nodes[i] = n;
        }
    }

    // =========================
    // ===== VISUAL ============
    // =========================

    private void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            float t = Mathf.InverseLerp(0f, maxTemperature, node.temperature);
            Gizmos.color = heatGradient.Evaluate(t);

            Gizmos.DrawSphere(node.position, 0.025f);

            if (!drawConnections || node.neighbors == null) continue;

            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.15f);
            foreach (int n in node.neighbors)
            {
                if (n < nodes.Count) // Защита от ошибок при редактировании
                    Gizmos.DrawLine(node.position, nodes[n].position);
            }
        }
    }
}
