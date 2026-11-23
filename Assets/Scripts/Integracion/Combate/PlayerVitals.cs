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
    // Escudo temporal del Botiquín del Similares (se gasta primero y caduca).
    public float BonusShield { get; private set; }
    public float BonusShieldMax { get; set; }
    public float OverhealToShield { get; set; }
    private float lastOverheal = -100f;
    // Contrato a Mano Pelona: sin escudo ni regeneración el resto de la partida.
    public bool NoShieldNoRegen { get; set; }
    // Golpe que sí quitó vida (cantidad, ¿era daño periódico como la lava?).
    public static event System.Action<float, bool> HealthLost;
    public float TotalShield => Shield + BonusShield;
    // Mientras sea mayor que Time.time no recibe daño (lo usa el dash).
    public float InvulnerableUntil { get; set; }
    // Tope global: el escudo no pasa del 60 % de la vida máxima.
    public float MaxShield => NoShieldNoRegen || PlayerStats.Instance == null ? 0f
        : Mathf.Min(PlayerStats.Instance.Get(PogoStat.Shield), 0.6f * PogoBridge.MaxHealth(player));

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

        // Regeneración (lo que sobra puede volverse escudo con el Botiquín)
        float max = PogoBridge.MaxHealth(player);
        float regen = NoShieldNoRegen ? 0f : stats.Get(PogoStat.Regeneration);
        if (regen > 0f)
            health = HealInternal(health, max, regen * Time.deltaTime);

        if (BonusShield > 0f && Time.time - lastOverheal > 6f)
            BonusShield = 0f;

        PogoBridge.SetFloat(player, "currentHealth", Mathf.Min(health, max));

        // Escudo: se recarga si pasas un rato sin recibir daño.
        if (Time.time - lastHit > shieldRechargeDelay)
            Shield = Mathf.MoveTowards(Shield, MaxShield, shieldRechargeRate * Time.deltaTime);
        Shield = Mathf.Min(Shield, MaxShield);
    }

    // Cura sin pasarse de la vida máxima; lo que sobra puede volverse escudo temporal (Botiquín).
    private float HealInternal(float health, float max, float amount)
    {
        float next = health + amount;
        if (next > max)
        {
            float extra = next - max;
            next = max;
            if (OverhealToShield > 0f && BonusShieldMax > 0f)
            {
                BonusShield = Mathf.Min(BonusShieldMax, BonusShield + extra * OverhealToShield);
                lastOverheal = Time.time;
            }
        }
        return next;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || PogoBridge.IsDead(player)) return;
        float max = PogoBridge.MaxHealth(player);
        float before = PogoBridge.Health(player);
        float after = HealInternal(before, max, amount);
        PogoBridge.SetFloat(player, "currentHealth", after);
        if (after > before + 0.5f)
            DamageNumbers.Show(player.transform.position + Vector3.up * 1.8f, after - before, DamageKind.Heal);
    }

    // Aplica daño al jugador: primero lo absorbe el escudo y el resto va a PogoPlayer.
    // "periodic" = daño continuo del ambiente (lava, río): no carga objetos como la Olla Exprés.
    public static void Damage(PogoPlayer target, float amount, bool periodic = false)
    {
        PlayerVitals v = Instance;
        if (v != null && target == v.player)
        {
            if (DemoGodMode.Active)
                return;
            if (Time.time < v.InvulnerableUntil || TimeStop.Active)
                return;

            // Escapulario: bloquea el golpe completo.
            ItemEffects fx = ItemEffects.Instance;
            if (fx != null && fx.TryBlockHit(target.transform.position))
            {
                v.InvulnerableUntil = Time.time + 0.4f;
                return;
            }

            v.lastHit = Time.time;
            float bonus = Mathf.Min(v.BonusShield, amount);
            v.BonusShield -= bonus;
            amount -= bonus;
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

        if (amount <= 0f)
            return;

        // Estampita de San Pogo: un golpe que te mataría detiene el tiempo en lugar de matarte.
        if (v != null && target == v.player && amount >= PogoBridge.Health(target) &&
            ItemEffects.Instance != null && ItemEffects.Instance.TryCheatDeath())
            return;

        target.TakeDamage(amount);
        HealthLost?.Invoke(amount, periodic);
    }
}
