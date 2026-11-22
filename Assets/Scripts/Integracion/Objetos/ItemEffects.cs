using System.Collections.Generic;
using UnityEngine;

// Efectos de todos los objetos (OBJETOSV3 + catálogo de 30 objetos). Escucha ItemInventory
// y aplica cada efecto según el "id". Los valores se ajustan aquí en el Inspector.
// Las habilidades activas (teclas 1-6) viven en ItemActives.
//
// Daño del pogo:
//   derivado = (50 + Rondana) × (1 + Empuñaduras)      → lo usan hadas, fuego, monedas, explosiones
//   directo  = derivado × Contrato × Taxímetro × Tapón  → solo el pisotón (se escribe cada paso físico)
[DefaultExecutionOrder(-30)]
public class ItemEffects : MonoBehaviour
{
    // ---- OBJETOSV3 ----
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
    // ---- Catálogo de 30 ----
    public const string Rondana = "rondana_de_tortilleria";
    public const string Boquilla = "boquilla_de_elotero";
    public const string Balero = "balero_de_diablito";
    public const string Plantilla = "plantilla_de_talonero";
    public const string Estambre = "estambre_de_mandado";
    public const string Placa = "placa_de_comal";
    public const string Bolillo = "bolillo_del_susto";
    public const string Silbato = "silbato_de_sereno";
    public const string Vale = "vale_de_farmacia";
    public const string Chiflador = "chiflador_de_banqueta";
    public const string Taximetro = "taximetro_adulterado";
    public const string Cerillos = "cerillos_del_velorio";
    public const string Petardo = "petardo_de_sacristia";
    public const string Tapon = "tapon_de_geiser";
    public const string Panico = "boton_de_panico_del_oxxo";
    public const string Salsa = "salsa_de_la_casa";
    public const string Alcancia = "alcancia_de_cochinito";
    public const string Ficha = "ficha_del_futbolito";
    public const string Botiquin = "botiquin_del_similares";
    public const string Detonador = "detonador_del_tianguis";
    public const string Muela = "muela_de_rebote";
    public const string Matraca = "matraca_del_cobrador";
    public const string Contrato = "contrato_a_mano_pelona";
    public const string Credencial = "credencial_de_mayorista";
    public const string Campanita = "campanita_del_animero";
    public const string Olla = "olla_expres_de_la_tia";
    public const string Timbre = "timbre_de_la_micro";
    public const string Pinata = "pinata_del_ultimo_rezo";
    public const string Escrituras = "escrituras_del_panteon";
    public const string Seguro = "seguro_de_danos_a_terceros";

    public static ItemEffects Instance { get; private set; }

    [Header("Compresor de los Lamentos")]
    [SerializeField] private float chargedJumpHeightPerItem = 0.25f;

    [Header("Rendimiento decreciente (Manubrio, Empuñaduras)")]
    [SerializeField, Range(0f, 1f)] private float diminishingFactor = 0.6f;
    [SerializeField] private float airControlPerItem = 0.25f;
    [SerializeField] private float pogoDamagePerItem = 0.15f;

    [Header("Estampita de San Pogo")]
    [SerializeField] private float timeStopDuration = 8f;

    [Header("Agua Bendita de Garrafón")]
    [SerializeField] private float healEverySeconds = 3f;
    [SerializeField] private float spitDamagePercent = 0.2f;
    [SerializeField] private float spitAreaBonus = 0.4f;

    [Header("Escapulario")]
    [SerializeField] private float blockRecharge = 40f;

    [Header("Veladora de la Santa Muerte")]
    [SerializeField] private float fireChancePerItem = 0.1f;
    [SerializeField] private float maxFireChance = 0.6f;
    [SerializeField] private float fireDuration = 3f;
    [SerializeField] private float fireRadius = 1.4f;
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
    [SerializeField] private AudioClip zaWarudoClip;
    [SerializeField] private AudioClip timeStopClip;
    [SerializeField] private AudioClip clockLoopClip;
    [SerializeField] private AudioClip timeResumeClip;
    [SerializeField] private AudioClip explodeClip;
    [SerializeField] private AudioClip steamClip;
    [SerializeField] private AudioClip rocketClip;
    [SerializeField] private AudioClip healClip;

