using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// Parte nueva del HUD (se arma desde código):
//  - Barra de experiencia arriba con el nivel.
//  - Aviso de HORDA (sin temporizador en pantalla).
//  - Escudo debajo de la barra de vida.
//  - Recarga del dash y saltos en el aire disponibles.
public class RunHUD : MonoBehaviour
{
    [SerializeField] private Font font;
    [SerializeField] private Sprite barSprite;
    [SerializeField] private AudioClip hordeClip;
    [SerializeField] private AudioClip warningClip;

    private RectTransform xpFill;
    private Text levelText;
    private Text hordeTitle;
    private Text hordeSub;
    private RectTransform shieldRoot, shieldFill;
    private RectTransform dashRoot, dashFill;
    private Text dashText;
    private Image[] jumpPips;
    private AudioSource audioSource;
    private float levelPop;
    private int lastLevel = 1;
    private int lastWarningSecond = -1;
    private float hordePop;
    private KeyManager keys;

    private void Start()
    {
        audioSource = UiKit.UiAudio(gameObject);
        Build();

        if (EnemyDirector.Instance != null)
            EnemyDirector.Instance.HordeChanged += OnHordeChanged;
    }

    private void OnDestroy()
    {
        if (EnemyDirector.Instance != null)
            EnemyDirector.Instance.HordeChanged -= OnHordeChanged;
    }

