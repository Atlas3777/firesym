using UnityEngine;
using System.Collections.Generic;

public class FireGraph : MonoBehaviour
{
    public bool Draw;
    [SerializeField] private List<FireNode> _nodes = new();
    public List<FireNode> Nodes => _nodes;

    public void SetNodeList(List<FireNode> nodes)
    {
        _nodes = nodes;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if(!Draw) return;
        if (_nodes == null || _nodes.Count == 0) return;

        // 1. Рисуем узлы
        foreach (var node in _nodes)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(node.position, 0.05f);

            // 2. Рисуем связи
            foreach (var edge in node.neighbors)
            {
                // Исправлено: прямой доступ к targetIndex
                int targetIndex = edge.targetIndex;
                if (targetIndex < 0 || targetIndex >= _nodes.Count) continue;

                Vector3 endPos = _nodes[targetIndex].position;
                Gizmos.color = Color.Lerp(Color.gray, Color.yellow,
                    Mathf.Clamp01(1.0f / (edge.conductivity + 0.1f)));
                Gizmos.DrawLine(node.position, endPos);
            }
        }

        // 3. Горящие узлы (для рантайма)
        foreach (var node in _nodes)
        {
            if (node.burnProgress > 0.1f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(node.position, 0.08f);
            }
        }
    }
#endif
}
