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

    private void Build(Font font, ItemDefinition preview)
    {
        SceneNav.SetGameplayCursor(false);
        Canvas canvas = UiKit.MakeCanvas("Oferta", 28);
        canvas.transform.SetParent(transform, false);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        Transform r = canvas.transform;
        UiKit.Image(UiKit.Stretch("Fondo", r), new Color(0.05f, 0f, 0.02f, 0.75f));

        var title = UiKit.Text(UiKit.Rect("Titulo", r, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1600f, 90f)),
            font, 70, new Color(1f, 0.78f, 0.35f), TextAnchor.MiddleCenter, "EL DIABLO TE OFRECE UN TRATO");
        title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4, -4);
        UiKit.Text(UiKit.Rect("Dialogo", r, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(1500f, 90f)),
            font, 30, new Color(1f, 0.85f, 0.8f), TextAnchor.MiddleCenter,
            "\"Ya, ya, me ganaste. Perdóname la vida y te doy algo bonito para tu pogo...\n...nomás que te quedas aquí abajo. Para siempre.\"");

        first = Option(r, font, new Vector2(-380f, -40f), "ACABAR CON ÉL", "Termina la partida.\nEl infierno se queda sin tendero.",
            new Color(0.5f, 0.08f, 0.08f), () => Choose(onKill));
        Option(r, font, new Vector2(380f, -40f), "ACEPTAR SU OFERTA", "Un objeto LEGENDARIO al azar\ny de vuelta al infierno: modo infinito.\nCada minuto, más demonios, monedas y cofres.",
            new Color(0.42f, 0.28f, 0.05f), () => Choose(onDeal));
        StartCoroutine(FadeIn());
    }

    private Button Option(Transform r, Font font, Vector2 pos, string title, string body, Color color, UnityEngine.Events.UnityAction a)
    {
        RectTransform rt = UiKit.Rect(title, r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(660f, 330f));
        Image img = UiKit.Image(rt, color);
        img.raycastTarget = true;
        rt.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.75f, 0.35f);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var c = b.colors; c.highlightedColor = new Color(1.3f, 1.2f, 1f); c.selectedColor = c.highlightedColor; b.colors = c;
        b.onClick.AddListener(a);
        UiKit.Text(UiKit.Rect("T", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(-40f, 70f)),
            font, 48, Color.white, TextAnchor.MiddleCenter, title);
        UiKit.Text(UiKit.Rect("B", rt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(-50f, -150f)),
            font, 28, new Color(1f, 0.9f, 0.8f), TextAnchor.UpperCenter, body);
        return b;
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
