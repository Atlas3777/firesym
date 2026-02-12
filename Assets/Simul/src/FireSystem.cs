using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class FireSystem : MonoBehaviour
{
    [Header("Heat Settings")] public float thermalConductivity = 0.67f; // Насколько быстро тепло уходит соседям
    public float coolingSpeed = 0.003f; // насколько уменьшаяется t за тик
    public float maxTemperature = 500f; // Температура "белого каления"

    [Header("Simulation Data")] [HideInInspector]
    public SimNode[] nodes;

    [HideInInspector] public SimEdge[] edges;
    public int[] edgeOffsets;
    public int[] edgeCounts;

    [Header("Visualization Settings")] public bool showVisualization = true;
    public ParticleSystem fireParticleSystem;
    [Range(0.01f, 0.5f)] public float nodeScale = 0.1f;

    private Dictionary<int, List<int>> _spatialGrid;
    private float _cellSize;
    List<int> _affectedIndices = new(256);
    

    private ParticleSystem.Particle[] _particles;
    private bool _initialized = false;
    private FireSystemBuilder _builder;

    private const float SIM_STEP = 0.1f;
    private float _timer;
    private float[] _energyDelta;

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
        nodes = newNodes.ToArray();
        edges = newEdges.ToArray();
        edgeOffsets = newOffsets;
        edgeCounts = newCounts;

        // Инициализируем массив частиц один раз
        _particles = new ParticleSystem.Particle[nodes.Length];

        for (int i = 0; i < nodes.Length; i++)
        {
            _particles[i].position = nodes[i].position;
            _particles[i].startSize = nodeScale;
            _particles[i].startColor = Color.gray;

            // ВАЖНО: Без этого частицы мгновенно исчезают!
            _particles[i].remainingLifetime = 10000f;
            _particles[i].startLifetime = 10000f;
        }

        // Очищаем старые и принудительно выставляем новые
        fireParticleSystem.Clear();
        fireParticleSystem.SetParticles(_particles, nodes.Length);


        // Сразу задаем позиции, так как они в вашей симуляции статичны
        for (int i = 0; i < nodes.Length; i++)
        {
            _particles[i].position = nodes[i].position;
            _particles[i].startSize = nodeScale;
            _particles[i].startColor = Color.gray;
        }

        _spatialGrid = spatialGrid;
        _cellSize = cellSize;

        _energyDelta = new float[nodes.Length];
        fireParticleSystem.Emit(nodes.Length);
        Debug.Log($"<color=orange>FireSystem Running:</color> {nodes.Length} particles.");
    }

    void Update()
    {
        if (nodes == null || nodes.Length == 0) return;

        _timer += Time.deltaTime;
        if (_timer >= SIM_STEP)
        {
            StepSimulation();
            _timer -= SIM_STEP;
            if (showVisualization && fireParticleSystem != null)
            {
                UpdateParticles();
            }
        }
    }

    private void UpdateParticles()
    {
        int count = nodes.Length;
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Clamp01(nodes[i].energy / maxTemperature);
            _particles[i].startColor = GetHeatmapColor(t);
            _particles[i].startSize = nodeScale * (1f + t * 0.3f);
        }

        fireParticleSystem.SetParticles(_particles, count);
    }

    private Color GetHeatmapColor(float t)
    {
        // Стандартная схема тепловизора:
        // 0.0: Черный/Фиолетовый
        // 0.2: Синий
        // 0.4: Зеленый
        // 0.6: Желтый
        // 0.8: Оранжевый/Красный
        // 1.0: Белый

        if (t < 0.2f) return Color.Lerp(new Color(0.1f, 0f, 0.2f), Color.blue, t / 0.2f);
        if (t < 0.4f) return Color.Lerp(Color.blue, Color.green, (t - 0.2f) / 0.2f);
        if (t < 0.6f) return Color.Lerp(Color.green, Color.yellow, (t - 0.4f) / 0.2f);
        if (t < 0.8f) return Color.Lerp(Color.yellow, new Color(1f, 0.3f, 0f), (t - 0.6f) / 0.2f);
        return Color.Lerp(new Color(1f, 0.3f, 0f), Color.white, (t - 0.8f) / 0.2f);
    }

    private void StepSimulation()
    {
        Array.Clear(_energyDelta, 0, _energyDelta.Length);

        for (int i = 0; i < nodes.Length; i++)
        {
            float currentEnergy = nodes[i].energy;
            if (currentEnergy <= 0) continue;

            // 1. Остывание
            _energyDelta[i] -= currentEnergy * coolingSpeed;

            // 2. Передача соседям
            var start = edgeOffsets[i];
            var count = edgeCounts[i];

            if (count > 0)
            {
                for (var j = 0; j < count; j++)
                {
                    var neighborIdx = edges[start + j].targetIndex;
                    var neighborEnergy = nodes[neighborIdx].energy;

                    // Передаем энергию только если мы "горячее" соседа
                    if (currentEnergy > neighborEnergy)
                    {
                        float diff = currentEnergy - neighborEnergy;

                        // Делим на count, чтобы распределить поток между всеми трубками
                        // Теперь даже если thermalConductivity = 1.0, 
                        // узел не отдаст больше, чем разница с соседями.
                        float transfer = (diff * thermalConductivity) / count;

                        _energyDelta[i] -= transfer;
                        _energyDelta[neighborIdx] += transfer;
                    }
                }
            }
        }

        // Применяем изменения
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            // Mathf.Max(0, ...) — это "подушка безопасности"
            n.energy = n.energy + _energyDelta[i];
            nodes[i] = n;
        }
    }

    public void ApplyHeat(Vector3 worldPoint, float radius, float energyIntensity)
    {
        if (_spatialGrid == null || _spatialGrid.Count == 0) return;

        float rSqr = radius * radius;

        // Определяем диапазон ячеек, которые покрывает радиус
        // Берем с запасом (WorldToCell должен возвращать Vector3Int)
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
                            if ((nodes[idx].position - worldPoint).sqrMagnitude <= rSqr)
                            {
                                _affectedIndices.Add(idx);
                            }
                        }
                    }
                }
            }
        }

        if (_affectedIndices.Count == 0) return;

        // Распределяем энергию
        float energyPerNode = energyIntensity / _affectedIndices.Count;
        foreach (int idx in _affectedIndices)
        {
            var n = nodes[idx];
            n.energy += energyPerNode;
            nodes[idx] = n;
        }
    }
}