using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class Laser : MonoBehaviour
{
    [Header("--- Common Settings ---")]
    [SerializeField] private float range = 100f;
    [SerializeField] private Vector3 offset = new Vector3(0.3f, -0.2f, 0.5f);

    [Header("--- Mode: Continuous (LMB) ---")]
    [Tooltip("Задержка между тиками урона. Чем меньше, тем чаще расчет.")]
    [SerializeField] private float fireRate = 0.1f; 
    [Tooltip("Сколько тепла передается за ОДИН тик.")]
    [SerializeField] private float heatPerTick = 500f; 
    [SerializeField] private float heatRadius = 0.2f;    
    [SerializeField] private float standardWidth = 0.1f;

    [Header("--- Stats (Read Only) ---")]
    [ReadOnly] [SerializeField] private string laserOutputInfo;
    [ReadOnly] [SerializeField] private float ticksPerSecond;
    [ReadOnly] [SerializeField] private float totalHeatPerSecond;

    [Header("--- Mode: Power Shot (RMB) ---")]
    [SerializeField] private float powerShotCooldown = 1.0f;
    [SerializeField] private float powerShotRadius = 0.5f;
    [SerializeField] private float powerShotDamage = 100000f;
    [SerializeField] private float powerWidth = 0.4f;     
    [SerializeField] private float flashDuration = 0.1f;  
    
    private FireSystem _fireSystem;
    private LineRenderer lineRenderer;
    private Camera mainCam;
    
    private float nextFireTime;
    private float nextPowerShotTime;
    private bool isPowerFlashing = false;

    private void OnValidate()
    {
        // Математика для Инспектора
        if (fireRate > 0)
        {
            ticksPerSecond = 1f / fireRate;
            totalHeatPerSecond = ticksPerSecond * heatPerTick;
            laserOutputInfo = $"Жарит {ticksPerSecond:F1} раз(а) в сек. Всего: {totalHeatPerSecond:F1} ед/сек";
        }
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        mainCam = Camera.main;
        _fireSystem = FindObjectOfType<FireSystem>();
        lineRenderer.startWidth = standardWidth;
        lineRenderer.endWidth = standardWidth;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.wasPressedThisFrame && Time.time >= nextPowerShotTime)
        {
            StartCoroutine(ExecutePowerShotRoutine());
            nextPowerShotTime = Time.time + powerShotCooldown;
        }

        if (mouse.leftButton.isPressed && !isPowerFlashing)
        {
            UpdateLaserVisuals();
            
            if (Time.time >= nextFireTime)
            {
                ApplyHeat(heatRadius, heatPerTick);
                nextFireTime = Time.time + fireRate;
            }
        }
        else if (!isPowerFlashing)
        {
            lineRenderer.enabled = false;
        }
    }

    private IEnumerator ExecutePowerShotRoutine()
    {
        isPowerFlashing = true;
        
        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 hitPoint = Physics.Raycast(ray, out RaycastHit hit, range) 
            ? hit.point 
            : ray.origin + (ray.direction * range);

        ApplyHeat(powerShotRadius, powerShotDamage);

        lineRenderer.enabled = true;
        lineRenderer.startWidth = powerWidth;
        lineRenderer.endWidth = powerWidth;
        lineRenderer.SetPosition(0, mainCam.transform.TransformPoint(offset));
        lineRenderer.SetPosition(1, hitPoint);

        yield return new WaitForSeconds(flashDuration);

        lineRenderer.startWidth = standardWidth;
        lineRenderer.endWidth = standardWidth;
        lineRenderer.enabled = false;
        isPowerFlashing = false;
    }

    private void UpdateLaserVisuals()
    {
        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 hitPoint = Physics.Raycast(ray, out RaycastHit hit, range) 
            ? hit.point 
            : ray.origin + (ray.direction * range);

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, mainCam.transform.TransformPoint(offset));
        lineRenderer.SetPosition(1, hitPoint);
    }

    private void ApplyHeat(float radius, float intensity)
    {
        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            if (_fireSystem)
                _fireSystem.ApplyHeat(hit.point, radius, intensity);
        }
    }
}

// Простой атрибут, чтобы сделать поля "только для чтения" в инспекторе
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        UnityEditor.EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;
    }
}
#endif