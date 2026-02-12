using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class FireSpread : MonoBehaviour
{
    [Header("Setup")]
    public RenderTexture fireStateA;
    public RenderTexture fireStateB;
    public Material simulatorMat;
    public Transform ignitionSource;

    private RenderTexture currentRead;
    private RenderTexture currentWrite;
    private MeshRenderer quadRenderer;
    private Material visualizerInstance;

    void Start()
    {
        quadRenderer = GetComponent<MeshRenderer>();

        // Создаём инстанс материала для избежания изменений в оригинале
        visualizerInstance = new Material(Shader.Find("Custom/FireVisualizer"));
        quadRenderer.material = visualizerInstance;
        visualizerInstance.SetTexture("_FireTex", fireStateA);

        // Инициализация буферов
        currentRead = fireStateA;
        currentWrite = fireStateB;

        ClearTexture(fireStateA);
        ClearTexture(fireStateB);
    }

    void Update()
    {
        if (ignitionSource == null) return;

        // КОНВЕРТАЦИЯ В ЛОКАЛЬНОЕ ПРОСТРАНСТВО КВАДА (ключевое исправление!)
        Vector3 localPos = transform.InverseTransformPoint(ignitionSource.position);
        float uvX = Mathf.Clamp01(localPos.x + 0.5f);
        float uvY = Mathf.Clamp01(localPos.y + 0.5f);

        simulatorMat.SetVector("_IgnitionPoint", new Vector4(uvX, uvY, 0.15f, 0));
        simulatorMat.SetFloat("_SpreadSpeed", 0.3f);
        simulatorMat.SetTexture("_MainTex", currentRead);

        // Двойная буферизация
        Graphics.Blit(currentRead, currentWrite, simulatorMat);
        visualizerInstance.SetTexture("_FireTex", currentWrite);

        // Своп буферов
        (currentRead, currentWrite) = (currentWrite, currentRead);
    }

    void ClearTexture(RenderTexture rt)
    {
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;
    }

    void OnDestroy()
    {
        if (visualizerInstance != null)
            Destroy(visualizerInstance);
    }
}
