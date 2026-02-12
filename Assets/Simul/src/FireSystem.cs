using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class FireSystem : MonoBehaviour
{
    public ParticleSystem fireParticleSystem;

    //public float thermalConductivity = 0.67f; // Сколько разницы отдаём
    public float coolingSpeed = 0.01f; // насколько уменьшаяется t за тик  
    public float maxTemperature = 500f; // Температура "белого каления"
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

        // Инициализируем массив частиц один раз  
        _particles = new ParticleSystem.Particle[_nodes.Length];

        for (var i = 0; i < _nodes.Length; i++)
        {
            _particles[i].position = _nodes[i].position;
            _particles[i].startSize = nodeScale;
            _particles[i].startColor = Color.gray;

            _particles[i].remainingLifetime = 10000f;
            _particles[i].startLifetime = 10000f;
        }

        // Очищаем старые и принудительно выставляем новые
        fireParticleSystem.Clear();
        fireParticleSystem.SetParticles(_particles, _nodes.Length);

        _spatialGrid = spatialGrid;
        _cellSize = cellSize;

        _energyDelta = new float[_nodes.Length];
        Debug.Log($"<color=orange>FireSystem Running:</color> {_nodes.Length} particles.");
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
            // Проверяем наличие топлива
            if (_nodes[i].fuel <= 0)
            {
                _particles[i].startColor = Color.mediumVioletRed;
                _particles[i].startSize = nodeScale; // Возвращаем обычный размер для "угля"
            }
            else
            {
                var t = Mathf.Clamp01(_nodes[i].temperature / maxTemperature);
                _particles[i].startColor = GetHeatmapColor(t);
                _particles[i].startSize = nodeScale * (1f + t * 0.3f);
            }
        
            _particles[i].remainingLifetime = 1000f;
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

        // ФАЗА 1: Горение и базовое охлаждение
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
                    _energyDelta[i] += node.calorificValue;
                    if (node.fuel <= 0) node.burning = false;
                }
                else if (node.temperature >= node.ignitionTemp)
                {
                    node.burning = true;
                }
            }

            // Остывание в среду
            float coolingLoss = node.temperature * coolingSpeed;
            _energyDelta[i] -= coolingLoss;
        }

        // ФАЗА 2: Передача энергии (Кондукция)
        for (int i = 0; i < _nodes.Length; i++)
        {
            int start = _edgeOffsets[i];
            int count = _edgeCounts[i];
            if (count == 0) continue;

            for (int j = 0; j < count; j++)
            {
                var edge = _edges[start + j];
                int neighborIdx = edge.targetIndex;

                // Обрабатываем каждое ребро только один раз (i < neighborIdx)
                if (i > neighborIdx) continue;

                float tempA = _nodes[i].temperature;
                float tempB = _nodes[neighborIdx].temperature;

                if (Mathf.Abs(tempA - tempB) < 0.01f) continue;

                // Считаем эффективную проводимость с учетом количества связей обоих узлов
                // Это предотвращает взрывную передачу энергии в плотных сетках
                float avgConnections = (_edgeCounts[i] + _edgeCounts[neighborIdx]) * 0.5f;
                float flowRate = (edge.materialConductivity / avgConnections);

                float avgCapacity = (_nodes[i].heatCapacity + _nodes[neighborIdx].heatCapacity) * 0.5f;

                // Формула: dE = dT * C * conductivity
                float energyTransfer = (tempA - tempB) * avgCapacity * flowRate;

                // Ограничитель, чтобы не передать больше, чем выровняет температуры
                float maxSafeTransfer = Mathf.Abs(tempA - tempB) * 0.5f * avgCapacity;
                energyTransfer = Mathf.Clamp(energyTransfer, -maxSafeTransfer, maxSafeTransfer);

                _energyDelta[i] -= energyTransfer;
                _energyDelta[neighborIdx] += energyTransfer;
            }
        }

        // ФАЗА 3: Применение
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].energy += _energyDelta[i];
            if (_nodes[i].energy < 0) _nodes[i].energy = 0;

            // Обновляем температуру для следующего кадра/отрисовки
            _nodes[i].temperature = _nodes[i].energy / Mathf.Max(0.1f, _nodes[i].heatCapacity);
        }
    }

    public void ApplyHeat(Vector3 worldPoint, float radius, float energyIntensity)
    {
        if (_spatialGrid == null || _spatialGrid.Count == 0) return;

        var rSqr = radius * radius;

        // Определяем диапазон ячеек, которые покрывает радиус  
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

        // Распределяем энергию
        var energyPerNode = energyIntensity / _affectedIndices.Count;
        foreach (int idx in _affectedIndices)
        {
            var n = _nodes[idx];
            n.energy += energyPerNode;
            _nodes[idx] = n;
        }
    }
}