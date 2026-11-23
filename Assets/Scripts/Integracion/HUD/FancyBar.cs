using UnityEngine;
using UnityEngine.UI;

// Barra de vida estilizada (pixel): marco de dos colores, fondo, "retraso" que baja despacio,
// relleno con brillo arriba y sombra abajo, marcas de segmentos, destello al recibir daño
// y un reflejo que la recorre de vez en cuando. La usan la vida de Pogoman, el escudo y el Diablo.
public class FancyBar : MonoBehaviour
{
    public RectTransform Fill { get; private set; }
    public RectTransform Lag { get; private set; }
    public Image FillImage { get; private set; }
    public Image LagImage { get; private set; }
    public RectTransform Interior { get; private set; }

    private Image flash, shine;
    private RectTransform shineRect;
    private float flashK, shineT, shineEvery = 4f;
    private float value = 1f, lag = 1f;
    private float lagSpeed = 0.35f;

    private static Sprite white;
    private static Sprite White()
    {
        if (white != null) return white;
        var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        return white = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
    }

    public static Image Img(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = White();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    public static RectTransform Stretch(Graphic g, float inset)
    {
        RectTransform r = g.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        return r;
    }

    // Arma la barra dentro de "area" (que ya tiene tamaño y posición).
    public static FancyBar Build(RectTransform area, Color fillColor, Color lagColor, Color frameOuter, Color frameInner,
        int ticks = 10, float outer = 3f, float inner = 2f)
    {
        var bar = area.gameObject.AddComponent<FancyBar>();
        Stretch(Img("MarcoFuera", area, frameOuter), -outer - inner);
        Stretch(Img("MarcoDentro", area, frameInner), -inner);
        Image bg = Img("Fondo", area, new Color(0.06f, 0.02f, 0.03f, 0.95f));
        Stretch(bg, 0f);
        bar.Interior = bg.rectTransform;
        bg.gameObject.AddComponent<RectMask2D>();

        bar.LagImage = Img("Retraso", bar.Interior, lagColor);
        bar.Lag = bar.LagImage.rectTransform;
        bar.Lag.anchorMin = Vector2.zero; bar.Lag.anchorMax = Vector2.one; bar.Lag.offsetMin = bar.Lag.offsetMax = Vector2.zero;
        bar.Lag.pivot = new Vector2(0f, 0.5f);

        bar.FillImage = Img("Relleno", bar.Interior, fillColor);
        bar.Fill = bar.FillImage.rectTransform;
        bar.Fill.anchorMin = Vector2.zero; bar.Fill.anchorMax = Vector2.one; bar.Fill.offsetMin = bar.Fill.offsetMax = Vector2.zero;
        bar.Fill.pivot = new Vector2(0f, 0.5f);

        // Brillo arriba y sombra abajo (en escalones: se ve pixel).
        RectTransform hi = Img("Brillo", bar.Fill, new Color(1f, 1f, 1f, 0.28f)).rectTransform;
        hi.anchorMin = new Vector2(0f, 0.62f); hi.anchorMax = new Vector2(1f, 0.82f); hi.offsetMin = hi.offsetMax = Vector2.zero;
        RectTransform hi2 = Img("Brillo2", bar.Fill, new Color(1f, 1f, 1f, 0.12f)).rectTransform;
        hi2.anchorMin = new Vector2(0f, 0.82f); hi2.anchorMax = Vector2.one; hi2.offsetMin = hi2.offsetMax = Vector2.zero;
        RectTransform sh = Img("Sombra", bar.Fill, new Color(0f, 0f, 0f, 0.3f)).rectTransform;
        sh.anchorMin = Vector2.zero; sh.anchorMax = new Vector2(1f, 0.25f); sh.offsetMin = sh.offsetMax = Vector2.zero;

        // Segmentos.
        for (int i = 1; i < ticks; i++)
        {
            RectTransform t = Img("Marca", bar.Interior, new Color(0f, 0f, 0f, 0.35f)).rectTransform;
            t.anchorMin = new Vector2(i / (float)ticks, 0f); t.anchorMax = new Vector2(i / (float)ticks, 1f);
            t.sizeDelta = new Vector2(2f, 0f);
        }

        // Reflejo diagonal que pasa.
        bar.shine = Img("Reflejo", bar.Interior, new Color(1f, 1f, 1f, 0f));
        bar.shineRect = bar.shine.rectTransform;
        bar.shineRect.anchorMin = new Vector2(0f, -0.5f); bar.shineRect.anchorMax = new Vector2(0f, 1.5f);
        bar.shineRect.sizeDelta = new Vector2(18f, 0f);
        bar.shineRect.localRotation = Quaternion.Euler(0f, 0f, -25f);

        bar.flash = Img("Destello", bar.Interior, new Color(1f, 1f, 1f, 0f));
        Stretch(bar.flash, 0f);
        bar.shineT = Random.value * 3f;
        return bar;
    }

    public void SetShineEvery(float seconds) => shineEvery = seconds;
    public void SetLagSpeed(float perSecond) => lagSpeed = perSecond;

    // value y lag de 0 a 1. Si baja la vida, destella.
    public void Set(float v)
    {
        v = Mathf.Clamp01(v);
        if (v < value - 0.002f) flashK = 1f;
        value = v;
        if (lag < value) lag = value;
    }

    public void SetColor(Color c) { if (FillImage != null) FillImage.color = c; }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        lag = Mathf.MoveTowards(lag, value, dt * lagSpeed);
        Fill.anchorMax = new Vector2(value, 1f);
        Lag.anchorMax = new Vector2(lag, 1f);
        flashK = Mathf.MoveTowards(flashK, 0f, dt * 4f);
        flash.color = new Color(1f, 1f, 1f, 0.55f * flashK);
        shineT += dt;
        float k = Mathf.Repeat(shineT, shineEvery) / 0.7f;
        if (k <= 1f)
        {
            shineRect.anchorMin = new Vector2(k * value, -0.5f);
            shineRect.anchorMax = new Vector2(k * value, 1.5f);
            shine.color = new Color(1f, 1f, 1f, 0.45f * Mathf.Sin(k * Mathf.PI));
        }
        else shine.color = new Color(1f, 1f, 1f, 0f);
    }
}
