using System.Collections.Generic;
using UnityEngine;

// Lo que hace cada objeto (documento OBJETOSV3). Escucha ItemInventory y aplica el efecto
// según el "id" del objeto. Los valores son iniciales: se ajustan aquí en el Inspector.
//
// Acumulables (cuando consigues varios del mismo):
//  - Compresor de los Lamentos: +25 % de altura del salto con espacio por cada uno.
//  - Manubrio Manijuela / Empuñaduras de Caín: cada copia rinde menos que la anterior
//    (25 %, +15 %, +9 %... / 15 %, +9 %, +5,4 %...).
//  - Estampita de San Pogo: una vida extra por cada una; se gasta al usarse.
//  - Agua Bendita: cada una suma su regeneración, daño y área.
//  - Escapulario: una carga de bloqueo por cada uno (se recargan de una en una).
//  - Veladora: +10 % de probabilidad de fuego por cada una.
//  - Cuadro de la Última Cena: un hada más por cada uno.
//  - Limosnero: +3 monedas por cada uno.
//  - Tuerca de Contrabando: ×0.8 la recarga del dash por cada una, hasta 0.5 s.
//  - Resorte Sónico: no se acumula (ya no sale de los cofres cuando lo tienes).
public class ItemEffects : MonoBehaviour
{
    public const string Compresor = "compresor_de_los_lamentos";
    public const string Manubrio = "manubrio_manijuela";
    public const string Empunaduras = "empunaduras_de_cain";
    public const string Resorte = "resorte_sonico";
    public const string Estampita = "estampita_de_san_pogo";
    public const string AguaBendita = "agua_bendita_de_garrafon";
    public const string Escapulario = "escapulario_de_la_tienda_de_la_esquina";
    public const string Veladora = "veladora_de_la_santa_muerte_de_oferta";
    public const string Cuadro = "cuadro_de_la_ultima_cena_con_un_comensal_de_mas";
    public const string Limosnero = "limosnero";
    public const string Tuerca = "tuerca_de_contrabando";

    public static ItemEffects Instance { get; private set; }

    [Header("Compresor de los Lamentos")]
    [SerializeField] private float chargedJumpHeightPerItem = 0.25f;

    [Header("Rendimiento decreciente (Manubrio, Empuñaduras)")]
    [Tooltip("Cada copia extra rinde esta fracción de la anterior.")]
    [SerializeField, Range(0f, 1f)] private float diminishingFactor = 0.6f;
    [SerializeField] private float airControlPerItem = 0.25f;
    [SerializeField] private float pogoDamagePerItem = 0.15f;

    [Header("Estampita de San Pogo")]
    [SerializeField] private float timeStopDuration = 8f;

    [Header("Agua Bendita de Garrafón")]
    [SerializeField] private float healEverySeconds = 3f;
    [SerializeField] private float spitDamagePercent = 0.2f;
    [Tooltip("Metros extra del daño en área del escupitajo.")]
    [SerializeField] private float spitAreaBonus = 0.4f;

    [Header("Escapulario")]
    [SerializeField] private float blockRecharge = 40f;

    [Header("Veladora de la Santa Muerte")]
    [SerializeField] private float fireChancePerItem = 0.1f;
    [SerializeField] private float maxFireChance = 0.6f;
    [SerializeField] private float fireDuration = 3f;
    [SerializeField] private float fireRadius = 1.4f;
    [Tooltip("Daño de cada quemada (cada 0.5 s) como fracción del daño del pogo.")]
    [SerializeField] private float fireTickDamage = 0.15f;
    [SerializeField] private int maxFires = 24;

    [Header("Cuadro de la Última Cena")]
    [SerializeField] private float fairyInterval = 2f;
    [SerializeField] private float fairyDamage = 0.25f;
    [SerializeField] private float fairyRange = 18f;
    [SerializeField] private Material fairyMaterial;

    [Header("Limosnero")]
    [SerializeField] private int coinsPerItem = 3;
    [SerializeField] private float coinDamage = 0.2f;
    [SerializeField] private Material coinMaterial;

    [Header("Tuerca de Contrabando")]
    [SerializeField] private float dashCooldownFactor = 0.8f;

