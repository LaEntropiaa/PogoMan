using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla de victoria al vencer al Diablo Tendero: puntaje (kills) y resumen,
// y de ahí a los créditos o al menú.
public class VictoryScreen : MonoBehaviour
{
    public static bool IsShown { get; private set; }

    private Font font;
    private CanvasGroup group;
    private Button creditsButton;

    public static void Show(Font font)
    {
        if (IsShown)
            return;
        IsShown = true;
        var go = new GameObject("PantallaVictoria");
        var v = go.AddComponent<VictoryScreen>();
        v.font = font;
        v.Build();
    }

    private void OnDestroy() => IsShown = false;

    private void Build()
    {
        SceneNav.SetGameplayCursor(false);
        PauseMenu pause = FindAnyObjectByType<PauseMenu>();
        if (pause != null)
            pause.enabled = false;

        Canvas canvas = UiKit.MakeCanvas("Victoria", 30);
        canvas.transform.SetParent(transform, false);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        Transform root = canvas.transform;

        UiKit.Image(UiKit.Stretch("Fondo", root), new Color(0.06f, 0.01f, 0.03f, 0.88f));

        Text title = UiKit.Text(UiKit.Rect("Titulo", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -110), new Vector2(1700, 120)), font, 92, new Color(1f, 0.82f, 0.35f), TextAnchor.MiddleCenter, "¡DERROTASTE AL DIABLO!");
        title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4, -4);
        UiKit.Text(UiKit.Rect("Sub", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -215), new Vector2(1500, 50)), font, 36, new Color(1f, 0.7f, 0.6f), TextAnchor.MiddleCenter,
            "Pogoman recuperó su alma. El pogo se queda.");

        PlayerStats s = PlayerStats.Instance;
        int kills = s != null ? s.Kills : 0;
        UiKit.Text(UiKit.Rect("PuntosTxt", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 150), new Vector2(800, 50)), font, 40, new Color(0.9f, 0.8f, 0.7f), TextAnchor.MiddleCenter, "PUNTOS (KILLS)");
        Text score = UiKit.Text(UiKit.Rect("Puntos", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 75), new Vector2(800, 110)), font, 110, Color.white, TextAnchor.MiddleCenter, kills.ToString());
        score.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4, -4);

        if (s != null)
        {
            int t = Mathf.FloorToInt(s.RunTime);
            int items = ItemInventory.Instance != null ? ItemInventory.Instance.Items.Count : 0;
            UiKit.Text(UiKit.Rect("Resumen", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -70), new Vector2(1200, 200)), font, 34, new Color(0.95f, 0.88f, 0.8f), TextAnchor.MiddleCenter,
                "TIEMPO  " + (t / 60).ToString("00") + ":" + (t % 60).ToString("00") + "      NIVEL  " + s.Level + "\n" +
                "MONEDAS  " + s.CoinsCollected + "      DAÑO TOTAL  " + Mathf.RoundToInt(s.DamageDealt) + "\n" +
                "OBJETOS DISTINTOS  " + items);
        }

        creditsButton = MakeButton(root, "VER CRÉDITOS", new Vector2(-230, -260), () =>
        {
            MainMenuController.OpenCreditsOnLoad = true;
            SceneNav.ToMainMenu();
        });
        MakeButton(root, "MENÚ PRINCIPAL", new Vector2(230, -260), SceneNav.ToMainMenu);

        StartCoroutine(FadeIn());
    }

    private Button MakeButton(Transform root, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        RectTransform rt = UiKit.Rect(label, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(400, 80));
        Image img = UiKit.Image(rt, new Color(0.35f, 0.05f, 0.08f, 0.95f));
        img.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.75f, 0.5f);
        colors.selectedColor = new Color(1f, 0.75f, 0.5f);
        colors.pressedColor = new Color(0.8f, 0.5f, 0.3f);
        button.colors = colors;
        button.onClick.AddListener(action);
        UiKit.Text(UiKit.Stretch("Texto", rt), font, 36, Color.white, TextAnchor.MiddleCenter, label);
        return button;
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            group.alpha = t;
            yield return null;
        }
        group.alpha = 1f;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(creditsButton.gameObject);
    }
}
