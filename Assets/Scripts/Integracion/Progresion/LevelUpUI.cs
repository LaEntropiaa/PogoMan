using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pantalla de subida de nivel: pausa el juego y ofrece 3 mejoras al azar.
// Se elige con el mouse, con 1/2/3, con A/D + Enter/Espacio o con el control.
public class LevelUpUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [SerializeField] private Font font;
    [SerializeField] private Sprite cardSprite;
    [SerializeField] private AudioClip levelUpClip;
    [SerializeField] private AudioClip selectClip;
    [SerializeField] private AudioClip moveClip;
    [Tooltip("Tiempo sin aceptar teclas al abrir (para no elegir sin querer mientras saltas).")]
    [SerializeField] private float inputDelay = 0.45f;

    private class Card
    {
        public RectTransform root;
        public Image frame;
        public Image glow;
        public Text rarity, title, stat, values, taken, key;
    }

    private readonly List<Card> cards = new List<Card>();
    private List<Upgrades.Offer> offers = new List<Upgrades.Offer>();
    private CanvasGroup group;
    private Text header;
    private Text levelText;
    private AudioSource audioSource;
    private PogoPlayer player;
    private GameOverScreen gameOver;
    private int selected;
    private float openTime;
    private float choiceFlash;
    private bool choosing;

    private void Awake()
    {
        Upgrades.ResetRun();
        audioSource = UiKit.UiAudio(gameObject);
        Build();
        Hide();
    }

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>();
        gameOver = FindAnyObjectByType<GameOverScreen>(FindObjectsInactive.Include);
    }

    private void OnDestroy()
    {
        if (IsOpen)
        {
            IsOpen = false;
            Time.timeScale = 1f;
        }
    }

    private void Build()
    {
        Canvas canvas = UiKit.MakeCanvas("PantallaNivel", 60);
        canvas.transform.SetParent(transform, false);
        group = canvas.gameObject.AddComponent<CanvasGroup>();

        UiKit.Image(UiKit.Stretch("Fondo", canvas.transform), new Color(0.03f, 0f, 0.05f, 0.78f));

        header = UiKit.Text(UiKit.Rect("Titulo", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1400, 110)), font, 84, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, "¡SUBISTE DE NIVEL!");
        levelText = UiKit.Text(UiKit.Rect("Nivel", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -215), new Vector2(1000, 60)), font, 40, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleCenter);

        for (int i = 0; i < 3; i++)
        {
            var c = new Card();
            c.root = UiKit.Rect("Carta" + (i + 1), canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 470, -40), new Vector2(420, 560));
            c.glow = UiKit.Image(UiKit.Stretch("Brillo", c.root, -14f), Color.clear, cardSprite, true);
            c.frame = UiKit.Image(UiKit.Stretch("Marco", c.root), Color.white, cardSprite, true);
            UiKit.Image(UiKit.Stretch("Interior", c.root, 10f), new Color(0.09f, 0.05f, 0.1f, 0.96f));

            c.rarity = UiKit.Text(UiKit.Rect("Rareza", c.root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
                new Vector2(0, -28), new Vector2(-40, 44)), font, 30, Color.white, TextAnchor.MiddleCenter);
            c.title = UiKit.Text(UiKit.Rect("Nombre", c.root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
                new Vector2(0, -90), new Vector2(-50, 150)), font, 44, Color.white, TextAnchor.UpperCenter);
            c.stat = UiKit.Text(UiKit.Rect("Stat", c.root, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -40), new Vector2(-40, 50)), font, 32, new Color(0.75f, 0.7f, 0.65f), TextAnchor.MiddleCenter);
            c.values = UiKit.Text(UiKit.Rect("Valores", c.root, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -100), new Vector2(-20, 60)), font, 38, new Color(0.5f, 1f, 0.55f), TextAnchor.MiddleCenter);
            c.values.horizontalOverflow = HorizontalWrapMode.Overflow;
            c.taken = UiKit.Text(UiKit.Rect("Veces", c.root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f),
                new Vector2(0, 80), new Vector2(-40, 40)), font, 26, new Color(0.7f, 0.65f, 0.6f), TextAnchor.MiddleCenter);
            c.key = UiKit.Text(UiKit.Rect("Tecla", c.root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f),
                new Vector2(0, 28), new Vector2(-40, 40)), font, 28, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter, "[" + (i + 1) + "]");
            cards.Add(c);
        }

        UiKit.Text(UiKit.Rect("Ayuda", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 60), new Vector2(1600, 50)), font, 28, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter,
            "Clic, 1 / 2 / 3, o A / D + Enter para elegir");
    }

    private void Hide()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    private void Update()
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null)
            return;

        if (!IsOpen)
        {
            bool canOpen = stats.PendingLevelUps > 0 && Time.timeScale > 0f && !ChestUI.IsOpen &&
                           !(player != null && PogoBridge.IsDead(player)) && !(gameOver != null && gameOver.IsShown);
            if (canOpen)
                Open(stats);
            return;
        }

        Animate();

        if (Time.unscaledTime - openTime < inputDelay || choosing)
            return;

        HandleInput(stats);
    }

    private void Open(PlayerStats stats)
    {
        offers = Upgrades.Roll(stats, 3);
        if (offers.Count == 0)
        {
            stats.ConsumeLevelUp();
            return;
        }

        IsOpen = true;
        Time.timeScale = 0f;
        openTime = Time.unscaledTime;
        selected = 1 % offers.Count;
        group.alpha = 1f;
        group.blocksRaycasts = true;
        SceneNav.SetGameplayCursor(false);

        levelText.text = "Nivel " + stats.Level + (stats.PendingLevelUps > 1 ? "   (+" + (stats.PendingLevelUps - 1) + " más)" : "");

        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            bool used = i < offers.Count;
            c.root.gameObject.SetActive(used);
            if (!used)
                continue;
            Fill(c, offers[i], stats);
        }

        if (levelUpClip != null)
            audioSource.PlayOneShot(levelUpClip, 0.8f * GameSettings.SfxVolume);
    }

    private void Fill(Card c, Upgrades.Offer offer, PlayerStats stats)
    {
        UpgradeDef d = offer.Def;
        Color color = Upgrades.RarityColor[(int)offer.Rarity];
        c.frame.color = color;
        c.rarity.text = Upgrades.RarityName[(int)offer.Rarity];
        c.rarity.color = color;
        c.title.text = d.Name;
        c.stat.text = d.Stat;

        float now = stats.Get(d.Target);
        float next = stats.Preview(d.Target, offer.Flat, offer.Percent);
        if (d.Target == PogoStat.MaxHealth && player != null)
        {
            // La vida se muestra completa (base de Pogoman + extra).
            now = PogoBridge.MaxHealth(player);
            next = now + offer.Flat;
        }
        c.values.text = Upgrades.FormatValue(d, now) + " → " + Upgrades.FormatValue(d, next);

        Upgrades.Taken.TryGetValue(d, out int n);
        c.taken.text = n > 0 ? "Ya la tienes x" + n : "¡Nueva!";
    }

    private void Animate()
    {
        float t = Time.unscaledTime - openTime;
        header.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.3f - t) * 1.2f + Mathf.Sin(Time.unscaledTime * 3f) * 0.015f);
        choiceFlash = Mathf.MoveTowards(choiceFlash, 0f, Time.unscaledDeltaTime * 4f);

        for (int i = 0; i < cards.Count && i < offers.Count; i++)
        {
            Card c = cards[i];
            float appear = Mathf.Clamp01((t - i * 0.08f) / 0.25f);
            float ease = 1f - (1f - appear) * (1f - appear);
            bool on = i == selected;
            float scale = Mathf.Lerp(0.6f, 1f, ease) * (on ? 1.06f + choiceFlash * 0.1f : 0.97f);
            c.root.localScale = Vector3.one * scale;
            c.root.anchoredPosition = new Vector2((i - 1) * 470, -40 + (1f - ease) * -120f + (on ? 14f : 0f));

            Color glow = Upgrades.RarityColor[(int)offers[i].Rarity];
            glow.a = on ? 0.45f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            c.glow.color = glow;
        }
    }

    private void HandleInput(PlayerStats stats)
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        Mouse mouse = Mouse.current;
        int previous = selected;

        // Mouse: pasar por encima selecciona, clic elige.
        if (mouse != null)
        {
            Vector2 pos = mouse.position.ReadValue();
            for (int i = 0; i < offers.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(cards[i].root, pos, null))
                {
                    if (mouse.delta.ReadValue().sqrMagnitude > 0.5f)
                        selected = i;
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        Choose(stats, i);
                        return;
                    }
                }
            }
        }

        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) { Choose(stats, 0); return; }
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) { Choose(stats, 1); return; }
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) { Choose(stats, 2); return; }
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) selected--;
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) selected++;
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) { Choose(stats, selected); return; }
        }

        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) selected--;
            if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) selected++;
            if (pad.buttonSouth.wasPressedThisFrame) { Choose(stats, selected); return; }
        }

        selected = Mathf.Clamp(selected, 0, offers.Count - 1);
        if (selected != previous && moveClip != null)
            audioSource.PlayOneShot(moveClip, 0.5f * GameSettings.SfxVolume);
    }

    private void Choose(PlayerStats stats, int index)
    {
        if (index < 0 || index >= offers.Count)
            return;

        selected = index;
        choiceFlash = 1f;
        Upgrades.Apply(stats, offers[index]);
        stats.ConsumeLevelUp();

        if (selectClip != null)
            audioSource.PlayOneShot(selectClip, 0.8f * GameSettings.SfxVolume);

        choosing = true;
        StartCoroutine(CloseSoon(stats));
    }

    private System.Collections.IEnumerator CloseSoon(PlayerStats stats)
    {
        yield return new WaitForSecondsRealtime(0.25f);
        choosing = false;

        if (stats.PendingLevelUps > 0)
        {
            IsOpen = false;
            Open(stats);   // otro nivel pendiente: nuevas cartas
            yield break;
        }

        IsOpen = false;
        Hide();
        Time.timeScale = 1f;
        SceneNav.SetGameplayCursor(true);
    }
}