    public Material FxMaterial => fxMaterial;
    public Material CoinMaterial => coinMaterial;
    public Material FairyMaterial => fairyMaterial;
    public Font Font => font;

    private PogoPlayer player;
    private Rigidbody playerBody;
    private Collider playerCollider;
    private float basePogoDamage = 50f;
    private float baseStompRadius = 2.5f;
    private ItemInventory inventory;
    private AudioSource audioSource;
    private readonly List<FairyCompanion> fairies = new List<FairyCompanion>();

    // Escapulario
    public int BlockCharges { get; private set; }
    public float BlockRechargeLeft { get; private set; }
    public float BlockRechargeTotal => blockRecharge;

    // Estado de objetos
    private bool wasGrounded = true;
    private bool tokenArmed;
    private float chifladorReady;
    private float taponUntil = -1f;
    private int stompKills, spitKills;
    private int ollaCharges;
    private float lastOllaCharge = -10f;
    private float seguroReady;
    public bool TimbreSlam { get; set; }
    private readonly List<Vector3> triangle = new List<Vector3>();
    private readonly List<float> triangleTimes = new List<float>();
    private readonly List<Telegraph> triangleLines = new List<Telegraph>();
    private float triangleCooldown;
    private int volleyCounter;

    // ---- Daño del pogo ----
    public float PogoDamage => (basePogoDamage + 5f * Mathf.Min(Count(Rondana), 4)) * (1f + Diminishing(pogoDamagePerItem, Count(Empunaduras)));
    public float DirectPogoDamage
    {
        get
        {
            if (TimbreSlam) return 0f;   // la caída del Timbre sustituye al pisotón
            float d = PogoDamage;
            if (Count(Contrato) > 0) d *= 2f;
            if (Count(Taximetro) > 0 && PogoJump.Instance != null)
                d *= 1f + 0.4f * Mathf.Clamp01((PogoJump.Instance.HorizontalSpeed - 12f) / 20f);
            if (TaponCharged) d *= 1.3f;
            return d;
        }
    }
    public bool TaponCharged => Time.time < taponUntil;
    public Transform Player => player != null ? player.transform : null;
    public PogoPlayer Pogo => player;

    private void Awake()
    {
        Instance = this;
        audioSource = UiKit.UiAudio(gameObject);
        Chest.PriceMultiplier = 1f;   // estado de la partida anterior
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        EnemyTracker.StompHit -= OnStompHit;
        EnemyTracker.Killed -= OnKilled;
        SpitAttack.VolleyFired -= OnVolley;
        SpitProjectile.DirectHit -= OnSpitHit;
        PlayerVitals.HealthLost -= OnHealthLost;
        Chest.AnyOpened -= OnChestOpened;
        if (inventory != null)
            inventory.ItemAdded -= OnItemAdded;
    }

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>();
        if (player != null)
        {
            basePogoDamage = PogoBridge.GetFloat(player, "attackDamage", 50f);
            baseStompRadius = PogoBridge.GetFloat(player, "attackRadius", 2.5f);
            playerBody = player.GetComponent<Rigidbody>();
            playerCollider = player.GetComponent<Collider>();
        }

        inventory = ItemInventory.Instance;
        if (inventory != null)
        {
            inventory.ItemAdded += OnItemAdded;
            foreach (ItemDefinition item in inventory.Items)
                for (int i = 1; i <= inventory.Count(item); i++)
                    OnItemAdded(item, i);
        }

