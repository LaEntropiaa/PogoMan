using System;
using System.Collections.Generic;
using UnityEngine;

// Director de enemigos (reemplaza a los EnemySpawner fijos, sin borrarlos):
//  - Los demonios salen del suelo alrededor de Pogoman.
//  - Con el tiempo salen más, más rápido, con más vida y un poco más veloces (máximo 500).
//  - Cada 4 minutos hay una HORDA de 20 s donde salen muchísimos.
//  - Si un demonio queda muy lejos, se mete al suelo y vuelve a salir cerca de Pogoman.
public class EnemyDirector : MonoBehaviour
{
    public static EnemyDirector Instance { get; private set; }

    [SerializeField] private Enemy enemyPrefab;
    [Tooltip("Otros modelos de demonio: cada aparición elige uno al azar (mismas stats).")]
    [SerializeField] private Enemy[] otherPrefabs;

    [Header("Voladores (Ojo Alado)")]
    [SerializeField] private Enemy[] flyingPrefabs;
    [Tooltip("Probabilidad de que una aparición sea un volador.")]
    [SerializeField, Range(0f, 1f)] private float flyerChance = 0.12f;
    [SerializeField] private float flyerStartMinute = 1f;
    [SerializeField] private int maxFlyers = 20;
    [Tooltip("Alturas sobre el suelo que se prueban para que quepan cuerpo y alas.")]
    [SerializeField] private float[] flyerHeights = { 2.4f, 3.2f, 4f, 1.8f };
    [SerializeField] private int maxEnemies = 500;
    [Tooltip("Apaga los EnemySpawner viejos de la escena.")]
    [SerializeField] private bool disableOldSpawners = true;

    [Header("Dificultad (de 0 a 15 min)")]
    [SerializeField] private float aliveAtStart = 6f;
    [SerializeField] private float aliveAtEnd = 240f;
    [SerializeField] private float spawnRateAtStart = 0.5f;   // por segundo
    [SerializeField] private float spawnRateAtEnd = 8f;
    [Tooltip("Vida extra por minuto (0.12 = +12 % por minuto).")]
    [SerializeField] private float healthPerMinute = 0.12f;
    [SerializeField] private float speedPerMinute = 0.035f;
    [Tooltip("Daño extra por minuto (0.07 = +7 % por minuto; a los 15 min pegan casi el doble).")]
    [SerializeField] private float damagePerMinute = 0.07f;
    [SerializeField] private float maxSpeedMultiplier = 1.6f;

    [Header("Horda")]
    [SerializeField] private float hordeInterval = 240f;
    [SerializeField] private float hordeDuration = 20f;
    [SerializeField] private float hordeWarning = 5f;
    [SerializeField] private float hordeSpawnRate = 35f;
    [Tooltip("Demonios extra durante la horda (crece con cada horda).")]
    [SerializeField] private float hordeExtraAlive = 220f;
    [Tooltip("Hordas antes de la horda infinita (minuto 3 y minuto 6).")]
    [SerializeField] private int maxHordes = 2;
    [Tooltip("Después de la primera horda salen más demonios (multiplicador de vivos y de ritmo).")]
    [SerializeField] private float afterFirstHordeAlive = 1.6f;
    [SerializeField] private float afterFirstHordeRate = 1.5f;

    [Header("Élites (corona)")]
    [Tooltip("Cada cuántos demonios aparece un élite.")]
    [SerializeField] private int eliteEvery = 100;

    [Header("Aparición")]
    [SerializeField] private float minSpawnDistance = 14f;
    [SerializeField] private float maxSpawnDistance = 26f;
    [SerializeField] private float hordeMinSpawnDistance = 10f;
    [Tooltip("Más lejos que esto el demonio se mete al suelo y reaparece cerca.")]
    [SerializeField] private float relocateDistance = 45f;
    [SerializeField] private int maxSpawnsPerFrame = 6;