    [Header("Efectos")]
    [SerializeField] private Material fxMaterial;
    [SerializeField] private Font font;
    [SerializeField] private AudioClip blockClip;
    [SerializeField] private AudioClip blockReadyClip;
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip fairyShotClip;
    [SerializeField] private AudioClip coinThrowClip;
    [SerializeField] private AudioClip timeStopClip;
    [SerializeField] private AudioClip timeTickClip;
    [SerializeField] private AudioClip timeResumeClip;

    public Material FxMaterial => fxMaterial;

    private PogoPlayer player;
    private float basePogoDamage = 50f;
    private ItemInventory inventory;
    private AudioSource audioSource;
    private readonly List<FairyCompanion> fairies = new List<FairyCompanion>();

    // Escapulario
    public int BlockCharges { get; private set; }
    public float BlockRechargeLeft { get; private set; }
    public float BlockRechargeTotal => blockRecharge;

    // ---- Valores que leen los otros scripts de objetos ----
    public float PogoDamage => player != null ? PogoBridge.GetFloat(player, "attackDamage", basePogoDamage) : basePogoDamage;
    public Transform Player => player != null ? player.transform : null;

    private void Awake()
    {
        Instance = this;
        audioSource = UiKit.UiAudio(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        EnemyTracker.StompHit -= OnStompHit;
        EnemyTracker.Killed -= OnKilled;
        if (inventory != null)
            inventory.ItemAdded -= OnItemAdded;
    }

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>();
        if (player != null)
            basePogoDamage = PogoBridge.GetFloat(player, "attackDamage", 50f);

        inventory = ItemInventory.Instance;
        if (inventory != null)
        {
            inventory.ItemAdded += OnItemAdded;
            // Por si algo ya estaba en el inventario antes de que arrancara este script.
            foreach (ItemDefinition item in inventory.Items)
                for (int i = 1; i <= inventory.Count(item); i++)
                    OnItemAdded(item, i);
        }

        EnemyTracker.StompHit += OnStompHit;
        EnemyTracker.Killed += OnKilled;

        TimeStop.Configure(font, timeStopClip, timeTickClip, timeResumeClip, fxMaterial);
    }

    public int Count(string id) => inventory != null ? inventory.Count(id) : 0;

    // Rendimiento decreciente: a, a·f, a·f²... (suma de las primeras n copias).
    private float Diminishing(float perItem, int n) =>
        n <= 0 ? 0f : perItem * (1f - Mathf.Pow(diminishingFactor, n)) / (1f - diminishingFactor);

    // ¿Puede salir este objeto de un cofre? (no acumulables que ya tienes o ya al máximo, no).
    public static bool CanOffer(ItemDefinition item)
    {
        ItemEffects fx = Instance;
        if (fx == null || item == null)
            return true;
        switch (item.id)
        {
            case Resorte:
                return fx.Count(Resorte) == 0;
            case Tuerca:
                PlayerStats stats = PlayerStats.Instance;
                return stats == null || stats.Get(PogoStat.DashCooldown) > stats.MinDashCooldown + 0.01f;
            default:
                return true;
        }
    }

    // ---- Al conseguir un objeto (count = cuántos tienes ahora) ----
    private void OnItemAdded(ItemDefinition item, int count)
    {
        PlayerStats stats = PlayerStats.Instance;
        PogoJump jump = PogoJump.Instance;

        switch (item.id)
        {
            case Compresor:
                if (jump != null)
                    jump.ChargedJumpHeightMultiplier = 1f + chargedJumpHeightPerItem * count;
                break;

            case Manubrio:
                if (jump != null)
                    jump.AirControlMultiplier = 1f + Diminishing(airControlPerItem, count);
                break;

            case Empunaduras:
                if (player != null)
                    PogoBridge.SetFloat(player, "attackDamage", basePogoDamage * (1f + Diminishing(pogoDamagePerItem, count)));
                break;

            case Resorte:
                if (count == 1 && stats != null)
                {
                    stats.AddFlat(PogoStat.AirJumps, 1f);
                    jump?.RefillAirJumps();
                }
                break;

            case AguaBendita:
                if (stats != null)
                {
                    stats.AddFlat(PogoStat.Regeneration, 1f / healEverySeconds);
                    stats.AddPercent(PogoStat.SpitDamage, spitDamagePercent);
                    stats.AddFlat(PogoStat.SpitArea, spitAreaBonus);
                }
                break;

            case Escapulario:
                BlockCharges++;   // la nueva viene cargada
                break;

            case Cuadro:
                while (fairies.Count < count && player != null)
                    fairies.Add(FairyCompanion.Spawn(this, fairies.Count, fairyMaterial, fxMaterial));
                foreach (FairyCompanion f in fairies)
                    f.SetSlot(fairies.IndexOf(f), fairies.Count);
                break;

            case Tuerca:
                if (stats != null)
                    stats.Multiply(PogoStat.DashCooldown, dashCooldownFactor);
                break;
        }
    }

