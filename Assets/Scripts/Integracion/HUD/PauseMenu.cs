using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menú de pausa (Esc / Start): stats de Pogoman comparadas con el inicio de la
// partida, para ver cómo van creciendo con los objetos, y datos de la partida.
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Text statsText;
    [SerializeField] private Text runText;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private GameOverScreen gameOver;

    private PogoPlayer player;
    private KeyManager keyManager;
    private bool paused;

    private struct StatLine
    {
        public string label;
        public float value;
        public string format;   // "{0}" con el valor ya formateado
        public bool percent;
        public string unit;
    }

    private readonly Dictionary<string, float> startValues = new Dictionary<string, float>();

    private void Awake()
    {
        SetVisible(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (menuButton != null) menuButton.onClick.AddListener(SceneNav.ToMainMenu);
    }

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>();
        keyManager = FindAnyObjectByType<KeyManager>();

        foreach (StatLine line in CollectStats())
            startValues[line.label] = line.value;
    }

    private void OnDestroy()
    {
        if (paused)
            Time.timeScale = 1f;
    }

    private void Update()
    {
        bool pressed =
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        if (pressed && !LevelUpUI.IsOpen && (gameOver == null || !gameOver.IsShown))
        {
            if (paused) Resume();
            else Pause();
        }

        if (paused)
            Refresh();
    }

    public void Pause()
    {
        paused = true;
        Time.timeScale = 0f;
        SetVisible(true);
        SceneNav.SetGameplayCursor(false);
        Refresh();

        if (resumeButton != null && EventSystem.current != null)
        {
            MenuButtonFx.MuteSelectionSound = true;
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            MenuButtonFx.MuteSelectionSound = false;
        }
    }

    public void Resume()
    {
        paused = false;
        Time.timeScale = 1f;
        SetVisible(false);
        SceneNav.SetGameplayCursor(true);
    }

    private void SetVisible(bool visible)
    {
        if (group == null)
            return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private List<StatLine> CollectStats()
    {
        PlayerStats s = PlayerStats.Instance;
        var list = new List<StatLine>
        {
            new StatLine { label = "VIDA MÁX", value = PogoBridge.MaxHealth(player), format = "0" },
            new StatLine { label = "DAÑO DE CAÍDA", value = PogoBridge.GetFloat(player, "attackDamage"), format = "0" },
            new StatLine { label = "RADIO DE CAÍDA", value = PogoBridge.GetFloat(player, "attackRadius"), format = "0.0", unit = "M" },
            new StatLine { label = "VELOCIDAD", value = PogoBridge.GetFloat(player, "maxAirSpeed"), format = "0.0" },
        };

        if (s != null)
        {
            list.Add(new StatLine { label = "DAÑO ESCUPITAJO", value = s.Get(PogoStat.SpitDamage), format = "0" });
            list.Add(new StatLine { label = "DISPAROS / SEG", value = s.Get(PogoStat.FireRate), format = "0.00" });
            list.Add(new StatLine { label = "PROB. CRÍTICO", value = s.Get(PogoStat.CritChance), format = "0", percent = true });
            list.Add(new StatLine { label = "DAÑO CRÍTICO", value = s.Get(PogoStat.CritMultiplier), format = "0.0", unit = "X" });
            list.Add(new StatLine { label = "ALCANCE", value = s.Get(PogoStat.Range), format = "0", unit = "M" });
            list.Add(new StatLine { label = "PROYECTILES", value = s.Get(PogoStat.Projectiles), format = "0" });
            list.Add(new StatLine { label = "ÁREA", value = s.Get(PogoStat.SpitArea), format = "0.0", unit = "M" });
            list.Add(new StatLine { label = "SALTOS EN AIRE", value = s.Get(PogoStat.AirJumps), format = "0" });
            list.Add(new StatLine { label = "DASH", value = s.Get(PogoStat.DashDistance), format = "0.0", unit = "M" });
            list.Add(new StatLine { label = "RECOGIDA", value = s.Get(PogoStat.PickupRadius), format = "0.0", unit = "M" });
            list.Add(new StatLine { label = "REGENERACIÓN", value = s.Get(PogoStat.Regeneration), format = "0.0" });
            list.Add(new StatLine { label = "ESCUDO", value = s.Get(PogoStat.Shield), format = "0" });
        }

        return list;
    }

    private static string Format(StatLine line, float value)
    {
        string v = line.percent ? (value * 100f).ToString(line.format) + "%" : value.ToString(line.format);
        return line.unit == "X" ? "X" + v : line.unit == "M" ? v + " M" : v;
    }

    private void Refresh()
    {
        if (statsText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=#ffc53a>" + Pad("STAT", 17) + Pad("INICIO", 9) + Pad("AHORA", 9) + "CAMBIO</color>");
            sb.AppendLine();

            foreach (StatLine line in CollectStats())
            {
                startValues.TryGetValue(line.label, out float start);
                string delta;
                if (Mathf.Approximately(start, line.value))
                    delta = "<color=#8a7a70>=</color>";
                else
                {
                    float pct = start != 0f ? (line.value - start) / Mathf.Abs(start) * 100f : 100f;
                    string sign = pct > 0 ? "+" : "";
                    string color = pct > 0 ? "#7cfc4a" : "#ff5a4a";
                    delta = $"<color={color}>{sign}{pct:0}%</color>";
                }

                sb.AppendLine(Pad(line.label, 17) + Pad(Format(line, start), 9) + Pad(Format(line, line.value), 9) + delta);
            }

            statsText.text = sb.ToString().TrimEnd();
        }

        PlayerStats s = PlayerStats.Instance;
        if (runText != null && s != null)
        {
            int t = Mathf.FloorToInt(s.RunTime);
            int keys = keyManager != null ? keyManager.CollectedKeys : 0;
            int required = keyManager != null ? PogoBridge.GetInt(keyManager, "requiredKeys", 4) : 4;

            runText.text =
                "<color=#ffc53a>PARTIDA</color>\n\n" +
                Pad("NIVEL", 18) + s.Level + "\n" +
                Pad("TIEMPO", 18) + (t / 60).ToString("00") + ":" + (t % 60).ToString("00") + "\n" +
                Pad("LLAVES", 18) + keys + " / " + required + "\n" +
                Pad("ENEMIGOS", 18) + s.Kills + "\n" +
                Pad("MONEDAS", 18) + s.Coins + "\n" +
                Pad("DAÑO TOTAL", 18) + Mathf.RoundToInt(s.DamageDealt) + "\n" +
                Pad("CRÍTICOS", 18) + s.CritHits + "\n" +
                Pad("GOLPE MÁS FUERTE", 18) + Mathf.RoundToInt(s.BiggestHit) + "\n" +
                Pad("ESCUPITAJOS", 18) + s.Shots;
        }
    }

    // La fuente pixel es monoespaciada, así que rellenar con espacios alinea columnas.
    private static string Pad(string text, int width) =>
        text.Length >= width ? text + " " : text + new string(' ', width - text.Length);
}
