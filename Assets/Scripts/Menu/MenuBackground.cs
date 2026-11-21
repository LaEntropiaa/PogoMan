using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Fondo vivo del menú: una cámara 3D mira el mapa real y se renderiza a baja
// resolución (con filtro "Point") para que se vea pixelado. Además mueve la
// cámara muy poco (respiración + parallax con el mouse) y hace parpadear luces.
public class MenuBackground : MonoBehaviour
{
    [Header("Render pixelado")]
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private RawImage target;
    [Tooltip("Alto en píxeles del render. Menos = más pixelado (270 = píxeles de 4x4 en 1080p).")]
    [SerializeField] private int pixelHeight = 270;

    [Header("Movimiento de cámara")]
    [SerializeField] private float swayAmount = 0.3f;
    [SerializeField] private float swaySpeed = 0.1f;
    [SerializeField] private float mouseParallax = 0.5f;
    [SerializeField] private float parallaxSmoothing = 2.5f;

    [Header("Luces que parpadean")]
    [SerializeField] private Transform flickerLightsRoot;
    [SerializeField] private float flickerAmount = 0.2f;
    [SerializeField] private float flickerSpeed = 2.5f;

    private RenderTexture renderTexture;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector2 parallax;
    private Light[] lights;
    private float[] baseIntensities;

    private void Start()
    {
        if (sceneCamera == null || target == null)
        {
            Debug.LogWarning("MenuBackground: falta la cámara o la imagen destino.");
            enabled = false;
            return;
        }

        basePosition = sceneCamera.transform.position;
        baseRotation = sceneCamera.transform.rotation;

        if (flickerLightsRoot != null)
        {
            lights = flickerLightsRoot.GetComponentsInChildren<Light>();
            baseIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                baseIntensities[i] = lights[i].intensity;
        }

        EnsureRenderTexture();
    }

    private void Update()
    {
        EnsureRenderTexture();

        float t = Time.unscaledTime;

        if (Mouse.current != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 m = Mouse.current.position.ReadValue();
            Vector2 n = new Vector2(m.x / Screen.width * 2f - 1f, m.y / Screen.height * 2f - 1f);
            n = Vector2.ClampMagnitude(n, 1f);
            parallax = Vector2.Lerp(parallax, n, 1f - Mathf.Exp(-parallaxSmoothing * Time.unscaledDeltaTime));
        }

        Transform cam = sceneCamera.transform;
        Vector3 right = baseRotation * Vector3.right;
        Vector3 up = baseRotation * Vector3.up;

        Vector3 sway =
            right * Mathf.Sin(t * swaySpeed * Mathf.PI * 2f) * swayAmount +
            up * Mathf.Sin(t * swaySpeed * 1.37f * Mathf.PI * 2f) * swayAmount * 0.5f;

        cam.position = basePosition + sway + right * parallax.x * mouseParallax + up * parallax.y * mouseParallax * 0.5f;
        cam.rotation = baseRotation * Quaternion.Euler(-parallax.y * 0.5f, parallax.x * 0.8f, 0f);

        if (lights != null)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                float noise = Mathf.PerlinNoise(i * 7.31f, t * flickerSpeed) * 2f - 1f;
                lights[i].intensity = baseIntensities[i] * (1f + noise * flickerAmount);
            }
        }
    }

    private void EnsureRenderTexture()
    {
        int h = Mathf.Max(90, pixelHeight);
        int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)Screen.width / Mathf.Max(1, Screen.height)));

        if (renderTexture != null && renderTexture.width == w && renderTexture.height == h)
            return;

        if (renderTexture != null)
        {
            sceneCamera.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
        }

        renderTexture = new RenderTexture(w, h, 24)
        {
            name = "MenuFondoPixelado",
            filterMode = FilterMode.Point,
        };
        renderTexture.Create();

        sceneCamera.targetTexture = renderTexture;
        target.texture = renderTexture;
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            if (sceneCamera != null)
                sceneCamera.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