    private void Update()
    {
        // Escapulario: recarga las cargas gastadas de una en una.
        int max = Count(Escapulario);
        if (BlockCharges < max)
        {
            BlockRechargeLeft -= Time.deltaTime;
            if (BlockRechargeLeft <= 0f)
            {
                BlockCharges++;
                BlockRechargeLeft = BlockCharges < max ? blockRecharge : 0f;
                Play(blockReadyClip, 1f, 0.6f);
                if (player != null)
                    ItemFx.Burst(player.transform.position + Vector3.up * 0.4f, 16, 0.6f, new Color(1f, 0.85f, 0.4f), fxMaterial);
            }
        }
    }

    // ---- Escapulario ----
    public bool TryBlockHit(Vector3 position)
    {
        if (BlockCharges <= 0)
            return false;

        BlockCharges--;
        if (BlockRechargeLeft <= 0f)
            BlockRechargeLeft = blockRecharge;

        DamageNumbers.Show(position + Vector3.up * 1.6f, 0f, DamageKind.Blocked);
        Play(blockClip, Random.Range(0.95f, 1.05f), 0.9f);
        ItemFx.Burst(position + Vector3.up * 0.8f, 26, 0.9f, new Color(1f, 0.85f, 0.35f), fxMaterial);
        return true;
    }

    // ---- Estampita de San Pogo ----
    public bool TryCheatDeath()
    {
        if (TimeStop.Active || inventory == null)
            return false;

        ItemDefinition estampita = null;
        foreach (ItemDefinition item in inventory.Items)
            if (item.id == Estampita)
                estampita = item;
        if (estampita == null || !inventory.Remove(estampita))
            return false;

        TimeStop.Begin(timeStopDuration, player != null ? player.transform.position : Vector3.zero);
        return true;
    }

    // ---- Golpe del pogo a un enemigo ----
    private void OnStompHit(EnemyTracker enemy, float damage)
    {
        // Resorte Sónico (y cualquier salto en el aire): rebotar sobre un enemigo los recupera.
        if (Count(Resorte) > 0)
            PogoJump.Instance?.RefillAirJumps();

        // Veladora: probabilidad de dejar fuego en el suelo.
        int velas = Count(Veladora);
        if (velas > 0 && FireZone.ActiveCount < maxFires &&
            Random.value < Mathf.Min(maxFireChance, fireChancePerItem * velas))
        {
            Vector3 at = enemy.Center;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                at = hit.point;
            FireZone.Spawn(at, fireRadius, fireDuration, PogoDamage * fireTickDamage, fxMaterial);
            Play(fireClip, Random.Range(0.9f, 1.1f), 0.7f);
        }
    }

    // ---- Limosnero: al matar con el pogo lanza monedas ----
    private void OnKilled(EnemyTracker enemy, HitSource source)
    {
        int n = Count(Limosnero);
        if (n <= 0 || source != HitSource.Stomp || enemy == null)
            return;

        int coins = coinsPerItem * n;
        CoinProjectile.Launch(enemy.Center + Vector3.up * 0.4f, enemy, coins, PogoDamage * coinDamage, coinMaterial, fxMaterial);
        Play(coinThrowClip, Random.Range(0.95f, 1.1f), 0.7f);
    }

    public float FairyInterval => fairyInterval;
    public float FairyDamage => PogoDamage * fairyDamage;
    public float FairyRange => fairyRange;
    public AudioClip FairyShotClip => fairyShotClip;

    public void Play(AudioClip clip, float pitch, float volume)
    {
        if (clip == null || audioSource == null)
            return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    // Enemigo vivo más cercano a un punto (para el hada y las monedas).
    public static EnemyTracker Nearest(Vector3 from, float range, ICollection<EnemyTracker> skip = null)
    {
        EnemyTracker best = null;
        float bestSqr = range * range;
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || (skip != null && skip.Contains(e)))
                continue;
            float d = (e.Center - from).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = e;
            }
        }
        return best;
    }
}
