using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// El Diablo, vencido y de rodillas, ofrece un trato:
//  1) ACABAR CON ÉL   → termina la partida (victoria y créditos).
//  2) ACEPTAR SU OFERTA → un objeto legendario al azar y de vuelta al infierno en modo infinito.
public class DevilOffer : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private System.Action onKill, onDeal;
    private CanvasGroup group;
    private Button first;

    public static void Show(Font font, ItemDefinition preview, System.Action kill, System.Action deal)
    {
        var go = new GameObject("OfertaDelDiablo");
        var o = go.AddComponent<DevilOffer>();
        o.onKill = kill;
        o.onDeal = deal;
        o.Build(font, preview);
        IsOpen = true;
    }

    private void OnDestroy() => IsOpen = false;

    private struct Card { public RectTransform rt; public Button button; public Image glow; public Color accent; }
    private readonly System.Collections.Generic.List<Card> cards = new System.Collections.Generic.List<Card>();
    private RectTransform title;

    private void Build(Font font, ItemDefinition preview)
    {
        SceneNav.SetGameplayCursor(false);
        Canvas canvas = UiKit.MakeCanvas("Oferta", 28);
        canvas.transform.SetParent(transform, false);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        Transform r = canvas.transform;
        UiKit.Image(UiKit.Stretch("Fondo", r), new Color(0.05f, 0f, 0.02f, 0.82f));
        // Franjas de color arriba y abajo (rojo sangre y dorado).
        Band(r, 1f, new Color(0.45f, 0.02f, 0.04f, 0.9f), 150f);
        Band(r, 0f, new Color(0.3f, 0.15f, 0.02f, 0.9f), 110f);

        Text t = UiKit.Text(UiKit.Rect("Titulo", r, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(1700f, 100f)),
            font, 76, Color.white, TextAnchor.MiddleCenter, "EL DIABLO TE OFRECE UN TRATO");
        var tfx = t.gameObject.AddComponent<PixelTextFx>();
        tfx.top = new Color(1f, 0.95f, 0.6f); tfx.bottom = new Color(1f, 0.3f, 0.08f); tfx.depth = 5; tfx.step = 3f; tfx.shineEvery = 2.5f;
        title = t.rectTransform;
        Text d = UiKit.Text(UiKit.Rect("Dialogo", r, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(1500f, 90f)),
            font, 30, new Color(1f, 0.88f, 0.82f), TextAnchor.MiddleCenter,
            "\"Ya, ya, me ganaste. Perdóname la vida y te doy algo bonito para tu pogo...\n...nomás que te quedas aquí abajo. Para siempre.\"");
        d.gameObject.AddComponent<PixelTextFx>().depth = 2;

        first = Option(r, font, new Vector2(-390f, -60f), "ACABAR CON ÉL", "Termina la partida.\nEl infierno se queda sin dueño.",
            new Color(0.55f, 0.04f, 0.06f), new Color(0.2f, 0f, 0.02f), new Color(1f, 0.35f, 0.25f), Skull, () => Choose(onKill));
        Option(r, font, new Vector2(390f, -60f), "ACEPTAR SU OFERTA", "Un objeto LEGENDARIO al azar\ny de vuelta al infierno: modo infinito.\nEl infierno entero te va a cazar:\ncada minuto todo se vuelve más fuerte.",
            new Color(0.35f, 0.08f, 0.45f), new Color(0.12f, 0.02f, 0.15f), new Color(1f, 0.8f, 0.25f), Crown, () => Choose(onDeal));
        StartCoroutine(FadeIn());
    }

    private static void Band(Transform r, float y, Color c, float h)
    {
        RectTransform b = UiKit.Rect("Franja", r, new Vector2(0f, y), new Vector2(1f, y), new Vector2(0.5f, y), Vector2.zero, new Vector2(0f, h));
        UiKit.Image(b, c);
        RectTransform line = UiKit.Rect("Linea", b, new Vector2(0f, 1f - y), new Vector2(1f, 1f - y), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 4f));
        UiKit.Image(line, new Color(0.95f, 0.7f, 0.25f));
    }

    // Íconos en pixel art (cada letra X es un cuadrito).
    private static readonly string[] Skull =
    {
        "..XXXXX..", ".XXXXXXX.", "XXXXXXXXX", "XX..X..XX", "XX..X..XX", "XXXXXXXXX", ".XXX.XXX.", "..X.X.X..", "..XXXXX..",
    };
    private static readonly string[] Crown =
    {
        "X...X...X", "XX.XXX.XX", "XXXXXXXXX", "XXXXXXXXX", ".XXXXXXX.", ".X.X.X.X.", ".XXXXXXX.", ".........", "XXXXXXXXX",
    };

    private static void Icon(RectTransform parent, string[] pattern, Color c, float px, Vector2 pos)
    {
        RectTransform icon = UiKit.Rect("Icono", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), pos, new Vector2(px * 9f, px * 9f));
        for (int y = 0; y < pattern.Length; y++)
            for (int x = 0; x < pattern[y].Length; x++)
            {
                if (pattern[y][x] != 'X') continue;
                RectTransform p = FancyBar.Img("P", icon, c).rectTransform;
                p.anchorMin = p.anchorMax = new Vector2(0f, 1f);
                p.pivot = new Vector2(0f, 1f);
                p.sizeDelta = new Vector2(px, px);
                p.anchoredPosition = new Vector2(x * px, -y * px);
                // Sombra de un pixel (se ve "pintado a mano").
                RectTransform s = FancyBar.Img("S", icon, new Color(0f, 0f, 0f, 0.5f)).rectTransform;
                s.anchorMin = s.anchorMax = new Vector2(0f, 1f); s.pivot = new Vector2(0f, 1f);
                s.sizeDelta = new Vector2(px, px); s.anchoredPosition = new Vector2(x * px + px * 0.4f, -y * px - px * 0.4f);
                s.SetSiblingIndex(0);
            }
    }

    private Button Option(Transform r, Font font, Vector2 pos, string titleText, string body, Color top, Color bottom, Color accent,
        string[] icon, UnityEngine.Events.UnityAction a)
    {
        RectTransform rt = UiKit.Rect(titleText, r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(680f, 380f));
        // Resplandor (crece al seleccionar) + marco dorado + marco oscuro + degradado en franjas.
        Image glow = FancyBar.Img("Resplandor", rt, new Color(accent.r, accent.g, accent.b, 0.25f));
        FancyBar.Stretch(glow, -16f);
        FancyBar.Stretch(FancyBar.Img("Oro", rt, new Color(0.95f, 0.72f, 0.25f)), -6f);
        FancyBar.Stretch(FancyBar.Img("Oscuro", rt, new Color(0.08f, 0.02f, 0.02f)), -2f);
        Image img = FancyBar.Img("Fondo", rt, top);
        FancyBar.Stretch(img, 0f);
        img.raycastTarget = true;
        for (int i = 0; i < 6; i++)
        {
            RectTransform band = FancyBar.Img("Degradado", rt, Color.Lerp(top, bottom, (i + 1) / 6f)).rectTransform;
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(1f, 1f - (i + 1) / 7f);
            band.offsetMin = band.offsetMax = Vector2.zero;
        }
        // Esquinas.
        foreach (Vector2 c in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
        {
            RectTransform k = FancyBar.Img("Esquina", rt, accent).rectTransform;
            k.anchorMin = k.anchorMax = c; k.sizeDelta = new Vector2(22f, 22f); k.localRotation = Quaternion.Euler(0, 0, 45f);
        }
        Icon(rt, icon, accent, 9f, new Vector2(0f, -62f));

        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(a);
        // Pasar el mouse encima también la selecciona (para que se ilumine).
        var trigger = rt.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(rt.gameObject); });
        trigger.triggers.Add(enter);
        Text tt = UiKit.Text(UiKit.Rect("T", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(-40f, 64f)),
            font, 46, Color.white, TextAnchor.MiddleCenter, titleText);
        var fx = tt.gameObject.AddComponent<PixelTextFx>();
        fx.top = Color.white; fx.bottom = accent; fx.depth = 3; fx.shineEvery = 3f; fx.shineOffset = pos.x;
        Text bt = UiKit.Text(UiKit.Rect("B", rt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(-50f, -200f)),
            font, 26, new Color(1f, 0.92f, 0.85f), TextAnchor.UpperCenter, body);
        bt.gameObject.AddComponent<PixelTextFx>().depth = 2;
        cards.Add(new Card { rt = rt, button = b, glow = glow, accent = accent });
        return b;
    }

    private void Update()
    {
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        float time = Time.unscaledTime;
        if (title != null) title.localScale = Vector3.one * (1f + Mathf.Sin(time * 2f) * 0.02f);
        foreach (Card c in cards)
        {
            bool on = sel == c.rt.gameObject;
            float target = on ? 1.06f + Mathf.Sin(time * 5f) * 0.015f : 0.96f;
            c.rt.localScale = Vector3.one * Mathf.Lerp(c.rt.localScale.x, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            c.rt.localRotation = Quaternion.Euler(0f, 0f, on ? Mathf.Sin(time * 3f) * 1f : 0f);
            c.glow.color = new Color(c.accent.r, c.accent.g, c.accent.b, on ? 0.35f + 0.2f * Mathf.Sin(time * 6f) : 0.08f);
        }
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / 0.8f;
            yield return null;
        }
        group.alpha = 1f;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    private void Choose(System.Action a)
    {
        IsOpen = false;
        Destroy(gameObject);
        a?.Invoke();
    }
}
