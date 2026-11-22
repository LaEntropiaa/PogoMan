using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menú de pausa (Esc / Start), armado por código y compacto:
//  - ESTADÍSTICAS: cada stat con su valor base, cuánto la tienes aumentada y el total.
//  - OBJETOS: tabla con todos los objetos del juego (los que tienes, con su cantidad);
//    al pasar el mouse por uno se ve su descripción.
//  - OPCIONES: volumen, filtro pixel y pantalla completa.
public class PauseMenu : MonoBehaviour
{
    // Referencias de la ventana vieja de la escena (se ocultan; se conservan por compatibilidad).
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Text statsText;
    [SerializeField] private Text runText;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private GameOverScreen gameOver;

    [Header("Ventana nueva")]
    [SerializeField] private Font font;
    [SerializeField] private ItemDatabase database;

    public static bool IsPaused { get; private set; }

    private PogoPlayer player;
    private KeyManager keyManager;
    private bool paused;

    private CanvasGroup root;
    private Text runLine;
    private readonly List<(Button b, Image img, GameObject page)> tabs = new List<(Button, Image, GameObject)>();
    private int tab;
    private Text statsLeft, statsRight;
    private Image detailIcon;
    private Text detailName, detailRarity, detailBody;
    private readonly List<(ItemDefinition item, Image icon, Text count, Outline border)> cells = new List<(ItemDefinition, Image, Text, Outline)>();
    private readonly List<(Text value, System.Func<string> get)> optionValues = new List<(Text, System.Func<string>)>();

    private static readonly Color Panel = new Color(0.09f, 0.04f, 0.06f, 0.97f);
    private static readonly Color Gold = new Color(1f, 0.78f, 0.35f);
    private static readonly Color Soft = new Color(0.92f, 0.84f, 0.76f);