    // ---- Estado público para el HUD ----
    public bool HordeActive { get; private set; }
    public int HordeNumber { get; private set; }
    public float HordeTimeLeft { get; private set; }
    public float NextHordeIn { get; private set; }
    public bool HordeIncoming => !HordeActive && NextHordeIn <= hordeWarning;
    public int AliveCount { get; private set; }
    // Qué tan arriba del suelo real está Pogoman (encima de demonios, volando...).
    public float PlayerHeightAboveGround { get; private set; }
    public event Action<bool> HordeChanged;   // true = empezó, false = terminó

    // Al acabarse los 15 minutos la horda ya no se detiene y cada minuto es más fuerte.
    public bool InfiniteHorde { get; private set; }
    public event Action InfiniteHordeStarted;
    [Header("Horda infinita (después del contrarreloj)")]
    [Tooltip("Cada cuántos segundos la horda infinita sube de fuerza.")]
    [SerializeField] private float infiniteStepSeconds = 60f;
    [Tooltip("En la horda infinita, cada cuántos segundos se multiplica la vida y el daño de los demonios.")]
    [SerializeField] private float infiniteMultiplyEvery = 60f;
    [Tooltip("Exponencial: ×2 cada minuto (×2, ×4, ×8...).")]
    [SerializeField] private float infiniteMultiplier = 2f;
    [Tooltip("Velocidad extra de los demonios en la horda infinita.")]
    [SerializeField] private float infiniteSpeedBonus = 0.15f;

    private int spawnCounter;
    public int SpawnedTotal => spawnCounter;

    // Multiplicador de vida y daño de la horda infinita: ×2 al empezar y se duplica cada minuto.
    public float InfiniteScale
    {
        get
        {
            float limit = PlayerStats.Instance != null ? PlayerStats.Instance.TimeLimit : 600f;
            if (!InfiniteHorde || RunTime < limit) return 1f;
            return Mathf.Pow(infiniteMultiplier, 1 + Mathf.FloorToInt((RunTime - limit) / infiniteMultiplyEvery));
        }
    }

    // Daño de los demonios ahora mismo (lo usan los contraataques).
    public float DamageMultiplier => (1f + damagePerMinute * (Minutes + (EndlessMode.Active ? EndlessMode.Minutes * 1.5f : 0f))) * InfiniteScale * EndlessMode.Scale;

    // En la arena del Diablo no salen demonios solos: los invoca el jefe con SpawnEnemy.
    private bool arenaMode;
    public bool ArenaMode
    {
        get => arenaMode;
        set
        {
            arenaMode = value;
            if (value && HordeActive)
            {
                HordeActive = false;
                HordeChanged?.Invoke(false);
            }
        }
    }

    private Transform player;
    private float spawnBudget;
    private float checkTimer;
    private readonly List<EnemyTracker> buffer = new List<EnemyTracker>();

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
        player = p != null ? p.transform : null;

