using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FireGraph))]
public class FireGraphBakerEditor : Editor
{
    private float _targetEdgeLength = 0.24f;
    private bool _enableSimplification = true;
    private float _clusterRadius = 0.067f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Baking Settings", EditorStyles.boldLabel);
        _targetEdgeLength = EditorGUILayout.Slider("Target Grid Size (m)", _targetEdgeLength, 0.05f, 1.0f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Simplification Settings", EditorStyles.boldLabel);
        _enableSimplification = EditorGUILayout.Toggle("Enable Simplification", _enableSimplification);

        if (_enableSimplification)
        {
            _clusterRadius = EditorGUILayout.Slider("Cluster Radius (m)", _clusterRadius, 0f, 0.5f);
            EditorGUILayout.HelpBox(
                "Optimization: \n1. Clustering \n2. Linear node removal \n3. Hanging node removal",
                MessageType.Info
            );
        }

        if (GUILayout.Button("Bake Fire Graph", GUILayout.Height(30)))
        {
            FireGraph graph = (FireGraph)target;
            Undo.RecordObject(graph, "Bake Fire Graph"); // Добавили поддержку Undo
            FireGraphProcessor.Bake(graph, _targetEdgeLength, _enableSimplification, _clusterRadius);
            EditorUtility.SetDirty(graph);
        }
    }

    [MenuItem("Tools/Fire System/Bake Selected")]
    static void BakeSelected()
    {
        foreach (var go in Selection.gameObjects)
        {
            var graph = go.GetComponent<FireGraph>();
            if (graph)
            {
                FireGraphProcessor.Bake(graph, 0.24f, true, 0.067f);
                EditorUtility.SetDirty(graph);
            }
        }
    }
}