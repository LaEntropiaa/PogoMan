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
        float progress = Mathf.Clamp(RunTime / 900f, 0f, 1.3f);
        float targetAlive = Mathf.Lerp(aliveAtStart, aliveAtEnd, Mathf.Pow(progress, 1.25f));
        float rate = Mathf.Lerp(spawnRateAtStart, spawnRateAtEnd, Mathf.Pow(progress, 1.3f));

        if (HordeActive)
        {
            targetAlive += hordeExtraAlive * (1f + 0.3f * (HordeNumber - 1));
            rate = hordeSpawnRate;
        }

        targetAlive = Mathf.Min(targetAlive, maxEnemies);

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
        if (t >= limit)
        {
            int step = 1 + Mathf.FloorToInt((t - limit) / infiniteStepSeconds);
            int number = Mathf.FloorToInt(limit / hordeInterval) + step;
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

        // La primera horda es al minuto 4, luego cada 4 minutos.
        int index = Mathf.FloorToInt(t / hordeInterval);
        float intoCycle = t - index * hordeInterval;
        bool active = index >= 1 && intoCycle < hordeDuration;

        HordeTimeLeft = active ? hordeDuration - intoCycle : 0f;
        NextHordeIn = (index + 1) * hordeInterval - t;

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

        SpawnEnemy(PickPrefab(), ground);
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

        Enemy enemy = Instantiate(prefab, ground + Vector3.up * 1f, rotation);
        enemy.SetTarget(player);

        // Más vida y velocidad con el tiempo.
        float minutes = Minutes;
        float health = PogoBridge.MaxHealth(enemy) * (1f + healthPerMinute * minutes);
        PogoBridge.SetFloat(enemy, "maxHealth", health);
        PogoBridge.SetFloat(enemy, "currentHealth", health);

        float speed = Mathf.Min(maxSpeedMultiplier, 1f + speedPerMinute * minutes) * UnityEngine.Random.Range(0.88f, 1.12f);
        PogoBridge.SetFloat(enemy, "moveSpeed", PogoBridge.GetFloat(enemy, "moveSpeed", 3f) * speed);

        float damageMult = 1f + damagePerMinute * minutes;
        foreach (EnemyContactDamage hit in enemy.GetComponentsInChildren<EnemyContactDamage>())
            PogoBridge.SetFloat(hit, "damage", PogoBridge.GetFloat(hit, "damage", 10f) * damageMult);

        ZombieMotor motor = ZombieMotor.Ensure(enemy);
        motor.Emerge(ground);
        return enemy;
    }

    public Enemy RandomDemonPrefab() => PickPrefab();

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
