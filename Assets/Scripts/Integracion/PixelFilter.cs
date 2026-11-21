using UnityEngine;
using UnityEngine.UI;

// Filtro retro suave: la cámara dibuja a menor resolución y se muestra con
// píxeles nítidos (sin difuminar). El HUD se dibuja encima a resolución completa.
// pixelSize = 1 lo desactiva; 2 es sutil; el menú usa 4.
[RequireComponent(typeof(Camera))]
public class PixelFilter : MonoBehaviour
{
    [Range(1, 6)]
    [SerializeField] private int pixelSize = 2;

    private Camera cam;
    private RenderTexture rt;
    private RawImage output;
    private Camera clearCamera;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        // Lienzo detrás de todo el HUD donde se muestra la imagen pixelada.
        var canvasGO = new GameObject("FiltroRetro", typeof(Canvas));
        canvasGO.layer = 5;
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;

        var img = new GameObject("Imagen", typeof(RectTransform), typeof(RawImage));
        img.layer = 5;
        img.transform.SetParent(canvasGO.transform, false);
        var rect = (RectTransform)img.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        output = img.GetComponent<RawImage>();
        output.raycastTarget = false;

        // Cámara mínima que solo limpia la pantalla (la principal dibuja a la textura).
        clearCamera = new GameObject("CamaraPantalla").AddComponent<Camera>();
        clearCamera.clearFlags = CameraClearFlags.SolidColor;
        clearCamera.backgroundColor = Color.black;
        clearCamera.cullingMask = 0;
        clearCamera.depth = cam.depth - 10;

        Apply();
    }

    private void Update() => Apply();

    private void Apply()
    {
        if (pixelSize <= 1)
        {
            if (cam.targetTexture != null)
                cam.targetTexture = null;
            output.enabled = false;
            return;
        }

        int w = Mathf.Max(1, Screen.width / pixelSize);
        int h = Mathf.Max(1, Screen.height / pixelSize);

        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null)
            {
                cam.targetTexture = null;
                rt.Release();
                Destroy(rt);
            }

            rt = new RenderTexture(w, h, 24) { name = "JuegoPixelado", filterMode = FilterMode.Point };
            rt.Create();
        }

        cam.targetTexture = rt;
        output.texture = rt;
        output.enabled = true;
    }

    private void OnDestroy()
    {
        if (rt != null)
        {
            if (cam != null)
                cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
        }
    }
}