        EnemyTracker.StompHit += OnStompHit;
        EnemyTracker.Killed += OnKilled;
        SpitAttack.VolleyFired += OnVolley;
        SpitProjectile.DirectHit += OnSpitHit;
        PlayerVitals.HealthLost += OnHealthLost;
        Chest.AnyOpened += OnChestOpened;
        if (PogoJump.Instance != null)
        {
            PogoJump.Instance.SlideJumped += OnSlideJumped;
            PogoJump.Instance.AirJumped += OnAirJumped;
            PogoJump.Instance.GeyserLaunched += () => { if (Count(Tapon) > 0) taponUntil = Time.time + 8f; };
        }
        if (PogoAbilities.Instance != null)
        {
            PogoAbilities.Instance.DashStarted += OnDashStarted;
            PogoAbilities.Instance.DashEnded += OnDashEnded;
        }

        TimeStop.Configure(font, zaWarudoClip != null ? zaWarudoClip : timeStopClip, clockLoopClip, timeResumeClip, fxMaterial);
    }

    public int Count(string id) => inventory != null ? inventory.Count(id) : 0;

    private float Diminishing(float perItem, int n) =>
        n <= 0 ? 0f : perItem * (1f - Mathf.Pow(diminishingFactor, n)) / (1f - diminishingFactor);

    // ¿Puede salir este objeto de un cofre? (copias máximas, topes, máximo 6 activas)
    public static bool CanOffer(ItemDefinition item)
    {
        ItemEffects fx = Instance;
        if (fx == null || item == null)
            return true;
        int have = fx.Count(item.id);
        if (item.maxCopies > 0 && have >= item.maxCopies)
            return false;
        if (item.active && have == 0 && ItemActives.Instance != null && ItemActives.Instance.SlotsFull)
            return false;
        PlayerStats stats = PlayerStats.Instance;
        switch (item.id)
        {
            case Tuerca:
                return stats == null || stats.Get(PogoStat.DashCooldown) > stats.MinDashCooldown + 0.01f;
            case Estambre:
                return stats == null || stats.Get(PogoStat.PickupRadius) < 8f - 0.01f;
            case Silbato:
                return stats == null || stats.Get(PogoStat.CritChance) < 0.999f;
            case Placa:
                return PlayerVitals.Instance == null || PlayerVitals.Instance.NoShieldNoRegen == false;
            default:
                return true;
        }
    }

    // ------------------------------------------------------------------ al conseguir un objeto
    private void OnItemAdded(ItemDefinition item, int count)
    {
        PlayerStats stats = PlayerStats.Instance;
        PogoJump jump = PogoJump.Instance;
        PlayerVitals vitals = PlayerVitals.Instance;

        switch (item.id)
        {
            case Compresor:
                if (jump != null) jump.ChargedJumpHeightMultiplier = 1f + chargedJumpHeightPerItem * count;
                break;
            case Manubrio:
                if (jump != null) jump.AirControlMultiplier = 1f + Diminishing(airControlPerItem, count);
                break;
            case Resorte:
                if (count == 1 && stats != null) { stats.AddFlat(PogoStat.AirJumps, 1f); jump?.RefillAirJumps(); }
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
                BlockCharges++;
                break;
            case Cuadro:
                while (fairies.Count < count && player != null)
                    fairies.Add(FairyCompanion.Spawn(this, fairies.Count, fairyMaterial, fxMaterial));
                foreach (FairyCompanion f in fairies)
                    f.SetSlot(fairies.IndexOf(f), fairies.Count);
                break;
            case Tuerca:
                stats?.Multiply(PogoStat.DashCooldown, dashCooldownFactor);
                break;

            // ---- Catálogo de 30 ----
            case Boquilla: stats?.AddFlat(PogoStat.SpitDamage, 2f); break;
            case Balero: if (jump != null) jump.ExtraHopGain = 0.25f * Mathf.Min(count, 3); break;
            case Estambre: stats?.AddFlat(PogoStat.PickupRadius, 1f); break;
            case Placa: stats?.AddFlat(PogoStat.Shield, 10f); break;
            case Bolillo:
                stats?.AddFlat(PogoStat.MaxHealth, 12f);   // PlayerVitals sube la vida máxima y cura lo mismo
                break;
            case Silbato: stats?.AddFlat(PogoStat.CritChance, 0.05f); break;
            case Botiquin:
                if (vitals != null) { vitals.OverhealToShield = 0.4f; vitals.BonusShieldMax = count >= 2 ? 25f : 15f; }
                break;
            case Contrato:
                if (vitals != null) vitals.NoShieldNoRegen = true;
                break;
            case Credencial:
                if (stats != null) stats.CoinPickupMultiplier = 1.5f;
                Chest.PriceMultiplier = 1.3f;   // 50 → 65
                break;
            case Seguro:
                if (PogoAbilities.Instance != null) PogoAbilities.Instance.DashInvulnerable = false;
                break;
        }

        if (item.active)
            ItemActives.Instance?.Register(item);
    }

    // ------------------------------------------------------------------ cada paso
    private void FixedUpdate()
    {
        if (player == null) return;

        // Daño y radio del pisotón que usará PogoPlayer en este paso.
        PogoBridge.SetFloat(player, "attackDamage", DirectPogoDamage);
        float radius = baseStompRadius + 0.2f * Mathf.Min(Count(Plantilla), 3) + (TaponCharged ? 1.5f : 0f);
        PogoBridge.SetFloat(player, "attackRadius", radius);

        bool grounded = PogoBridge.IsGrounded(player);
        if (grounded && !wasGrounded)
            OnLanded();
        wasGrounded = grounded;

        // Ficha del Futbolito: solo el suelo firme la rearma.
        if (grounded && Count(Ficha) > 0 && OnFirmGround())
            tokenArmed = true;
    }

    private Vector3 Feet => playerCollider != null
        ? new Vector3(player.transform.position.x, playerCollider.bounds.min.y, player.transform.position.z)
        : player.transform.position;

    private bool OnFirmGround() =>
        Physics.Raycast(Feet + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore) &&
        hit.rigidbody == null;

    private void OnLanded()
    {
        // Tapón de Géiser: la carga se gasta al aterrizar (aunque falles).
        if (TaponCharged)
        {
            taponUntil = -1f;
            ItemFx.Burst(Feet, 40, 1.5f, new Color(0.4f, 0.9f, 1f), fxMaterial, 8f, 0.4f, 0.6f);
        }
        if (TimbreSlam)
            ItemActives.Instance?.TimbreLanded(Feet);
        if (Count(Escrituras) > 0)
            AddTrianglePoint(Feet);
    }

    private void Update()
    {
        if (TimeStop.Active) return;

        // Escapulario: recarga de una en una.
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

        // Escrituras: los puntos caducan a los 6 s.
        triangleCooldown -= Time.deltaTime;
        for (int i = triangle.Count - 1; i >= 0; i--)
            if (Time.time - triangleTimes[i] > 6f)
                RemoveTrianglePoint(i);
    }

    // ------------------------------------------------------------------ defensa
    public bool TryBlockHit(Vector3 position)
    {
        if (BlockCharges <= 0) return false;
        BlockCharges--;
        if (BlockRechargeLeft <= 0f) BlockRechargeLeft = blockRecharge;
        DamageNumbers.Show(position + Vector3.up * 1.6f, 0f, DamageKind.Blocked);
        Play(blockClip, Random.Range(0.95f, 1.05f), 0.9f);
        ItemFx.Burst(position + Vector3.up * 0.8f, 26, 0.9f, new Color(1f, 0.85f, 0.35f), fxMaterial);
        return true;
    }

    public bool TryCheatDeath()
    {
        if (TimeStop.Active || inventory == null) return false;
        ItemDefinition estampita = null;
        foreach (ItemDefinition item in inventory.Items)
            if (item.id == Estampita) estampita = item;
        if (estampita == null || !inventory.Remove(estampita)) return false;
        TimeStop.Begin(timeStopDuration, player != null ? player.transform.position : Vector3.zero);
        return true;
    }

    // Olla Exprés: cada golpe que quita vida (sin contar lava ni bloqueos) carga; al tercero, vapor.
    private void OnHealthLost(float amount, bool periodic)
    {
        int n = Count(Olla);
        if (n <= 0 || periodic || PogoBridge.IsDead(player)) return;
        if (Time.time - lastOllaCharge < 2f) return;
        lastOllaCharge = Time.time;
        ollaCharges++;
        if (ollaCharges >= 3)
        {
            ollaCharges = 0;
            Explode(player.transform.position, n >= 2 ? 5f : 4f, PogoDamage * 1.8f, new Color(0.95f, 0.95f, 1f), steamClip);
        }
    }

    private void OnChestOpened(Chest chest)
    {
        int n = Mathf.Min(Count(Vale), 3);
        if (n > 0 && PlayerVitals.Instance != null)
        {
            PlayerVitals.Instance.Heal(6f * n);
            Play(healClip, 1.1f, 0.6f);
        }
    }

    // ------------------------------------------------------------------ golpes y muertes
    private void OnStompHit(EnemyTracker enemy, float damage)
    {
        if (Count(Resorte) > 0)
            PogoJump.Instance?.RefillAirJumps();

        int velas = Count(Veladora);
        if (velas > 0 && FireZone.ActiveCount < maxFires && Random.value < Mathf.Min(maxFireChance, fireChancePerItem * velas))
        {
            Vector3 at = enemy.Center;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                at = hit.point;
            FireZone.Spawn(at, fireRadius, fireDuration, PogoDamage * fireTickDamage, fxMaterial);
            Play(fireClip, Random.Range(0.9f, 1.1f), 0.7f);
        }

        if (Count(Detonador) > 0)
            ItemActives.Instance?.PlantCharge(enemy);
    }

    private void OnKilled(EnemyTracker enemy, HitSource source)
    {
        if (enemy == null) return;
        if (source == HitSource.Stomp)
        {
            int n = Count(Limosnero);
            if (n > 0)
            {
                CoinProjectile.Launch(enemy.Center + Vector3.up * 0.4f, enemy, coinsPerItem * n, PogoDamage * coinDamage, coinMaterial, fxMaterial);
                Play(coinThrowClip, Random.Range(0.95f, 1.1f), 0.7f);
            }

            stompKills++;
            int petardo = Count(Petardo);
            if (petardo > 0 && stompKills % 4 == 0)
                Explode(enemy.Center, 3f + (petardo >= 2 ? 0.75f : 0f), PogoDamage * 0.8f, new Color(1f, 0.6f, 0.15f), explodeClip);

            int campana = Count(Campanita);
            if (campana > 0 && stompKills % (campana >= 2 ? 5 : 6) == 0)
                SpiritCompanion.Summon(this, enemy.Center);
        }
        else if (source == HitSource.Spit)
        {
            spitKills++;
            int alc = Count(Alcancia);
            if (alc > 0 && spitKills % 8 == 0)
            {
                PlayerStats.Instance?.AddCoins(alc >= 2 ? 4 : 3);
                ItemFx.Burst(enemy.Center, 20, 0.4f, new Color(1f, 0.85f, 0.3f), fxMaterial, 5f, 0.25f, 0.6f);
                Play(coinThrowClip, 1.3f, 0.6f);
            }
        }
    }

    // Salsa de la Casa: cada quinta salva el proyectil central lleva salpicadura extra.
    private void OnVolley(SpitProjectile central)
    {
        int n = Count(Salsa);
        if (n <= 0) return;
        volleyCounter++;
        if (volleyCounter % 5 == 0)
        {
            central.ExtraSplashRadius = 2.5f;
            central.ExtraSplashFraction = n >= 2 ? 0.8f : 0.6f;
            central.transform.localScale *= 1.4f;
        }
    }

    // Muela de Rebote: el proyectil central rebota hacia otro enemigo.
    private void OnSpitHit(SpitProjectile p, EnemyTracker enemy, Vector3 point)
    {
        int n = Count(Muela);
        if (n <= 0 || !p.Central) return;
        var skip = new List<EnemyTracker> { enemy };
        EnemyTracker next = Nearest(point, 5f, skip);
        if (next != null)
            SpitBounce.Fire(point, next, p.Damage * 0.65f, n >= 2 ? p.Damage * 0.4f : 0f, skip, fxMaterial);
    }

    // ------------------------------------------------------------------ movimiento
    private void OnSlideJumped()
    {
        int n = Count(Chiflador);
        if (n <= 0 || Time.time < chifladorReady || PogoJump.Instance == null || PogoJump.Instance.HorizontalSpeed < 16f) return;
        chifladorReady = Time.time + 3f;
        Vector3 dir = playerBody != null ? playerBody.linearVelocity : player.transform.forward;
        dir.y = 0f;
        Rocket.Fire(player.transform.position + Vector3.up * 0.6f, dir.normalized, PogoDamage * (n >= 2 ? 0.9f : 0.7f), fxMaterial, explodeClip);
        Play(rocketClip, Random.Range(0.95f, 1.1f), 0.8f);
    }

    private void OnAirJumped()
    {
        int n = Count(Ficha);
        if (n <= 0 || !tokenArmed) return;
        tokenArmed = false;
        TokenDrop.Fire(Feet, PogoDamage * (n >= 2 ? 0.85f : 0.6f), coinMaterial, fxMaterial);
    }

    private void OnDashStarted(Vector3 at)
    {
        if (Count(Cerillos) > 0)
            FireZone.Spawn(GroundAt(at), 1.1f, Count(Cerillos) >= 2 ? 3f : 2f, PogoDamage * 0.1f, fxMaterial);

        if (Count(Seguro) > 0 && Time.time >= seguroReady)
        {
            seguroReady = Time.time + 6f;
            Decoy.Spawn(GroundAt(at), PogoDamage * 1.4f, fxMaterial, explodeClip);
        }
    }

    private void OnDashEnded(Vector3 at)
    {
        int n = Count(Cerillos);
        if (n <= 0) return;
        float dur = n >= 2 ? 3f : 2f;
        Vector3 back = playerBody != null ? -playerBody.linearVelocity.normalized : -player.transform.forward;
        back.y = 0f;
        FireZone.Spawn(GroundAt(at + back * 3f), 1.1f, dur, PogoDamage * 0.1f, fxMaterial);
        FireZone.Spawn(GroundAt(at), 1.1f, dur, PogoDamage * 0.1f, fxMaterial);
    }

    // ------------------------------------------------------------------ Escrituras del Panteón
    private void AddTrianglePoint(Vector3 p)
    {
        if (triangleCooldown > 0f) return;
        // Cada lado entre 3 y 10 m y tolerancia vertical de 2 m; si no cabe, se descarta la marca más vieja.
        foreach (Vector3 q in triangle)
        {
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));
            if (d < 3f) return;
            if (d > 10f || Mathf.Abs(p.y - q.y) > 2f) { RemoveTrianglePoint(0); AddTrianglePoint(p); return; }
        }
        triangle.Add(p);
        triangleTimes.Add(Time.time);
        ItemFx.Burst(p + Vector3.up * 0.2f, 14, 0.4f, new Color(0.75f, 0.6f, 1f), fxMaterial, 2f, 0.3f, 0.6f);
        if (triangle.Count >= 2)
        {
            Vector3 a = triangle[triangle.Count - 2];
            Vector3 d = p - a; d.y = 0f;
            triangleLines.Add(Telegraph.Strip(a, d, d.magnitude, 0.25f).SetColor(new Color(0.8f, 0.6f, 1f, 0.8f)));
        }
        if (triangle.Count == 3)
            CloseTriangle();
    }

    private void RemoveTrianglePoint(int i)
    {
        triangle.RemoveAt(i);
        triangleTimes.RemoveAt(i);
        foreach (Telegraph t in triangleLines) if (t != null) Destroy(t.gameObject);
        triangleLines.Clear();
        for (int k = 1; k < triangle.Count; k++)
        {
            Vector3 d = triangle[k] - triangle[k - 1]; d.y = 0f;
            triangleLines.Add(Telegraph.Strip(triangle[k - 1], d, d.magnitude, 0.25f).SetColor(new Color(0.8f, 0.6f, 1f, 0.8f)));
        }
    }

    private void CloseTriangle()
    {
        Vector3 a = triangle[0], b = triangle[1], c = triangle[2];
        float damage = PogoDamage * 1.6f;
        int hit = 0;
        foreach (EnemyTracker e in new List<EnemyTracker>(EnemyTracker.All))
        {
            if (e == null || e.IsDead) continue;
            Vector3 p = e.Center;
            if (InTriangle(new Vector2(p.x, p.z), new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z)))
            {
                e.Hit(damage);
                ItemFx.Burst(p + Vector3.up * 1.5f, 16, 0.4f, new Color(0.7f, 0.65f, 0.6f), fxMaterial, 6f, 0.5f, 0.5f);
                hit++;
            }
        }
        // Lápidas que caen: polvo en los tres lados.
        for (int i = 0; i < 3; i++)
            for (float k = 0f; k <= 1f; k += 0.25f)
                ItemFx.Burst(Vector3.Lerp(triangle[i], triangle[(i + 1) % 3], k) + Vector3.up * 0.3f, 8, 0.3f, new Color(0.6f, 0.55f, 0.6f), fxMaterial, 4f, 0.4f, 0.6f);
        Play(explodeClip, 0.7f, 0.8f);
        foreach (Telegraph t in triangleLines) if (t != null) Destroy(t.gameObject);
        triangleLines.Clear();
        triangle.Clear();
        triangleTimes.Clear();
        triangleCooldown = 8f;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    // ------------------------------------------------------------------ utilidades
    public static Vector3 GroundAt(Vector3 p) => new Vector3(p.x, Telegraph.GroundY(p, p.y), p.z);

    // Explosión de objeto: daña a los demonios en el radio (como daño de objeto).
    public int Explode(Vector3 at, float radius, float damage, Color color, AudioClip clip = null)
    {
        int hits = 0;
        float r2 = radius * radius;
        foreach (EnemyTracker e in new List<EnemyTracker>(EnemyTracker.All))
            if (e != null && !e.IsDead && (e.Center - at).sqrMagnitude <= r2)
            {
                e.Hit(damage);
                hits++;
            }
        ItemFx.Burst(at + Vector3.up * 0.5f, 40, radius * 0.4f, color, fxMaterial, radius * 3f, 0.5f, 0.55f);
        Play(clip != null ? clip : explodeClip, Random.Range(0.9f, 1.1f), 0.8f);
        return hits;
    }

    public float FairyInterval => fairyInterval;
    public float FairyDamage => PogoDamage * fairyDamage;
    public float FairyRange => fairyRange;
    public AudioClip FairyShotClip => fairyShotClip;
    public AudioClip ExplodeClip => explodeClip;

    public void Play(AudioClip clip, float pitch, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    public static EnemyTracker Nearest(Vector3 from, float range, ICollection<EnemyTracker> skip = null)
    {
        EnemyTracker best = null;
        float bestSqr = range * range;
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || !e.Targetable || (skip != null && skip.Contains(e))) continue;
            float d = (e.Center - from).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = e; }
        }
        return best;
    }
}
