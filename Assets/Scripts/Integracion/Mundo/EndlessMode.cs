using UnityEngine;
using UnityEngine.UI;

// Modo infinito: aceptaste la oferta del Diablo y regresaste al infierno.
// Cada minuto sube un nivel: más demonios (y con más vida), más monedas y cofres nuevos.
public class EndlessMode : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static int Level { get; private set; }
    public static float Minutes => instance != null ? instance.elapsed / 60f : 0f;

    private static EndlessMode instance;
    private float elapsed;
    private float chestTimer = 20f;
    private Text label;
    private float pop;

    public static void Begin(Font font)
    {
        if (instance != null) return;
        Active = true;
        Level = 0;
        instance = new GameObject("ModoInfinito").AddComponent<EndlessMode>();
        instance.BuildHud(font);
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; Active = false; Level = 0; EnemyTracker.CoinChanceBonus = 0f; }
    }

    private void BuildHud(Font font)
    {
        Canvas canvas = UiKit.MakeCanvas("HUDInfinito", 5);
        canvas.transform.SetParent(transform, false);
        label = UiKit.Text(UiKit.Rect("Infinito", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -150f), new Vector2(760f, 40f)),
            font, 28, new Color(1f, 0.4f, 0.3f), TextAnchor.MiddleRight);
        label.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
    }

    private void Update()
    {
        if (TimeStop.Active || (PlayerStats.Instance != null && PlayerStats.Instance.RunEnded)) return;
        elapsed += Time.deltaTime;
        int lvl = Mathf.FloorToInt(elapsed / 60f);
        if (lvl != Level)
        {
            Level = lvl;
            pop = 1f;
            EnemyTracker.CoinChanceBonus = Mathf.Min(0.5f, 0.05f * Level);   // más monedas
        }

        // Cofres nuevos cada vez más seguido (de 45 s hasta 20 s).
        chestTimer -= Time.deltaTime;
        if (chestTimer <= 0f)
        {
            chestTimer = Mathf.Max(20f, 45f - Level * 3f);
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p != null) ChestSpawner.Instance?.SpawnNear(p.transform.position);
        }

        pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime * 1.5f);
        int t = Mathf.FloorToInt(elapsed);
        if (label != null)
        {
            label.text = "INFIERNO INFINITO  ·  NIVEL " + (Level + 1) + "  ·  " + (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
            label.rectTransform.localScale = Vector3.one * (1f + pop * 0.3f);
        }
    }
}
