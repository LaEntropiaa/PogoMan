using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Entrada del menú: pantalla negra -> suena la música -> el logo aparece pequeño
// -> en el momento épico de la canción crece, rebota y sube -> se revela el
// escenario -> aparecen los botones.
//
// Todo se calcula a partir de "t" = segundos relativos al impacto del logo
// (t < 0 antes del golpe, t = 0 el golpe). Así, cambiar el momento musical
// mueve toda la secuencia junta.
public class MenuIntroSequence : MonoBehaviour
{
    // Ponlo en true antes de cargar el menú para saltarte la entrada
    // (por ejemplo, al volver desde el juego).
    public static bool SkipNextIntro;

    [Header("General")]
    [SerializeField] private bool playIntroSequence = true;
    [SerializeField] private bool allowSkip = true;

    [Header("Sincronización musical")]
    [Tooltip("Segundo de la canción desde el que empieza a sonar en el menú.")]
    [SerializeField] private float musicStartOffsetSeconds = 80f;
    [Tooltip("Segundo de la canción en el que el logo impacta.")]
    [SerializeField] private float logoImpactMusicTimeSeconds = 85.64f;
    [Tooltip("Tiempo en negro antes de que empiece la música.")]
    [SerializeField] private float initialBlackSeconds = 0.6f;
    [Tooltip("Si no hay música asignada, segundos de espera hasta el impacto.")]
    [SerializeField] private float fallbackLeadInSeconds = 3f;

    [Header("Logo")]
    [SerializeField] private RectTransform logo;
    [SerializeField] private CanvasGroup logoGroup;
    [Tooltip("Segundos después de que empieza la música para que aparezca el logo.")]
    [SerializeField] private float logoAppearDelay = 1.6f;
    [SerializeField] private float logoFadeInDuration = 0.9f;
    [SerializeField] private float logoInitialScale = 0.28f;
    [Tooltip("Escala que alcanza creciendo despacio antes del golpe.")]
    [SerializeField] private float logoAnticipationScale = 0.38f;
    [Tooltip("Duración del crecimiento rápido que termina justo en el impacto.")]
    [SerializeField] private float logoAnimationDuration = 0.24f;
    [SerializeField] private float logoOvershootScale = 1.2f;
    [SerializeField] private float logoSettleDuration = 0.6f;
    [SerializeField] private float logoHoldAfterImpact = 0.45f;
    [SerializeField] private float logoMoveDuration = 0.9f;
    [SerializeField] private Vector2 logoTopPosition = new Vector2(0f, 345f);
    [SerializeField] private float logoTopScale = 0.6f;