        if (disableOldSpawners)
            foreach (EnemySpawner s in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include))
                s.enabled = false;
    }

    private float RunTime => PlayerStats.Instance != null ? PlayerStats.Instance.RunTime : Time.timeSinceLevelLoad;
    private float Minutes => RunTime / 60f;

    private void Update()
    {
        if (player == null || enemyPrefab == null)
            return;

        if (PlayerStats.Instance != null && PlayerStats.Instance.RunEnded)
            return;

        if (arenaMode)
        {
            AliveCount = EnemyTracker.All.Count;
            return;
        }

        UpdateHorde();
        referenceY = GroundUnderPlayer();
        PlayerHeightAboveGround = player.position.y - referenceY;

        AliveCount = EnemyTracker.All.Count;

        // ---- Cuántos demonios queremos vivos y qué tan rápido salen ----
        float limitTime = PlayerStats.Instance != null ? PlayerStats.Instance.TimeLimit : 600f;
        float progress = Mathf.Clamp(RunTime / limitTime, 0f, 1.3f);
        float targetAlive = Mathf.Lerp(aliveAtStart, aliveAtEnd, Mathf.Pow(progress, 1.25f));
        float rate = Mathf.Lerp(spawnRateAtStart, spawnRateAtEnd, Mathf.Pow(progress, 1.3f));

        // Pasada la primera horda el infierno ya no se calma: salen más.
        if (RunTime > hordeInterval + hordeDuration)
        {
            targetAlive *= afterFirstHordeAlive;
            rate *= afterFirstHordeRate;
        }

        if (HordeActive)
        {
            targetAlive += hordeExtraAlive * (1f + 0.3f * (HordeNumber - 1));
            rate = hordeSpawnRate;
        }
        if (EndlessMode.Active)
        {
            // Infierno infinito: empieza como a media partida y cada minuto +20 % de demonios y +15 % de ritmo.
            targetAlive = aliveAtEnd * 0.45f * (1f + 0.2f * EndlessMode.Level);
            rate = spawnRateAtEnd * 0.6f * (1f + 0.15f * EndlessMode.Level);
        }

        // En el modo infinito los demonios ya son muy fuertes y rápidos: menos cantidad para que no se caiga el FPS.
        targetAlive = Mathf.Min(targetAlive, EndlessMode.Active ? 320f : maxEnemies);

        spawnBudget = Mathf.Min(spawnBudget + rate * Time.deltaTime, maxSpawnsPerFrame * 2f);
        int spawned = 0;
        while (spawnBudget >= 1f && AliveCount < targetAlive && spawned < maxSpawnsPerFrame)
        {
            spawnBudget -= 1f;
            if (TrySpawn())
            {
                spawned++;
                AliveCount++;
            }
        }
        if (AliveCount >= targetAlive)
            spawnBudget = Mathf.Min(spawnBudget, 1f);

        // ---- Zombies muy lejos o caídos: se entierran y reaparecen cerca ----
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.5f;
            CheckFarEnemies();
        }
    }

    private void UpdateHorde()
    {
        float t = RunTime;

        float limit = PlayerStats.Instance != null ? PlayerStats.Instance.TimeLimit : 900f;
        if (EndlessMode.Active)
        {
            // El modo infinito no usa la horda: lo escala Update minuto a minuto.
            if (HordeActive) { HordeActive = false; HordeChanged?.Invoke(false); }
            InfiniteHorde = false;
            return;
        }
        if (t >= limit)
        {
            int step = 1 + Mathf.FloorToInt((t - limit) / infiniteStepSeconds);
            int number = maxHordes + step;
            HordeTimeLeft = hordeDuration;
            NextHordeIn = 0f;
            if (!InfiniteHorde)
            {
                InfiniteHorde = true;
                HordeActive = true;
                HordeNumber = number;
                InfiniteHordeStarted?.Invoke();
                HordeChanged?.Invoke(true);
            }
            HordeNumber = number;
            return;
        }

        // Hordas al minuto 3 y al 6 (luego, al acabarse el tiempo, la horda infinita).
        int index = Mathf.FloorToInt(t / hordeInterval);
        float intoCycle = t - index * hordeInterval;
        bool active = index >= 1 && index <= maxHordes && intoCycle < hordeDuration;

        HordeTimeLeft = active ? hordeDuration - intoCycle : 0f;
        NextHordeIn = index + 1 <= maxHordes ? (index + 1) * hordeInterval - t : limit - t;

        if (active != HordeActive)
        {
            HordeActive = active;
            if (active)
                HordeNumber = index;
            HordeChanged?.Invoke(active);
        }
    }

    private bool TrySpawn()
    {
        if (!FindSpawnPoint(HordeActive ? hordeMinSpawnDistance : minSpawnDistance, maxSpawnDistance, out Vector3 ground))
            return false;

        SpawnEnemy(PickSpawnPrefab(), ground);
        return true;
    }

    // Saca un demonio del suelo en ese punto con la vida y velocidad que tocan por el tiempo de partida
    // (lo usa también el Diablo en la arena para invocar).
    public Enemy SpawnEnemy(Enemy prefab, Vector3 ground)
    {
        if (prefab == null)
            prefab = PickPrefab();
        Vector3 look = player != null ? player.position - ground : Vector3.forward;
        look.y = 0f;
        Quaternion rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

        bool flyer = prefab.GetComponent<OjoEnemy>() != null;
        Vector3 position = flyer ? FlyerSpot(ground) : ground + Vector3.up * 1f;
        Enemy enemy = Instantiate(prefab, position, rotation);
        enemy.SetTarget(player);

        // Más vida y velocidad con el tiempo.
        float minutes = Minutes + (EndlessMode.Active ? EndlessMode.Minutes * 1.5f : 0f);
        float infinite = InfiniteScale * EndlessMode.Scale;   // modo infinito: ×3 cada minuto
        EnemyTracker tracker = enemy.GetComponent<EnemyTracker>();
        // Demonios en su propia capa: no chocan (ni empujan) a los guardianes de los pilares.
        if (tracker != null && !tracker.IsBoss && enemy.GetComponent<Guardian>() == null)
            SetLayer(enemy.transform, Guardian.DemonLayer);
        // En el modo infinito salen más élites (cada vez más seguido).
        int every = EndlessMode.Active ? Mathf.Max(6, 30 - 4 * EndlessMode.Level) : eliteEvery;
        bool elite = every > 0 && (tracker == null || !tracker.IsBoss) && ++spawnCounter % every == 0;
        float health = PogoBridge.MaxHealth(enemy) * (1f + healthPerMinute * minutes) * infinite * (elite ? EliteEnemy.HealthFactor : 1f);
        PogoBridge.SetFloat(enemy, "maxHealth", health);
        PogoBridge.SetFloat(enemy, "currentHealth", health);

        float speed = Mathf.Min(maxSpeedMultiplier, 1f + speedPerMinute * minutes) * UnityEngine.Random.Range(0.88f, 1.12f)
            * (InfiniteHorde ? 1f + infiniteSpeedBonus : 1f)
            * EndlessMode.SpeedScale;   // después del Diablo: rapidísimos
        PogoBridge.SetFloat(enemy, "moveSpeed", PogoBridge.GetFloat(enemy, "moveSpeed", 3f) * speed);

        float damageMult = (1f + damagePerMinute * minutes) * infinite * (elite ? EliteEnemy.DamageFactor : 1f);
        if (elite)
            EliteEnemy.Make(enemy);
        if (flyer)
        {
            // El ojo vuela solo (OjoAlado): sin ZombieMotor ni salir de la tierra.
            PogoBridge.SetFloat(enemy, "damage", PogoBridge.GetFloat(enemy, "damage", 8f) * damageMult);
            return enemy;
        }
        foreach (EnemyContactDamage hit in enemy.GetComponentsInChildren<EnemyContactDamage>())
            PogoBridge.SetFloat(hit, "damage", PogoBridge.GetFloat(hit, "damage", 10f) * damageMult);

        ZombieMotor motor = ZombieMotor.Ensure(enemy);
        motor.Emerge(ground);
        return enemy;
    }

    public Enemy RandomDemonPrefab() => PickPrefab();

    private static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }
    public Enemy FlyingPrefab => flyingPrefabs != null && flyingPrefabs.Length > 0 ? flyingPrefabs[UnityEngine.Random.Range(0, flyingPrefabs.Length)] : null;

    private Enemy PickSpawnPrefab()
    {
        if (flyingPrefabs != null && flyingPrefabs.Length > 0 && Minutes >= flyerStartMinute &&
            OjoEnemy.Alive < maxFlyers && UnityEngine.Random.value < flyerChance)
        {
            Enemy f = flyingPrefabs[UnityEngine.Random.Range(0, flyingPrefabs.Length)];
            if (f != null)
                return f;
        }
        return PickPrefab();
    }

    // Punto en el aire sobre "ground" con espacio libre para el cuerpo y las alas (envergadura ~2.9 m).
    private Vector3 FlyerSpot(Vector3 ground)
    {
        foreach (float h in flyerHeights)
        {
            Vector3 p = ground + Vector3.up * h;
            if (!Physics.CheckSphere(p + Vector3.up * 0.7f, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                return p;
        }
        return ground + Vector3.up * flyerHeights[0];
    }

    private Enemy PickPrefab()
    {
        int count = 1 + (otherPrefabs != null ? otherPrefabs.Length : 0);
        int i = UnityEngine.Random.Range(0, count);
        Enemy chosen = i == 0 ? enemyPrefab : otherPrefabs[i - 1];
        return chosen != null ? chosen : enemyPrefab;
    }

    // Busca un punto de suelo alrededor de Pogoman (no lava, no muy empinado).
    public bool FindSpawnPoint(float minDistance, float maxDistance, out Vector3 ground)
    {
        ground = default;
        if (player == null)
            return false;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            // Si no encuentra lugar, poco a poco busca más cerca.
            float shrink = attempt < 6 ? 1f : 0.6f;
            float distance = UnityEngine.Random.Range(minDistance, maxDistance) * shrink;
            Vector3 candidate = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            // Primero desde un poco arriba de Pogoman (para no caer encima de rocas altas), luego desde más alto.
            if (GroundAt(candidate, 6f, out ground) || GroundAt(candidate, 25f, out ground))
                return true;
        }

        SpawnFailures++;
        return false;
    }

    public int SpawnFailures { get; private set; }

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private float referenceY;

    // Altura del suelo debajo de Pogoman (si va volando o encima de una pila de demonios,
    // los demonios salen a nivel del suelo, no a su altura).
    private float GroundUnderPlayer()
    {
        int count = Physics.RaycastNonAlloc(player.position + Vector3.up * 0.5f, Vector3.down, hits, 80f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = player.position.y;
        for (int i = 0; i < count; i++)
        {
            Rigidbody body = hits[i].collider.attachedRigidbody;
            if (body != null && (body.GetComponent<Enemy>() != null || body.GetComponent<PogoPlayer>() != null))
                continue;
            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                y = hits[i].point.y;
            }
        }
        return y;
    }

    private bool GroundAt(Vector3 candidate, float height, out Vector3 ground)
    {
        ground = default;
        Vector3 origin = new Vector3(candidate.x, referenceY + height, candidate.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, height + 30f, ~0, QueryTriggerInteraction.Ignore);

        // El suelo más alto que no sea un demonio ni Pogoman (en la horda hay demonios por todos lados).
        int best = -1;
        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i].collider;
            if (c.attachedRigidbody != null && (c.attachedRigidbody.GetComponent<Enemy>() != null || c.attachedRigidbody.GetComponent<PogoPlayer>() != null))
                continue;
            if (best < 0 || hits[i].distance < hits[best].distance)
                best = i;
        }
        if (best < 0)
            return false;

        RaycastHit hit = hits[best];
        if (Vector3.Angle(hit.normal, Vector3.up) > 50f)
            return false;
        if (Mathf.Abs(hit.point.y - referenceY) > 25f)
            return false;
        if (hit.collider.GetComponent<LavaHazard>() != null)
            return false;

        ground = hit.point;
        return true;
    }

    public bool FindSpawnPoint(out Vector3 ground) => FindSpawnPoint(minSpawnDistance, maxSpawnDistance, out ground);

    private void CheckFarEnemies()
    {
        buffer.Clear();
        buffer.AddRange(EnemyTracker.All);

        int relocated = 0;
        float far = relocateDistance * relocateDistance;

        foreach (EnemyTracker e in buffer)
        {
            if (e == null || e.IsDead)
                continue;

            if (e.Enemy is OjoEnemy ojo)
            {
                Vector3 o = e.transform.position - player.position;
                if ((o.y < -40f || new Vector3(o.x, 0f, o.z).sqrMagnitude > far) && relocated < 25 && FindSpawnPoint(out Vector3 g))
                {
                    ojo.Teleport(FlyerSpot(g));
                    relocated++;
                }
                continue;
            }

            ZombieMotor motor = ZombieMotor.Ensure(e.Enemy);
            if (!motor.CanRelocate)
                continue;

            Vector3 offset = e.transform.position - player.position;
            bool fellOff = offset.y < -40f;
            Vector3 flat = new Vector3(offset.x, 0f, offset.z);

            if ((fellOff || flat.sqrMagnitude > far || motor.IsStuck) && relocated < 25)
            {
                motor.Relocate();
                relocated++;
            }
        }
    }
}