    private void Awake()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>();
        keyManager = FindAnyObjectByType<KeyManager>();
        if (font == null && statsText != null) font = statsText.font;
        if (database == null) database = FindDatabase();
        Build();
        SetVisible(false);
    }

    private static ItemDatabase FindDatabase()
    {
        ChestUI ui = FindAnyObjectByType<ChestUI>();
        return ui != null ? PogoBridge.Get<ItemDatabase>(ui, "database") : null;
    }

    private void OnDestroy()
    {
        if (paused) Time.timeScale = 1f;
        IsPaused = false;
    }

    private void Update()
    {
        bool pressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                       (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (pressed && !LevelUpUI.IsOpen && !MapScreen.IsOpen && !VictoryScreen.IsShown && !DevilOffer.IsOpen &&
            (gameOver == null || !gameOver.IsShown))
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
        IsPaused = true;
        Time.timeScale = 0f;
        SetVisible(true);
        SceneNav.SetGameplayCursor(false);
        SelectTab(tab);
    }

    public void Resume()
    {
        paused = false;
        IsPaused = false;
        // Si pausaste con un cofre o la pantalla de mejoras abiertos, el juego sigue detenido detrás.
        Time.timeScale = ChestUI.IsOpen || LevelUpUI.IsOpen ? 0f : 1f;
        SetVisible(false);
        SceneNav.SetGameplayCursor(true);
    }

    private void SetVisible(bool visible)
    {
        if (root == null) return;
        root.alpha = visible ? 1f : 0f;
        root.interactable = visible;
        root.blocksRaycasts = visible;
    }

    // ------------------------------------------------------------------ construcción
    private Text T(RectTransform rt, int size, Color c, TextAnchor a, string s = "") => UiKit.Text(rt, font, size, c, a, s);

    private void Build()
    {
        Canvas canvas = UiKit.MakeCanvas("MenuPausa", 25);   // en la raíz: la ventana vieja (alpha 0) no lo esconde
        root = canvas.gameObject.AddComponent<CanvasGroup>();
        root.ignoreParentGroups = true;
        Transform r = canvas.transform;
        UiKit.Image(UiKit.Stretch("Oscuro", r), new Color(0f, 0f, 0f, 0.7f));

        RectTransform panel = UiKit.Rect("Panel", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440f, 860f));
        UiKit.Image(panel, Panel);
        panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.55f, 0.32f, 0.12f);

        T(UiKit.Rect("Titulo", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(400f, 60f)), 52, Gold, TextAnchor.MiddleLeft, "PAUSA");
        runLine = T(UiKit.Rect("Partida", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -32f), new Vector2(980f, 44f)), 24, Soft, TextAnchor.MiddleRight);

        string[] names = { "ESTADÍSTICAS", "OBJETOS", "OPCIONES" };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            RectTransform b = UiKit.Rect(names[i], panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f + i * 290f, -100f), new Vector2(276f, 52f));
            Image img = UiKit.Image(b, new Color(0.2f, 0.08f, 0.1f));
            img.raycastTarget = true;
            var button = b.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => SelectTab(idx));
            T(UiKit.Stretch("Texto", b), 26, Color.white, TextAnchor.MiddleCenter, names[i]);
            RectTransform page = UiKit.Rect("Pagina" + i, panel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(-80f, -300f));
            tabs.Add((button, img, page.gameObject));
        }

        BuildStats(tabs[0].page.transform as RectTransform);
        BuildItems(tabs[1].page.transform as RectTransform);
        BuildOptions(tabs[2].page.transform as RectTransform);

        string[] actions = { "CONTINUAR", "REINICIAR", "MENÚ PRINCIPAL" };
        UnityEngine.Events.UnityAction[] calls = { Resume, SceneNav.RestartLevel, SceneNav.ToMainMenu };
        for (int i = 0; i < 3; i++)
        {
            RectTransform b = UiKit.Rect(actions[i], panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 330f, 26f), new Vector2(310f, 60f));
            Image img = UiKit.Image(b, i == 0 ? new Color(0.45f, 0.12f, 0.1f) : new Color(0.22f, 0.09f, 0.11f));
            img.raycastTarget = true;
            var button = b.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors; colors.highlightedColor = new Color(1f, 0.8f, 0.55f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            button.onClick.AddListener(calls[i]);
            T(UiKit.Stretch("Texto", b), 28, Color.white, TextAnchor.MiddleCenter, actions[i]);
        }
    }

    private void SelectTab(int i)
    {
        tab = i;
        for (int k = 0; k < tabs.Count; k++)
        {
            tabs[k].page.SetActive(k == i);
            tabs[k].img.color = k == i ? new Color(0.55f, 0.18f, 0.12f) : new Color(0.2f, 0.08f, 0.1f);
        }
        Refresh();
    }

    // ---- Estadísticas ----
    private void BuildStats(RectTransform page)
    {
        statsLeft = T(UiKit.Rect("Izq", page, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(10f, 0f), new Vector2(-20f, 0f)), 21, Soft, TextAnchor.UpperLeft);
        statsRight = T(UiKit.Rect("Der", page, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, 0f), new Vector2(-20f, 0f)), 21, Soft, TextAnchor.UpperLeft);
        statsLeft.horizontalOverflow = statsRight.horizontalOverflow = HorizontalWrapMode.Overflow;
        statsLeft.lineSpacing = statsRight.lineSpacing = 1.25f;
    }

    private static string Pad(string s, int w) => s.Length >= w ? s + " " : s + new string(' ', w - s.Length);

    private static string Row(string label, float baseV, float total, string fmt, string unit = "", bool lowerIsBetter = false)
    {
        float bonus = total - baseV;
        string b = Mathf.Abs(bonus) < 0.005f ? "<color=#7a6a64>—</color>"
            : "<color=" + ((bonus > 0f) != lowerIsBetter ? "#7CFC4A" : "#FF6A55") + ">" + (bonus > 0f ? "+" : "") + bonus.ToString(fmt) + unit + "</color>";
        string visible = StripTags(b);
        string padded = b + (visible.Length >= 9 ? " " : new string(' ', 9 - visible.Length));
        return Pad(label, 18) + Pad(baseV.ToString(fmt) + unit, 9) + padded + "<color=#FFD27A>" + total.ToString(fmt) + unit + "</color>";
    }

    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

    private string StatsText(bool left)
    {
        PlayerStats s = PlayerStats.Instance;
        ItemEffects fx = ItemEffects.Instance;
        if (s == null) return "";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<color=#FFC34D>" + Pad(left ? "POGO Y MOVIMIENTO" : "ESCUPITAJO", 18) + Pad("BASE", 9) + Pad("BONO", 9) + "TOTAL</color>");
        if (left)
        {
            float maxHp = PogoBridge.MaxHealth(player);
            sb.AppendLine(Row("VIDA MÁXIMA", 100f, maxHp, "0"));
            float pogoBase = 50f;
            sb.AppendLine(Row("DAÑO DEL POGO", pogoBase, fx != null ? fx.PogoDamage * (fx.Count(ItemEffects.Contrato) > 0 ? 2f : 1f) : pogoBase, "0"));
            sb.AppendLine(Row("RADIO PISOTÓN", 2.5f, 2.5f + 0.2f * Mathf.Min(fx != null ? fx.Count(ItemEffects.Plantilla) : 0, 3), "0.0", " m"));
            sb.AppendLine(Row("VELOCIDAD", 100f, s.Get(PogoStat.MoveSpeed) * 100f, "0", "%"));
            sb.AppendLine(Row("CONTROL AÉREO", 100f, (PogoJump.Instance != null ? PogoJump.Instance.AirControlMultiplier : 1f) * 100f, "0", "%"));
            sb.AppendLine(Row("SALTO CON ESPACIO", 100f, (PogoJump.Instance != null ? PogoJump.Instance.ChargedJumpHeightMultiplier : 1f) * 100f, "0", "%"));
            sb.AppendLine(Row("SALTOS EN EL AIRE", 0f, s.Get(PogoStat.AirJumps), "0"));
            sb.AppendLine(Row("DISTANCIA DASH", s.GetBase(PogoStat.DashDistance), s.Get(PogoStat.DashDistance), "0.0", " m"));
            sb.AppendLine(Row("RECARGA DASH", s.GetBase(PogoStat.DashCooldown), s.Get(PogoStat.DashCooldown), "0.0", " s", true));
            bool contrato = PlayerVitals.Instance != null && PlayerVitals.Instance.NoShieldNoRegen;
            if (contrato)
            {
                // El Contrato a Mano Pelona las anula: que se vea por qué están en cero.
                sb.AppendLine(Pad("ESCUDO", 18) + "<color=#FF6A55>0  (CONTRATO)</color>");
                sb.AppendLine(Pad("REGENERACIÓN", 18) + "<color=#FF6A55>0  (CONTRATO)</color>");
            }
            else
            {
                sb.AppendLine(Row("ESCUDO", 0f, PlayerVitals.Instance != null ? PlayerVitals.Instance.MaxShield : 0f, "0"));
                sb.AppendLine(Row("REGENERACIÓN", 0f, s.Get(PogoStat.Regeneration), "0.0", "/s"));
            }
            sb.AppendLine(Row("RECOGIDA", s.GetBase(PogoStat.PickupRadius), s.Get(PogoStat.PickupRadius), "0.0", " m"));
            sb.AppendLine(Row("EXPERIENCIA", 100f, s.Get(PogoStat.XpGain) * 100f, "0", "%"));
        }
        else
        {
            sb.AppendLine(Row("DAÑO", s.GetBase(PogoStat.SpitDamage), s.Get(PogoStat.SpitDamage), "0.0"));
            sb.AppendLine(Row("DISPAROS / SEG", s.GetBase(PogoStat.FireRate), s.Get(PogoStat.FireRate), "0.00"));
            sb.AppendLine(Row("PROB. CRÍTICO", s.GetBase(PogoStat.CritChance) * 100f, s.Get(PogoStat.CritChance) * 100f, "0", "%"));
            sb.AppendLine(Row("DAÑO CRÍTICO", s.GetBase(PogoStat.CritMultiplier), s.Get(PogoStat.CritMultiplier), "0.00", "x"));
            sb.AppendLine(Row("ALCANCE", s.GetBase(PogoStat.Range), s.Get(PogoStat.Range), "0", " m"));
            sb.AppendLine(Row("VEL. PROYECTIL", s.GetBase(PogoStat.ProjectileSpeed), s.Get(PogoStat.ProjectileSpeed), "0"));
            sb.AppendLine(Row("ÁREA", s.GetBase(PogoStat.SpitArea), s.Get(PogoStat.SpitArea), "0.0", " m"));
            sb.AppendLine(Row("TAMAÑO", 100f, s.Get(PogoStat.SpitSize) * 100f, "0", "%"));
            sb.AppendLine(Row("PROYECTILES", 1f, s.Get(PogoStat.Projectiles), "0"));
            sb.AppendLine();
            sb.AppendLine("<color=#FFC34D>PARTIDA</color>");
            sb.AppendLine(Pad("DAÑO TOTAL", 18) + Mathf.RoundToInt(s.DamageDealt));
            sb.AppendLine(Pad("GOLPE MÁS FUERTE", 18) + Mathf.RoundToInt(s.BiggestHit));
            sb.AppendLine(Pad("CRÍTICOS", 18) + s.CritHits);
            sb.AppendLine(Pad("ESCUPITAJOS", 18) + s.Shots);
        }
        return sb.ToString().TrimEnd();
    }

    // ---- Objetos ----
    private void BuildItems(RectTransform page)
    {
        RectTransform grid = UiKit.Rect("Tabla", page, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, 0f), new Vector2(860f, 0f));
        RectTransform detail = UiKit.Rect("Detalle", page, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, 0f), new Vector2(470f, 0f));
        UiKit.Image(detail, new Color(0.14f, 0.06f, 0.08f, 0.9f));
        detailIcon = UiKit.Image(UiKit.Rect("Icono", detail, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(150f, 150f)), Color.white);
        detailIcon.preserveAspect = true;
        detailName = T(UiKit.Rect("Nombre", detail, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(-30f, 70f)), 28, Gold, TextAnchor.UpperCenter);
        detailRarity = T(UiKit.Rect("Rareza", detail, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -244f), new Vector2(-30f, 32f)), 22, Color.white, TextAnchor.UpperCenter);
        detailBody = T(UiKit.Rect("Texto", detail, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -284f), new Vector2(-40f, -296f)), 22, Soft, TextAnchor.UpperLeft);
        detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailName.horizontalOverflow = HorizontalWrapMode.Wrap;

        if (database == null) return;
        var all = new List<ItemDefinition>(database.items);
        all.RemoveAll(x => x == null);
        all.Sort((a, b) => a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal));
        const int cols = 9;
        const float cell = 90f;
        for (int i = 0; i < all.Count; i++)
        {
            ItemDefinition item = all[i];
            RectTransform c = UiKit.Rect(item.id, grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2((i % cols) * (cell + 4f), -(i / cols) * (cell + 4f)), new Vector2(cell, cell));
            Image bg = UiKit.Image(c, new Color(0.16f, 0.07f, 0.09f));
            bg.raycastTarget = true;
            var border = c.gameObject.AddComponent<Outline>();
            border.effectColor = item.RarityColor;
            border.effectDistance = new Vector2(2, -2);
            Image icon = UiKit.Image(UiKit.Stretch("Icono", c, 8f), Color.white, item.icon);
            icon.preserveAspect = true;
            Text count = T(UiKit.Rect("Cantidad", c, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-3f, 1f), new Vector2(60f, 26f)), 22, Color.white, TextAnchor.LowerRight);
            count.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
            var trigger = c.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowDetail(item));
            trigger.triggers.Add(enter);
            cells.Add((item, icon, count, border));
        }
        if (all.Count > 0) ShowDetail(all[0]);
    }

    private void ShowDetail(ItemDefinition item)
    {
        int have = ItemInventory.Instance != null ? ItemInventory.Instance.Count(item) : 0;
        detailIcon.sprite = item.icon;
        detailIcon.color = have > 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        detailName.text = item.displayName.ToUpperInvariant();
        detailRarity.text = item.RarityName + (item.active ? " · ACTIVA" : "") + (item.cursed ? " · MALDITO" : "");
        detailRarity.color = item.RarityColor;
        detailBody.text = item.description + "\n\n<color=#a89080>“" + item.quote + "”</color>\n\n" +
            (have > 0 ? "<color=#7CFC4A>LO TIENES" + (have > 1 ? "  x" + have : "") + "</color>" : "<color=#8a7a70>AÚN NO LO TIENES</color>");
    }

    // ---- Opciones ----
    private void BuildOptions(RectTransform page)
    {
        AddOption(page, 0, "VOLUMEN GENERAL", () => Pct(GameSettings.MasterVolume), d => GameSettings.MasterVolume = Mathf.Clamp01(GameSettings.MasterVolume + d * 0.1f));
        AddOption(page, 1, "MÚSICA", () => Pct(GameSettings.MusicVolume), d => GameSettings.MusicVolume = Mathf.Clamp01(GameSettings.MusicVolume + d * 0.1f));
        AddOption(page, 2, "EFECTOS", () => Pct(GameSettings.SfxVolume), d => GameSettings.SfxVolume = Mathf.Clamp01(GameSettings.SfxVolume + d * 0.1f));
        AddOption(page, 3, "FILTRO PIXEL", () => GameSettings.PixelLevelNames[GameSettings.PixelSize - 1], d => GameSettings.PixelSize = Mathf.Clamp(GameSettings.PixelSize + d, 1, GameSettings.PixelLevelNames.Length));
        AddOption(page, 4, "SENSIBILIDAD", () => GameSettings.MouseSensitivity.ToString("0.0") + "x", d => GameSettings.MouseSensitivity += d * 0.1f);
        AddOption(page, 5, "PANTALLA COMPLETA", () => Screen.fullScreen ? "SÍ" : "NO", d =>
        {
            Vector2Int res = GameSettings.Resolution;
            GameSettings.SetDisplay(!Screen.fullScreen, res.x, res.y);
        });
        T(UiKit.Rect("Nota", page, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1200f, 36f)), 22, new Color(0.7f, 0.6f, 0.55f), TextAnchor.MiddleCenter,
            "TAB  MAPA      1-6  HABILIDADES ACTIVAS      E  COFRES / PILARES / PUERTA");
    }

    private static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

    private void AddOption(RectTransform page, int row, string label, System.Func<string> get, System.Action<int> change)
    {
        float y = -10f - row * 72f;
        T(UiKit.Rect(label, page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-60f, y), new Vector2(520f, 56f)), 30, Soft, TextAnchor.MiddleRight, label);
        Arrow(page, "<", new Vector2(10f, y), () => { change(-1); GameSettings.Save(); });
        Text value = T(UiKit.Rect("Valor", page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(80f, y), new Vector2(260f, 56f)), 30, Gold, TextAnchor.MiddleCenter);
        Arrow(page, ">", new Vector2(350f, y), () => { change(1); GameSettings.Save(); });
        optionValues.Add((value, get));
    }

    private void Arrow(RectTransform page, string s, Vector2 pos, UnityEngine.Events.UnityAction a)
    {
        RectTransform b = UiKit.Rect("Flecha" + s, page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), pos, new Vector2(60f, 56f));
        Image img = UiKit.Image(b, new Color(0.3f, 0.1f, 0.1f));
        img.raycastTarget = true;
        var button = b.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(a);
        T(UiKit.Stretch("T", b), 32, Color.white, TextAnchor.MiddleCenter, s);
    }

    // ------------------------------------------------------------------ refresco
    private void Refresh()
    {
        PlayerStats s = PlayerStats.Instance;
        if (s != null && runLine != null)
        {
            int t = Mathf.FloorToInt(s.RunTime);
            int keys = keyManager != null ? keyManager.CollectedKeys : 0;
            runLine.text = "NV " + s.Level + "   ·   " + (t / 60).ToString("00") + ":" + (t % 60).ToString("00") +
                (EndlessMode.Active ? "   ·   <color=#FF6A55>INFINITO</color>" : "   ·   LLAVES " + keys + "/4") +
                "   ·   KILLS " + s.Kills + "   ·   MONEDAS " + s.Coins;
        }
        if (tab == 0 && statsLeft != null)
        {
            statsLeft.text = StatsText(true);
            statsRight.text = StatsText(false);
        }
        else if (tab == 1)
        {
            foreach (var c in cells)
            {
                int have = ItemInventory.Instance != null ? ItemInventory.Instance.Count(c.item) : 0;
                c.icon.color = have > 0 ? Color.white : new Color(0.18f, 0.12f, 0.14f, 0.8f);   // los que no tienes, en silueta
                c.count.text = have > 1 ? "x" + have : "";
                c.border.effectDistance = have > 0 ? new Vector2(3, -3) : new Vector2(1, -1);
            }
        }
        else if (tab == 2)
        {
            foreach (var o in optionValues) o.value.text = o.get();
        }
    }
}
