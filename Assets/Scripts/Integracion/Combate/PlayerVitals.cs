using UnityEngine;

// Vida extra, regeneración y escudo de Pogoman, encima de PogoPlayer (sin modificarlo).
// Todo el daño al jugador debe pasar por PlayerVitals.Damage para que el escudo funcione.
[RequireComponent(typeof(PogoPlayer))]
public class PlayerVitals : MonoBehaviour
{
    public static PlayerVitals Instance { get; private set; }

    // Sonidos del escudo (los pone RunSystems).
    public static AudioClip ShieldHitClip;
    public static AudioClip ShieldBreakClip;
    private AudioSource audioSource;

    [Tooltip("Segundos sin recibir daño para que el escudo empiece a recargarse.")]
    [SerializeField] private float shieldRechargeDelay = 4f;
    [Tooltip("Escudo que se recupera por segundo.")]
    [SerializeField] private float shieldRechargeRate = 10f;

    public float Shield { get; private set; }
    // Mientras sea mayor que Time.time no recibe daño (lo usa el dash).
    public float InvulnerableUntil { get; set; }
    public float MaxShield => PlayerStats.Instance != null ? PlayerStats.Instance.Get(PogoStat.Shield) : 0f;

    private PogoPlayer player;
    private float baseMaxHealth;
    private float appliedHealthBonus;
    private float lastHit = -100f;

    private void Awake()
    {
        Instance = this;
        player = GetComponent<PogoPlayer>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start() => baseMaxHealth = PogoBridge.MaxHealth(player);

    private void Update()
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null || PogoBridge.IsDead(player))
            return;

        float health = PogoBridge.Health(player);

        // Vida máxima extra: al subirla también te cura esa cantidad.
        float bonus = stats.Get(PogoStat.MaxHealth);
        if (!Mathf.Approximately(bonus, appliedHealthBonus))
        {
            float delta = bonus - appliedHealthBonus;
            appliedHealthBonus = bonus;
            PogoBridge.SetFloat(player, "maxHealth", baseMaxHealth + bonus);
            if (delta > 0f)
                health += delta;
        }

        // Regeneración
        float max = PogoBridge.MaxHealth(player);
        float regen = stats.Get(PogoStat.Regeneration);
        if (regen > 0f && health < max)
            health = Mathf.Min(max, health + regen * Time.deltaTime);

        PogoBridge.SetFloat(player, "currentHealth", Mathf.Min(health, max));

        // Escudo: se recarga si pasas un rato sin recibir daño.
        if (Time.time - lastHit > shieldRechargeDelay)
            Shield = Mathf.MoveTowards(Shield, MaxShield, shieldRechargeRate * Time.deltaTime);
        Shield = Mathf.Min(Shield, MaxShield);
    }

    // Aplica daño al jugador: primero lo absorbe el escudo y el resto va a PogoPlayer.
    public static void Damage(PogoPlayer target, float amount)
    {
        PlayerVitals v = Instance;
        if (v != null && target == v.player)
        {
            if (Time.time < v.InvulnerableUntil)
                return;
            v.lastHit = Time.time;
            float absorbed = Mathf.Min(v.Shield, amount);
            v.Shield -= absorbed;
            amount -= absorbed;
            if (absorbed > 0f)
            {
                DamageNumbers.Show(target.transform.position + Vector3.up * 1.6f, absorbed, DamageKind.Shield);
                bool broke = v.Shield <= 0.01f;
                AudioClip clip = broke ? ShieldBreakClip : ShieldHitClip;
                if (clip != null)
                {
                    v.audioSource.pitch = broke ? 1f : Random.Range(0.95f, 1.1f);
                    v.audioSource.PlayOneShot(clip, (broke ? 1f : 0.7f) * GameSettings.SfxVolume);
                }
            }
        }

        if (amount > 0f)
            target.TakeDamage(amount);
    }
}
