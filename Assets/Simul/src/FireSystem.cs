using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class FireSystem : MonoBehaviour
{
    public ParticleSystem fireParticleSystem;

    [Header("Simulation Settings")] public float coolingSpeed = 0.02f; // Пассивная потеря (излучение)
    public float convectionRate = 0.25f; // Какую часть энергии нода пытается отдать вверх за тик
    public float conductionRate = 0.05f; // Классическая теплопроводность (во все стороны)

    public float maxTemperature = 500f;
    public float nodeScale = 0.1f;

    private SimNode[] _nodes;
    private SimEdge[] _edges;
    private int[] _edgeOffsets;
    private int[] _edgeCounts;

    private float[] _energyDelta;

    private readonly List<int> _affectedIndices = new(256);
    private Dictionary<int, List<int>> _spatialGrid;
    private float _cellSize;

    private ParticleSystem.Particle[] _particles;
    private FireSystemBuilder _builder;

    private const float SIM_STEP = 0.1f;
    private float _timer;

    void Awake()
    {
        _builder = GetComponent<FireSystemBuilder>();

        if (fireParticleSystem == null)
            fireParticleSystem = GetComponentInChildren<ParticleSystem>();
    }

    private void Start()
    {
        _builder.BuildAndInitialize();
    }

    public void SetData(
        List<SimNode> newNodes, List<SimEdge> newEdges,
        int[] newOffsets, int[] newCounts,
        Dictionary<int, List<int>> spatialGrid, float cellSize)
    {
        _nodes = newNodes.ToArray();
        _edges = newEdges.ToArray();
        _edgeOffsets = newOffsets;
        _edgeCounts = newCounts;

        _particles = new ParticleSystem.Particle[_nodes.Length];

        for (var i = 0; i < _nodes.Length; i++)
        {
            _particles[i].position = _nodes[i].position;
            _particles[i].startSize = nodeScale;
            _particles[i].startColor = Color.gray;
            _particles[i].remainingLifetime = 10000f;
            _particles[i].startLifetime = 10000f;
        }

        fireParticleSystem.Clear();
        fireParticleSystem.SetParticles(_particles, _nodes.Length);

        _spatialGrid = spatialGrid;
        _cellSize = cellSize;

        _energyDelta = new float[_nodes.Length];
        Debug.Log($"<color=orange>FireSystem Running:</color> {_nodes.Length} particles. Convection Mode ON.");
    }

    void Update()
    {
        if (_nodes == null || _nodes.Length == 0) return;

        _timer += Time.deltaTime;
        if (_timer >= SIM_STEP)
        {
            StepSimulation();
            _timer -= SIM_STEP;
            if (fireParticleSystem)
            {
                UpdateParticles();
            }
        }
    }

    private void UpdateParticles()
    {
        var count = _nodes.Length;
        for (var i = 0; i < count; i++)
        {
            if (_nodes[i].fuel <= 0)
            {
                _particles[i].startColor = Color.gray; // Сгорел - серый
                _particles[i].startSize = nodeScale * 0.8f;
            }
            else
            {
                var t = Mathf.Clamp01(_nodes[i].temperature / maxTemperature);
                _particles[i].startColor = GetHeatmapColor(t);

                // Если горит или очень горячий - увеличиваем визуально
                float sizeMult = _nodes[i].burning ? 1.5f : 1.0f;
                _particles[i].startSize = nodeScale * (1f + t * 0.5f) * sizeMult;
            }

            _particles[i].remainingLifetime = 1000f;
        }

        fireParticleSystem.SetParticles(_particles, count);
    }

    private Color GetHeatmapColor(float t)
    {
        if (t < 0.2f) return Color.Lerp(new Color(0.1f, 0f, 0.2f), Color.blue, t / 0.2f);
        if (t < 0.4f) return Color.Lerp(Color.blue, Color.green, (t - 0.2f) / 0.2f);
        if (t < 0.6f) return Color.Lerp(Color.green, Color.yellow, (t - 0.4f) / 0.2f);
        if (t < 0.8f) return Color.Lerp(Color.yellow, new Color(1f, 0.3f, 0f), (t - 0.6f) / 0.2f);
        return Color.Lerp(new Color(1f, 0.3f, 0f), Color.white, (t - 0.8f) / 0.2f);
    }

    private void StepSimulation()
    {
        Array.Clear(_energyDelta, 0, _energyDelta.Length);

        // --- ФАЗА 1: Горение и расчет базовой температуры ---
        for (int i = 0; i < _nodes.Length; i++)
        {
            ref var node = ref _nodes[i];
            float capacity = Mathf.Max(0.1f, node.heatCapacity);
            node.temperature = node.energy / capacity;

            if (node.fuel > 0)
            {
                if (node.burning)
                {
                    node.fuel -= 1;
                    _energyDelta[i] += node.calorificValue; // Генерация тепла
                    if (node.fuel <= 0) node.burning = false;
                }
                else if (node.temperature >= node.ignitionTemp)
                {
                    node.burning = true;
                }
            }
        }

        // --- ФАЗА 2: Распределение энергии ---
        for (int i = 0; i < _nodes.Length; i++)
        {
            float temp = _nodes[i].temperature;
            float energy = _nodes[i].energy;
            bool isBurning = _nodes[i].burning;

            if (temp < 1.0f && !isBurning) continue;

            // 1. ПАССИВНОЕ ОХЛАЖДЕНИЕ (Окружающая среда)
            // Нода просто теряет часть тепла в пространство
            float cooling = temp * coolingSpeed;
            _energyDelta[i] -= cooling;

            int start = _edgeOffsets[i];
            int count = _edgeCounts[i];
            if (count == 0) continue;

            // 2. КОНВЕКЦИЯ (Только если горит!)
            // "Лифт" вверх, который активируется только при пламени
            float convectionPool = 0f;
            if (isBurning)
            {
                // Берем 50-60% от генерации тепла (calorificValue) и пускаем строго вверх
                convectionPool = _nodes[i].calorificValue * 0.6f;
                _energyDelta[i] -= convectionPool;
            }

            float totalUpWeight = 0f;
            // Сначала считаем веса для конвекции
            if (convectionPool > 0)
            {
                for (int j = 0; j < count; j++)
                {
                    float weight = _edges[start + j].hightConductivity;
                    if (weight > 1.1f) totalUpWeight += weight; // Берем только тех, кто выше
                }
            }

            // 3. ОБМЕН С СОСЕДЯМИ (Кондукция + Конвекция)
            for (int j = 0; j < count; j++)
            {
                var edge = _edges[start + j];
                int neighborIdx = edge.targetIndex;
                float neighborTemp = _nodes[neighborIdx].temperature;

                // А) Горизонтальная и вертикальная теплопроводность (всегда)
                // Это то, что позволяет огню ползти по полу
                if (temp > neighborTemp)
                {
                    // Стандартная формула теплопередачи
                    float diff = temp - neighborTemp;
                    float conduction = diff * conductionRate * edge.materialConductivity;

                    _energyDelta[i] -= conduction;
                    _energyDelta[neighborIdx] += conduction;
                }

                // Б) Распределение конвекционного пула (только вверх)
                if (convectionPool > 0 && totalUpWeight > 0 && edge.hightConductivity > 1.1f)
                {
                    float share = (edge.hightConductivity / totalUpWeight) * convectionPool;
                    _energyDelta[neighborIdx] += share;
                }
            }
        }

        // --- ФАЗА 3: Применение ---
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].energy += _energyDelta[i];
            if (_nodes[i].energy < 0) _nodes[i].energy = 0;
            _nodes[i].temperature = _nodes[i].energy / Mathf.Max(0.1f, _nodes[i].heatCapacity);
        }
    }

    public void ApplyHeat(Vector3 worldPoint, float radius, float energyIntensity)
    {
        if (_spatialGrid == null || _spatialGrid.Count == 0) return;

        var rSqr = radius * radius;
        var minCell = SpatialHash.WorldToCell(worldPoint - new Vector3(radius, radius, radius), _cellSize);
        var maxCell = SpatialHash.WorldToCell(worldPoint + new Vector3(radius, radius, radius), _cellSize);

        _affectedIndices.Clear();

        for (var x = minCell.x; x <= maxCell.x; x++)
        {
            for (var y = minCell.y; y <= maxCell.y; y++)
            {
                for (var z = minCell.z; z <= maxCell.z; z++)
                {
                    var key = SpatialHash.CellKey(x, y, z);
                    if (_spatialGrid.TryGetValue(key, out var cellNodes))
                    {
                        foreach (var idx in cellNodes)
                        {
                            if ((_nodes[idx].position - worldPoint).sqrMagnitude <= rSqr)
                            {
                                _affectedIndices.Add(idx);
                            }
                        }
                    }
                }
            }
        }

        if (_affectedIndices.Count == 0) return;

        var energyPerNode = energyIntensity / _affectedIndices.Count;
        foreach (int idx in _affectedIndices)
        {
            var n = _nodes[idx];
            n.energy += energyPerNode;
            _nodes[idx] = n;
        }
    }
}