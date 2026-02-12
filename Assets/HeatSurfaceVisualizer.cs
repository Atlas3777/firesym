// HeatSurfaceVisualizer.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(VertexFireGraph))]
public class HeatSurfaceVisualizer : MonoBehaviour
{
    public Gradient heatGradient;
    public float maxTemperature = 1000f;
    public float updateInterval = 0.1f;

    private VertexFireGraph _fireGraph;
    private MeshFilter[] _meshFilters;
    private float _lastUpdate;

    void Start()
    {
        _fireGraph = GetComponent<VertexFireGraph>();

        // Градиент по умолчанию
        if (heatGradient == null)
        {
            heatGradient = new Gradient();
            heatGradient.SetKeys(
                new[] {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.blue, 0.3f),
                    new GradientColorKey(Color.cyan, 0.5f),
                    new GradientColorKey(Color.yellow, 0.7f),
                    new GradientColorKey(Color.red, 0.9f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
        }

        // Собираем только обычные меш-фильтры (без Combined Mesh)
        var allFilters = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        var valid = new List<MeshFilter>();

        foreach (var mf in allFilters)
        {
            if (!mf || !mf.gameObject.activeInHierarchy) continue;
            if (mf.name.Contains("Combined Mesh")) continue;
            if (!mf.sharedMesh || !mf.sharedMesh.isReadable) continue;

            // Создаём уникальную копию меша для редактирования цветов
            Mesh instanceMesh = Instantiate(mf.sharedMesh);
            mf.mesh = instanceMesh;

            // Инициализация чёрными цветами
            Color[] colors = new Color[instanceMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0, 0, 0, 0);
            instanceMesh.colors = colors;

            valid.Add(mf);
        }

        _meshFilters = valid.ToArray();

        // Источник огня
        if (_fireGraph.fireSource != null)
            _fireGraph.SetInfiniteHeatSource(_fireGraph.fireSource.position, radius: 0.3f);
    }

    void Update()
    {
        if (Time.time - _lastUpdate < updateInterval) return;
        _lastUpdate = Time.time;

        if (_fireGraph.nodes == null || _fireGraph.nodes.Count == 0) return;

        foreach (var mf in _meshFilters)
        {
            Mesh mesh = mf.mesh;
            if (!mesh) continue;

            Vector3[] vertices = mesh.vertices;
            Color[] colors = new Color[vertices.Length];
            Transform tx = mf.transform;

            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 worldPos = tx.TransformPoint(vertices[v]);
                float temp = GetTemperatureAt(worldPos);
                float t = Mathf.InverseLerp(0f, maxTemperature, temp);
                Color heatColor = heatGradient.Evaluate(t);
                colors[v] = new Color(heatColor.r, heatColor.g, heatColor.b, t); // alpha = интенсивность
            }

            mesh.colors = colors;
        }
    }

    private float GetTemperatureAt(Vector3 worldPos)
    {
        float maxTemp = 0f;
        foreach (var node in _fireGraph.nodes)
        {
            float dist = Vector3.Distance(node.position, worldPos);
            if (dist <= 0.35f) // радиус влияния
            {
                float influence = 1f - Mathf.Clamp01(dist / 0.35f);
                maxTemp = Mathf.Max(maxTemp, node.temperature * influence);
            }
        }
        return maxTemp;
    }
}
