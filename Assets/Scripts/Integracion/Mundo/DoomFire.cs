using UnityEngine;
using UnityEngine.UI;

// Fuego de pixel animado (el algoritmo clásico del fuego de DOOM) en una textura chiquita
// que se estira sobre la pantalla: flamas que suben, se mueven con el "viento" y se apagan solas.
[RequireComponent(typeof(RawImage))]
public class DoomFire : MonoBehaviour
{
    private const int W = 160, H = 70;
    private static Color32[] palette;
    private byte[] heat;
    private Color32[] pixels;
    private Texture2D tex;
    private float timer;
    private System.Random rng = new System.Random();

    // 0..1: qué tan fuerte arde la base (0 = se apaga poco a poco).
    public float Intensity { get; set; } = 1f;

    public static DoomFire Create(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("FuegoPixel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.offsetMin = r.offsetMax = Vector2.zero;
        var img = go.AddComponent<RawImage>();
        img.raycastTarget = false;
        return go.AddComponent<DoomFire>();
    }

    private void Awake()
    {
        if (palette == null) BuildPalette();
        heat = new byte[W * H];
        pixels = new Color32[W * H];
        tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        GetComponent<RawImage>().texture = tex;
    }

    private void OnDestroy() { if (tex != null) Destroy(tex); }

    // Negro transparente → rojo oscuro → rojo → naranja → amarillo → blanco.
    private static void BuildPalette()
    {
        palette = new Color32[37];
        Color[] keys = { new Color(0f, 0f, 0f, 0f), new Color(0.3f, 0.02f, 0.02f, 0.75f), new Color(0.75f, 0.08f, 0.02f, 0.95f),
                         new Color(1f, 0.38f, 0.04f, 1f), new Color(1f, 0.75f, 0.15f, 1f), new Color(1f, 0.95f, 0.55f, 1f), new Color(1f, 1f, 0.95f, 1f) };
        for (int i = 0; i < 37; i++)
        {
            float t = i / 36f * (keys.Length - 1);
            int k = Mathf.Min((int)t, keys.Length - 2);
            palette[i] = Color.Lerp(keys[k], keys[k + 1], t - k);
        }
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < 1f / 30f) return;   // 30 cuadros por segundo bastan (y se ve más pixel)
        timer = 0f;

        // Base: fila de abajo encendida según la intensidad.
        int baseHeat = Mathf.RoundToInt(36 * Mathf.Clamp01(Intensity));
        for (int x = 0; x < W; x++)
            heat[x] = (byte)(Intensity > 0.01f ? Mathf.Max(0, baseHeat - rng.Next(0, 4)) : Mathf.Max(0, heat[x] - 3));

        // Propagación hacia arriba con enfriamiento aleatorio y viento.
        for (int y = 1; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int src = (y - 1) * W + x;
                int r = rng.Next(0, 4);
                int dst = y * W + Mathf.Clamp(x - r + 1, 0, W - 1);
                int v = heat[src] - (r & 1);
                heat[dst] = (byte)Mathf.Max(0, v);
            }

        for (int i = 0; i < heat.Length; i++) pixels[i] = palette[heat[i]];
        tex.SetPixels32(pixels);
        tex.Apply(false);
    }
}