    private void Build()
    {
        Canvas canvas = UiKit.MakeCanvas("HUDProgreso", 5);
        canvas.transform.SetParent(transform, false);
        Transform root = canvas.transform;

        // ---- Barra de experiencia (arriba de todo) ----
        RectTransform xpBar = UiKit.Rect("BarraXP", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            new Vector2(0, 0), new Vector2(0, 12));
        UiKit.Image(xpBar, new Color(0.05f, 0.02f, 0.08f, 0.75f));
        xpFill = UiKit.Rect("Relleno", xpBar, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        UiKit.Image(xpFill, new Color(0.35f, 0.75f, 1f));
        levelText = UiKit.Text(UiKit.Rect("Nivel", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -18), new Vector2(300, 44)), font, 34, new Color(0.6f, 0.85f, 1f), TextAnchor.UpperCenter, "NV 1");

        // ---- Horda ----
        hordeTitle = UiKit.Text(UiKit.Rect("Horda", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -230), new Vector2(1400, 120)), font, 96, new Color(1f, 0.2f, 0.15f), TextAnchor.MiddleCenter);
        hordeSub = UiKit.Text(UiKit.Rect("HordaSub", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -310), new Vector2(1200, 50)), font, 36, new Color(1f, 0.75f, 0.6f), TextAnchor.MiddleCenter);

        // ---- Dash y saltos (abajo a la izquierda) ----
        dashRoot = UiKit.Rect("Dash", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(28, 120), new Vector2(170, 16));
        UiKit.Image(dashRoot, new Color(0.05f, 0.03f, 0.08f, 0.7f));
        dashFill = UiKit.Rect("Relleno", dashRoot, Vector2.zero, new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        UiKit.Image(dashFill, new Color(0.6f, 0.9f, 1f));
        dashText = UiKit.Text(UiKit.Rect("Texto", dashRoot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0),
            new Vector2(0, 4), new Vector2(300, 32)), font, 24, new Color(0.8f, 0.92f, 1f), TextAnchor.LowerLeft, "SHIFT  DASH");

        jumpPips = new Image[10];
        for (int i = 0; i < jumpPips.Length; i++)
        {
            RectTransform pip = UiKit.Rect("Salto" + i, root, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(28 + i * 22, 90), new Vector2(16, 16));
            jumpPips[i] = UiKit.Image(pip, Color.white);
            pip.localRotation = Quaternion.Euler(0, 0, 45f);
        }

        BuildShield();
    }

    // El escudo va justo debajo de la barra de vida del HUD original.
    private void BuildShield()
    {
        GameHUD hud = FindAnyObjectByType<GameHUD>();
        RectTransform healthFill = hud != null
            ? typeof(GameHUD).GetField("healthFill", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hud) as RectTransform
            : null;
        Transform frame = healthFill != null ? healthFill.parent : null;
        if (frame == null)
            return;

        shieldRoot = UiKit.Rect("Escudo", frame, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(0, 8));
        UiKit.Image(shieldRoot, new Color(0.03f, 0.05f, 0.1f, 0.7f));
        shieldFill = UiKit.Rect("Relleno", shieldRoot, Vector2.zero, new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        UiKit.Image(shieldFill, new Color(0.45f, 0.8f, 1f));
    }

    private void OnHordeChanged(bool started)
    {
        if (started)
        {
            hordePop = 1f;
            if (hordeClip != null)
                audioSource.PlayOneShot(hordeClip, 0.9f * GameSettings.SfxVolume);
        }
    }

    private void Update()
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null)
            return;

        float dt = Time.unscaledDeltaTime;

        // ---- XP ----
        UiKit.SetFill(xpFill, stats.Xp / Mathf.Max(1f, stats.XpToNext));
        if (stats.Level != lastLevel)
        {
            lastLevel = stats.Level;
            levelPop = 1f;
        }
        levelPop = Mathf.MoveTowards(levelPop, 0f, dt * 2f);
        levelText.text = "NV " + stats.Level;
        levelText.rectTransform.localScale = Vector3.one * (1f + levelPop * 0.4f);

        UpdateHorde(dt);
        UpdateShield();
        UpdateAbilities(stats);
    }

    private void UpdateHorde(float dt)
    {
        EnemyDirector d = EnemyDirector.Instance;
        if (d == null || d.ArenaMode)
        {
            hordeTitle.text = hordeSub.text = "";
            return;
        }

        hordePop = Mathf.MoveTowards(hordePop, 0f, dt * 1.5f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

        // Después de los 15 minutos: horda infinita (el título se queda chiquito arriba).
        if (d.InfiniteHorde && d.HordeActive)
        {
            float intro = Mathf.Clamp01(hordePop);
            hordeTitle.text = "¡HORDA INFINITA!";
            hordeTitle.color = new Color(1f, 0.3f + pulse * 0.2f, 0.15f, 0.55f + intro * 0.45f);
            hordeTitle.rectTransform.localScale = Vector3.one * (0.55f + intro * 0.6f);
            if (keys == null)
                keys = FindAnyObjectByType<KeyManager>();
            bool ready = keys != null && keys.AllKeysCollected;
            hordeSub.text = ready ? "LA PUERTA DEL INFIERNO TE ESPERA" : "SOBREVIVE Y JUNTA KILLS... O CONSIGUE LAS 4 LLAVES";
            hordeSub.color = new Color(1f, 0.75f, 0.6f, 0.45f + intro * 0.5f);
            hordeSub.rectTransform.localScale = Vector3.one * (0.7f + intro * 0.3f);
        }
        // Sin temporizadores: la horda llega de sorpresa (solo un aviso y tambores antes).
        else if (d.HordeActive)
        {
            // Aparece grande y se va desvaneciendo; sin teñir la pantalla de rojo.
            float show = Mathf.Clamp01(d.HordeTimeLeft > 17f ? 1f : 0.35f + hordePop);
            hordeTitle.text = "¡HORDA!";
            hordeTitle.color = new Color(1f, 0.45f + pulse * 0.2f, 0.2f, show);
            hordeTitle.rectTransform.localScale = Vector3.one * (1f + hordePop * 0.6f);
            hordeSub.text = "";
        }
        else if (d.HordeIncoming)
        {
            int s = Mathf.CeilToInt(d.NextHordeIn);
            hordeTitle.text = "LA HORDA SE ACERCA...";
            hordeTitle.color = new Color(1f, 0.6f, 0.4f, 0.5f + pulse * 0.4f);
            hordeTitle.rectTransform.localScale = Vector3.one * 0.6f;
            hordeSub.text = "";

            if (s != lastWarningSecond)
            {
                lastWarningSecond = s;
                if (warningClip != null)
                    audioSource.PlayOneShot(warningClip, 0.6f * GameSettings.SfxVolume);
            }
        }
        else
        {
            hordeTitle.text = hordeSub.text = "";
        }
    }

    private void UpdateShield()
    {
        if (shieldRoot == null)
            return;
        PlayerVitals v = PlayerVitals.Instance;
        bool has = v != null && v.MaxShield > 0f;
        if (shieldRoot.gameObject.activeSelf != has)
            shieldRoot.gameObject.SetActive(has);
        if (has)
            UiKit.SetFill(shieldFill, v.Shield / v.MaxShield);
    }

    private void UpdateAbilities(PlayerStats stats)
    {
        PogoAbilities a = PogoAbilities.Instance;
        if (a != null)
        {
            float ready = a.DashCooldownTotal > 0f ? 1f - a.DashCooldownLeft / a.DashCooldownTotal : 1f;
            UiKit.SetFill(dashFill, ready);
            dashFill.GetComponent<Image>().color = ready >= 1f ? new Color(0.6f, 0.95f, 1f) : new Color(0.3f, 0.45f, 0.6f);
        }

        PogoJump j = PogoJump.Instance;
        int max = j != null ? j.AirJumpsMax : 0;
        int left = j != null ? j.AirJumpsLeft : 0;
        for (int i = 0; i < jumpPips.Length; i++)
        {
            bool show = i < max;
            if (jumpPips[i].gameObject.activeSelf != show)
                jumpPips[i].gameObject.SetActive(show);
            if (show)
                jumpPips[i].color = i < left ? new Color(0.75f, 0.95f, 1f) : new Color(0.2f, 0.25f, 0.35f, 0.7f);
        }
    }
}