    [Header("Impacto")]
    [SerializeField] private Image flash;
    [SerializeField] private float flashDuration = 0.4f;
    [SerializeField] private float flashMaxAlpha = 0.85f;
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] private float shakeAmplitude = 16f;
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private RectTransform sparksRoot;
    [SerializeField] private Sprite sparkSprite;
    [SerializeField] private int sparkCount = 40;
    [SerializeField] private bool playImpactSfx = false;

    [Header("Escenario")]
    [SerializeField] private CanvasGroup blackOverlay;
    [Tooltip("Segundos después del impacto en que empieza a verse el escenario.")]
    [SerializeField] private float backgroundRevealDelay = 0.1f;
    [SerializeField] private float backgroundRevealDuration = 1.4f;

    [Header("Botones")]
    [Tooltip("Segundos después de que el logo llega arriba.")]
    [SerializeField] private float buttonsRevealDelay = 0.05f;
    [SerializeField] private float buttonStagger = 0.09f;
    [SerializeField] private float buttonRevealDuration = 0.35f;
    [SerializeField] private float buttonSlideDistance = 28f;

    [Header("Aviso para saltar")]
    [SerializeField] private CanvasGroup skipHint;

    public event Action Finished;
    public bool IsFinished { get; private set; }

    private readonly List<RectTransform> buttons = new List<RectTransform>();
    private readonly List<CanvasGroup> buttonGroups = new List<CanvasGroup>();

    private float clockStart;
    private float impactClock;
    private bool impactDone;
    private bool musicScheduled;
    private Vector2 shakeBase;

    private struct Spark
    {
        public RectTransform rect;
        public Image image;
        public Vector2 position;
        public Vector2 velocity;
        public float life;
        public float maxLife;
    }

    private Spark[] sparks;

    private float LeadIn
    {
        get
        {
            bool hasMusic = MenuAudio.Instance != null && MenuAudio.Instance.HasMusic;
            float lead = hasMusic
                ? logoImpactMusicTimeSeconds - musicStartOffsetSeconds
                : fallbackLeadInSeconds;
            return Mathf.Max(lead, logoAnimationDuration + 0.2f);
        }
    }

    private float MoveStart => logoSettleDuration + logoHoldAfterImpact;
    private float ButtonsStart => MoveStart + logoMoveDuration + buttonsRevealDelay;
    private float EndTime => ButtonsStart + Mathf.Max(0, buttons.Count - 1) * buttonStagger + buttonRevealDuration;

    // El controlador del menú nos dice qué botones están visibles (en orden).
    public void SetButtons(IEnumerable<RectTransform> visibleButtons)
    {
        buttons.Clear();
        buttonGroups.Clear();

        foreach (RectTransform b in visibleButtons)
        {
            buttons.Add(b);
            CanvasGroup g = b.GetComponent<CanvasGroup>();
            if (g == null)
                g = b.gameObject.AddComponent<CanvasGroup>();
            buttonGroups.Add(g);
        }
    }

    private void Start()
    {
        if (shakeTarget != null)
            shakeBase = shakeTarget.anchoredPosition;

        CreateSparks();

        clockStart = Time.unscaledTime;
        impactClock = initialBlackSeconds + LeadIn;

        // La música arranca al terminar el negro inicial, ya en la marca elegida.
        if (MenuAudio.Instance != null)
            musicScheduled = MenuAudio.Instance.ScheduleMusic(
                AudioSettings.dspTime + initialBlackSeconds,
                musicStartOffsetSeconds);

        bool skip = !playIntroSequence || SkipNextIntro;
        SkipNextIntro = false;

        if (skip)
            JumpToEnd();
        else
            Evaluate(Clock - impactClock);
    }

    private float Clock => Time.unscaledTime - clockStart;

    private void Update()
    {
        if (IsFinished)
            return;

        if (allowSkip && SkipPressed())
        {
            JumpToEnd();
            return;
        }

        ResyncWithMusic();
        Evaluate(Clock - impactClock);
        UpdateSparks();
    }

    // Si el audio y el reloj se separan (tirón de frames, etc.), mandamos a la música.
    private void ResyncWithMusic()
    {
        if (!musicScheduled || impactDone || Clock < initialBlackSeconds + 0.15f)
            return;

        MenuAudio audio = MenuAudio.Instance;
        if (audio == null || !audio.IsMusicPlaying)
            return;

        float songTime = audio.MusicTime;
        float tFromMusic = songTime - logoImpactMusicTimeSeconds;
        float tFromClock = Clock - impactClock;

        if (Mathf.Abs(tFromMusic - tFromClock) > 0.05f)
            impactClock = Clock - tFromMusic;
    }

    private void JumpToEnd()
    {
        impactDone = true;
        impactClock = Clock - EndTime - 1f;

        if (sparks != null)
            for (int i = 0; i < sparks.Length; i++)
                sparks[i].rect.gameObject.SetActive(false);

        Evaluate(EndTime + 1f);
    }

    private void Evaluate(float t)
    {
        float lead = LeadIn;

        // ---------- Logo ----------
        float appear = -lead + logoAppearDelay;
        float growStart = -logoAnimationDuration;
        float scale;

        if (t < growStart)
        {
            float x = Mathf.Clamp01((t - appear) / Mathf.Max(0.01f, growStart - appear));
            scale = Mathf.Lerp(logoInitialScale, logoAnticipationScale, x * x * (3f - 2f * x));
        }
        else if (t < 0f)
        {
            float x = (t - growStart) / logoAnimationDuration;
            scale = Mathf.Lerp(logoAnticipationScale, logoOvershootScale, x * x * x);
        }
        else
        {
            float x = t / logoSettleDuration;
            scale = x < 1f
                ? 1f + (logoOvershootScale - 1f) * Mathf.Exp(-5f * x) * Mathf.Cos(x * Mathf.PI * 2.5f)
                : 1f;
        }

        float m = EaseInOutCubic(Mathf.Clamp01((t - MoveStart) / logoMoveDuration));

        if (logo != null)
        {
            logo.anchoredPosition = Vector2.Lerp(Vector2.zero, logoTopPosition, m);
            logo.localScale = Vector3.one * scale * Mathf.Lerp(1f, logoTopScale, m);
        }

        if (logoGroup != null)
            logoGroup.alpha = Mathf.Clamp01((t - appear) / logoFadeInDuration);

        // ---------- Impacto ----------
        if (!impactDone && t >= 0f)
        {
            impactDone = true;
            FireSparks();

            if (playImpactSfx)
                MenuAudio.Instance?.PlayImpact();
        }

        if (flash != null)
        {
            float f = t >= 0f && t < flashDuration ? 1f - t / flashDuration : 0f;
            Color c = flash.color;
            c.a = f * f * flashMaxAlpha;
            flash.color = c;
            flash.enabled = c.a > 0.001f;
        }

        if (shakeTarget != null)
        {
            float s = t >= 0f && t < shakeDuration ? 1f - t / shakeDuration : 0f;
            Vector2 offset = s > 0f ? UnityEngine.Random.insideUnitCircle * shakeAmplitude * s * s : Vector2.zero;
            shakeTarget.anchoredPosition = shakeBase + new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));
        }

        // ---------- Escenario ----------
        if (blackOverlay != null)
        {
            float r = Mathf.Clamp01((t - backgroundRevealDelay) / backgroundRevealDuration);
            blackOverlay.alpha = 1f - r * r * (3f - 2f * r);
            blackOverlay.blocksRaycasts = blackOverlay.alpha > 0.5f;
        }

        // ---------- Botones ----------
        for (int i = 0; i < buttons.Count; i++)
        {
            float x = Mathf.Clamp01((t - ButtonsStart - i * buttonStagger) / buttonRevealDuration);
            float e = 1f - Mathf.Pow(1f - x, 3f);
            buttonGroups[i].alpha = e;
            buttons[i].anchoredPosition = new Vector2(0f, -(1f - e) * buttonSlideDistance);
        }

        if (skipHint != null)
            skipHint.alpha = t < EndTime ? Mathf.Clamp01(Clock - 1f) * 0.6f : 0f;

        if (!IsFinished && t >= EndTime)
        {
            IsFinished = true;
            Finished?.Invoke();
        }
    }

    private static float EaseInOutCubic(float x) =>
        x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    private static bool SkipPressed()
    {
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool pad = Gamepad.current != null &&
                   (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame);
        return key || mouse || pad;
    }

    // ---------- Chispas pixeladas del impacto ----------
    private void CreateSparks()
    {
        if (sparksRoot == null || sparkCount <= 0)
            return;

        sparks = new Spark[sparkCount];

        for (int i = 0; i < sparkCount; i++)
        {
            var go = new GameObject("Chispa", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(sparksRoot, false);

            var img = go.GetComponent<Image>();
            img.sprite = sparkSprite;
            img.raycastTarget = false;

            sparks[i].rect = (RectTransform)go.transform;
            sparks[i].image = img;
            go.SetActive(false);
        }
    }

    private void FireSparks()
    {
        if (sparks == null)
            return;

        Color[] palette =
        {
            new Color(1f, 0.92f, 0.45f),
            new Color(1f, 0.6f, 0.12f),
            new Color(0.95f, 0.22f, 0.08f),
        };

        for (int i = 0; i < sparks.Length; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float speed = UnityEngine.Random.Range(380f, 1150f);
            float size = 4f * UnityEngine.Random.Range(1, 4);

            sparks[i].position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * UnityEngine.Random.Range(80f, 420f);
            sparks[i].velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f + 0.3f) * speed;
            sparks[i].maxLife = sparks[i].life = UnityEngine.Random.Range(0.5f, 1.1f);
            sparks[i].rect.sizeDelta = new Vector2(size, size);
            sparks[i].image.color = palette[UnityEngine.Random.Range(0, palette.Length)];
            sparks[i].rect.gameObject.SetActive(true);
        }
    }

    private void UpdateSparks()
    {
        if (sparks == null)
            return;

        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i].life <= 0f)
                continue;

            sparks[i].life -= dt;
            sparks[i].velocity += new Vector2(0f, -1300f) * dt;
            sparks[i].velocity *= 1f - 1.5f * dt;
            sparks[i].position += sparks[i].velocity * dt;

            // Posición en múltiplos de 4 px para que se vea pixelado.
            Vector2 p = sparks[i].position;
            sparks[i].rect.anchoredPosition = new Vector2(Mathf.Round(p.x / 4f) * 4f, Mathf.Round(p.y / 4f) * 4f);

            Color c = sparks[i].image.color;
            c.a = Mathf.Clamp01(sparks[i].life / sparks[i].maxLife * 1.5f);
            sparks[i].image.color = c;

            if (sparks[i].life <= 0f)
                sparks[i].rect.gameObject.SetActive(false);
        }
    }
}
