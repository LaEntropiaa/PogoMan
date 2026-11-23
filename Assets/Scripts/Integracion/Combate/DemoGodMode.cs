using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Modo dios para la DEMO FINAL GOD MODE (sirve para enseñar todo el juego sin morir).
// Solo existe en el build compilado con el símbolo POGO_GODMODE (Pogo Man > Compilar DEMO FINAL GOD MODE).
// Da 999,999 monedas, inmortalidad y 10 ondas del pogo de 50 de daño desde el inicio.
// Las partidas en modo dios no se guardan en los récords.
public class DemoGodMode : MonoBehaviour
{
#if POGO_GODMODE
    public const bool Enabled = true;
#else
    public const bool Enabled = false;
#endif

    public const int StartCoins = 999999;
    public const int Waves = 10;
    public const float WaveDamage = 50f;

    public static bool Active => Enabled && instance != null;
    private static DemoGodMode instance;
    private PogoPlayer player;

    private void Awake()
    {
        instance = this;
        player = GetComponent<PogoPlayer>();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private IEnumerator Start()
    {
        while (PlayerStats.Instance == null)
            yield return null;
        yield return null;   // deja que los demás sistemas terminen su Start

        PlayerStats s = PlayerStats.Instance;
        s.AddCoins(Mathf.Max(0, StartCoins - s.Coins));
        s.AddFlat(PogoStat.WaveCount, Waves - s.Get(PogoStat.WaveCount));
        s.AddFlat(PogoStat.WaveDamage, WaveDamage - s.Get(PogoStat.WaveDamage));
        ShowLabel();
    }

    private void LateUpdate()
    {
        // Por si algo le baja la vida sin pasar por PlayerVitals: siempre al máximo.
        if (player != null && !PogoBridge.IsDead(player))
        {
            float max = PogoBridge.MaxHealth(player);
            if (PogoBridge.Health(player) < max)
                PogoBridge.SetFloat(player, "currentHealth", max);
        }
    }

    private void ShowLabel()
    {
        var go = new GameObject("GodModeLabel", typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var t = new GameObject("Texto", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(go.transform, false);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = "GOD MODE";
        t.fontSize = 22;
        t.fontStyle = FontStyle.Bold;
        t.color = new Color(1f, 0.78f, 0.25f, 0.85f);
        t.alignment = TextAnchor.LowerCenter;
        t.raycastTarget = false;
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0.5f, 0f);
        r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0f, 8f);
        r.sizeDelta = new Vector2(400f, 40f);
    }
}
