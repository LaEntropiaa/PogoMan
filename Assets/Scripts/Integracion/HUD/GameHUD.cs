using UnityEngine;
using UnityEngine.UI;

// HUD de Pogoman: vida, llaves, tiempo y enemigos derrotados.
// También muestra el destello rojo al recibir daño y abre la pantalla de muerte.
public class GameHUD : MonoBehaviour
{
    [Header("Referencias (se buscan solas si están vacías)")]
    [SerializeField] private PogoPlayer player;
    [SerializeField] private KeyManager keyManager;
    [SerializeField] private GameOverScreen gameOver;

    [Header("Vida")]
    [SerializeField] private RectTransform healthFill;
    [SerializeField] private RectTransform healthChip;
    [SerializeField] private Text healthText;
    [SerializeField] private RectTransform heartIcon;

    [Header("Llaves")]
    [SerializeField] private Image[] keyIcons;
    [SerializeField] private Color keyCollected = Color.white;
    [SerializeField] private Color keyMissing = new Color(0.15f, 0.08f, 0.08f, 0.8f);

    [Header("Partida")]
    [SerializeField] private Text timeText;
    [SerializeField] private Text killsText;
    [SerializeField] private Text coinsText;
    [SerializeField] private RectTransform coinIcon;

    [Header("Daño")]
    [SerializeField] private Image hurtVignette;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip keyClip;
    [SerializeField] private float lowHealthThreshold = 0.3f;

    private AudioSource audioSource;
    private float lastHealth;
    private float chip = 1f;
    private float vignette;
    private int lastKeys;
    private float keyPop;
    private bool deathHandled;
    private int lastCoins;
    private float coinPop;
    private float deathTimer;

    private void Start()
    {
        if (player == null) player = FindAnyObjectByType<PogoPlayer>();
        if (keyManager == null) keyManager = FindAnyObjectByType<KeyManager>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        lastHealth = PogoBridge.Health(player);
    }

    private void Update()
    {
        if (player == null)
            return;

        float dt = Time.unscaledDeltaTime;
        float health = PogoBridge.Health(player);
        float max = PogoBridge.MaxHealth(player);
        float percent = Mathf.Clamp01(health / max);

        // ---- Barra de vida (con "rastro" blanco que baja más lento) ----
        SetFill(healthFill, percent);
        chip = Mathf.MoveTowards(chip, percent, dt * (chip > percent ? 0.5f : 5f));
        if (chip < percent) chip = percent;
        SetFill(healthChip, chip);

        if (healthText != null)
            healthText.text = Mathf.CeilToInt(Mathf.Max(0f, health)) + " / " + Mathf.RoundToInt(max);

        if (heartIcon != null)
        {
            float beat = percent < lowHealthThreshold ? 8f : 2.5f;
            float s = 1f + Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * beat)) * (percent < lowHealthThreshold ? 0.18f : 0.06f);
            heartIcon.localScale = Vector3.one * s;
        }

        // ---- Daño recibido ----
        if (health < lastHealth - 0.01f)
        {
            vignette = 1f;
            DamageNumbers.Show(player.transform.position + Vector3.up * 1.6f, lastHealth - health, DamageKind.Player);
            if (hurtClip != null)
                audioSource.PlayOneShot(hurtClip, 0.8f * GameSettings.SfxVolume);
        }
        lastHealth = health;

        vignette = Mathf.MoveTowards(vignette, 0f, dt * 2.5f);
        float low = percent < lowHealthThreshold && health > 0f
            ? 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f)
            : 0f;
        if (hurtVignette != null)
        {
            Color c = hurtVignette.color;
            c.a = Mathf.Max(vignette * 0.75f, low);
            hurtVignette.color = c;
        }

        UpdateKeys(dt);

        // ---- Tiempo y enemigos ----
        PlayerStats stats = PlayerStats.Instance;
        if (stats != null)
        {
            if (timeText != null)
            {
                int seconds = Mathf.FloorToInt(stats.RunTime);
                timeText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            }
            if (killsText != null)
                killsText.text = "x " + stats.Kills;

            if (stats.Coins != lastCoins)
            {
                coinPop = 1f;
                lastCoins = stats.Coins;
            }
            coinPop = Mathf.MoveTowards(coinPop, 0f, dt * 5f);
            if (coinsText != null)
                coinsText.text = stats.Coins.ToString();
            if (coinIcon != null)
                coinIcon.localScale = Vector3.one * (1f + coinPop * 0.35f);
        }

        // ---- Muerte ----
        if (!deathHandled && PogoBridge.IsDead(player))
        {
            deathHandled = true;
            if (stats != null)
                stats.RunEnded = true;
        }

        if (deathHandled && gameOver != null && !gameOver.IsShown)
        {
            deathTimer += dt;
            if (deathTimer > 1.3f)
                gameOver.Show();
        }
    }

    private void UpdateKeys(float dt)
    {
        if (keyIcons == null)
            return;

        int collected = keyManager != null ? keyManager.CollectedKeys : 0;
        int required = keyManager != null ? PogoBridge.GetInt(keyManager, "requiredKeys", 4) : 4;

        if (collected > lastKeys)
        {
            keyPop = 1f;
            if (keyClip != null)
                audioSource.PlayOneShot(keyClip, 0.9f * GameSettings.SfxVolume);
        }
        lastKeys = collected;
        keyPop = Mathf.MoveTowards(keyPop, 0f, dt * 3f);

        for (int i = 0; i < keyIcons.Length; i++)
        {
            Image icon = keyIcons[i];
            if (icon == null)
                continue;

            bool visible = i < required;
            if (icon.gameObject.activeSelf != visible)
                icon.gameObject.SetActive(visible);

            icon.color = i < collected ? keyCollected : keyMissing;
            bool newest = i == collected - 1;
            icon.rectTransform.localScale = Vector3.one * (newest ? 1f + keyPop * 0.6f : 1f);
        }
    }

    private static void SetFill(RectTransform fill, float percent)
    {
        if (fill == null)
            return;
        Vector2 max = fill.anchorMax;
        max.x = percent;
        fill.anchorMax = max;
    }
}
