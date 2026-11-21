using UnityEngine;

// Va en cada zombie (el EnemyDirector lo agrega solo). Sin tocar Enemy/Zombie:
//  - Trepa: si hay una pared, una montaña u OTRO ZOMBIE enfrente, sube caminando.
//    Así se forman pilas y "mares" de zombies durante las hordas.
//  - Sale del suelo al aparecer y se mete al suelo para reaparecer cerca de Pogoman.
//  - Usa el modelo ligero (Zombie_Lejos) cuando está lejos de la cámara.
[DefaultExecutionOrder(60)]
[RequireComponent(typeof(Enemy))]
public class ZombieMotor : MonoBehaviour
{
    [Header("Trepar")]
    [SerializeField] private float climbSpeed = 3.2f;
    [Tooltip("Qué tan lejos enfrente revisa si hay algo que trepar.")]
    [SerializeField] private float probeDistance = 0.4f;
    [SerializeField] private float maxClimbTime = 7f;
    [Tooltip("Altura máxima de las pilas de zombies (sobre el suelo real).")]
    [SerializeField] private float maxPileHeight = 5f;

    [Header("Salir / meterse al suelo")]
    [SerializeField] private float emergeTime = 0.9f;
    [SerializeField] private float burrowTime = 0.6f;

    private enum State { Walking, Emerging, Burrowing }

    private static Transform player;
    private static Material dirtMaterial;

    private Enemy enemy;
    private Rigidbody rb;
    private Collider body;
    private EnemyContactDamage attack;
    private State state = State.Walking;
    private float stateTimer;
    private Vector3 groundPoint;
    private float footOffset = 1f;
    private bool climbing;
    private float climbTimer;
    private Vector3 stuckCheckPosition;
    private float stuckTimer;
    private Renderer nearRenderer;
    private float shadowCheck;

    // Con muchos zombies se apagan sus sombras (es lo que más cuesta dibujar).
    private const int ShadowLimit = 120;
    private const int HeavyLimit = 220;

    public bool IsClimbing => climbing;
    public bool CanRelocate => state == State.Walking;
    public bool IsStuck { get; private set; }

    public static ZombieMotor Ensure(Enemy enemy)
    {
        ZombieMotor m = enemy.GetComponent<ZombieMotor>();
        return m != null ? m : enemy.gameObject.AddComponent<ZombieMotor>();
    }

    public static void SetDirtMaterial(Material m) => dirtMaterial = m;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody>();
        body = GetComponent<Collider>();
        attack = GetComponent<EnemyContactDamage>();

        if (body != null)
            footOffset = transform.position.y - body.bounds.min.y;
        if (footOffset < 0.1f)
            footOffset = 1f;

        SetupLod();
        stuckCheckPosition = transform.position;
    }

    private void Start()
    {
        if (player == null)
        {
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p != null) player = p.transform;
        }
    }

    // ---- Modelo ligero de lejos ----
    // De lejos el zombie es una malla fija (ya posada caminando) compartida por todos:
    // se dibuja muchísimo más barato que cientos de esqueletos animados.
    private static Mesh farPoseMesh;
    private LODGroup lodGroup;
    private Renderer farRenderer;
    private bool heavyLod;

    private void SetupLod()
    {
        if (GetComponentInChildren<LODGroup>() != null)
            return;

        Renderer near = null;
        SkinnedMeshRenderer farSkin = null;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == "Zombie_Lejos") farSkin = r as SkinnedMeshRenderer;
            else if (r is SkinnedMeshRenderer && near == null) near = r;
        }
        if (near == null || farSkin == null)
            return;

        if (farPoseMesh == null)
        {
            ZombieRigAnimator rig = GetComponentInChildren<ZombieRigAnimator>();
            if (rig != null)
                rig.ApplyPose(Mathf.PI * 0.5f, 1f, 0f, 0f);
            farPoseMesh = new Mesh { name = "ZombieLejosPose" };
            farSkin.BakeMesh(farPoseMesh, false);
            farPoseMesh.RecalculateBounds();
        }

        var go = new GameObject("Zombie_LejosFijo");
        go.layer = farSkin.gameObject.layer;
        go.transform.SetParent(farSkin.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = farPoseMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = near.sharedMaterials;   // el jefe tiene su propio material
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        farSkin.enabled = false;
        farRenderer = mr;

        near.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        nearRenderer = near;

        lodGroup = near.transform.parent.gameObject.AddComponent<LODGroup>();
        ApplyLods(false);
        lodGroup.RecalculateBounds();
    }

    private void ApplyLods(bool heavy)
    {
        heavyLod = heavy;
        // Con muchísimos zombies, el modelo detallado solo se usa ya muy cerca.
        lodGroup.SetLODs(new[]
        {
            new LOD(heavy ? 0.55f : 0.3f, new[] { nearRenderer }),
            new LOD(0.006f, new[] { farRenderer }),
        });
    }

    // ---- Salir del suelo ----
    public void Emerge(Vector3 ground)
    {
        groundPoint = ground;
        state = State.Emerging;
        stateTimer = 0f;
        SetFrozen(true);
        transform.position = ground + Vector3.up * (footOffset - HiddenDepth());
        Dirt(ground, 18);
    }

    // ---- Meterse al suelo y reaparecer cerca de Pogoman ----
    public void Relocate()
    {
        if (state != State.Walking)
            return;
        state = State.Burrowing;
        stateTimer = 0f;
        groundPoint = body != null ? new Vector3(transform.position.x, body.bounds.min.y, transform.position.z) : transform.position;
        SetFrozen(true);
        Dirt(groundPoint, 12);
    }

    private float HiddenDepth() => footOffset * 2f + 0.3f;

    private void SetFrozen(bool frozen)
    {
        rb.isKinematic = frozen;
        if (!frozen)
            rb.useGravity = true;
        if (attack != null)
            attack.enabled = !frozen;
        // Sin objetivo el zombie no camina (Enemy/Zombie se detienen solos).
        enemy.SetTarget(frozen ? null : player);
        climbing = false;
    }

    private void Update()
    {
        shadowCheck -= Time.deltaTime;
        if (shadowCheck <= 0f && nearRenderer != null)
        {
            shadowCheck = 1f + Random.value * 0.5f;
            int alive = EnemyTracker.All.Count;
            var mode = alive <= ShadowLimit ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (nearRenderer.shadowCastingMode != mode)
                nearRenderer.shadowCastingMode = mode;
            bool heavy = alive > HeavyLimit;
            if (lodGroup != null && heavy != heavyLod)
                ApplyLods(heavy);
        }

        if (state == State.Walking)
            return;

        if (PogoBridge.IsDead(enemy))
            return;

        stateTimer += Time.deltaTime;

        if (state == State.Emerging)
        {
            float k = Mathf.Clamp01(stateTimer / emergeTime);
            float eased = 1f - (1f - k) * (1f - k);
            float shake = (1f - k) * 0.06f;
            Vector3 jitter = new Vector3(Mathf.Sin(stateTimer * 60f), 0f, Mathf.Cos(stateTimer * 53f)) * shake;
            transform.position = groundPoint + jitter + Vector3.up * (footOffset - HiddenDepth() * (1f - eased));

            if (k >= 1f)
            {
                state = State.Walking;
                transform.position = groundPoint + Vector3.up * footOffset;
                SetFrozen(false);
                stuckCheckPosition = transform.position;
                stuckTimer = 0f;
                IsStuck = false;
            }
        }
        else if (state == State.Burrowing)
        {
            float k = Mathf.Clamp01(stateTimer / burrowTime);
            transform.position = groundPoint + Vector3.up * (footOffset - HiddenDepth() * k * k);

            if (k >= 1f)
            {
                EnemyDirector director = EnemyDirector.Instance;
                Vector3 next = groundPoint;
                if (director == null || !director.FindSpawnPoint(out next))
                    next = groundPoint;
                Emerge(next);
            }
        }
    }

    // ---- Trepar ----
    private void FixedUpdate()
    {
        if (state != State.Walking || rb.isKinematic)
            return;

        if (PogoBridge.IsDead(enemy) || player == null)
        {
            StopClimbing();
            return;
        }

        UpdateStuck();

        Vector3 toPlayer = player.position - rb.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.25f)
        {
            StopClimbing();
            return;
        }
        Vector3 dir = toPlayer.normalized;

        bool blocked = ObstacleAhead(dir);

        if (blocked && climbTimer < maxClimbTime)
        {
            climbing = true;
            climbTimer += Time.fixedDeltaTime;
            rb.useGravity = false;
            Vector3 v = rb.linearVelocity;
            v.x *= 0.5f;
            v.z *= 0.5f;
            v.y = climbSpeed;
            rb.linearVelocity = v;
        }
        else if (climbing)
        {
            // Llegó arriba: brinquito hacia adelante para pasar la orilla.
            StopClimbing();
            rb.linearVelocity = dir * 1.8f + Vector3.up * 3.2f;
        }
        else if (rb.linearVelocity.y > -0.5f && !blocked)
        {
            climbTimer = Mathf.MoveTowards(climbTimer, 0f, Time.fixedDeltaTime * 2f);
        }
    }

    private void StopClimbing()
    {
        if (!climbing)
            return;
        climbing = false;
        rb.useGravity = true;
    }

    private bool ObstacleAhead(Vector3 dir)
    {
        if (body == null)
            return false;

        Bounds b = body.bounds;
        float feet = b.min.y;
        float reach = Mathf.Max(b.extents.x, b.extents.z) + probeDistance;

        // Rayo bajo (rodillas) y medio (pecho): si cualquiera toca algo, hay que trepar.
        // A otros zombies solo se les trepa si Pogoman está más arriba, y la pila no pasa de unos 5 m
        // sobre el suelo real (si no, la pila levanta a Pogoman hasta el cielo).
        EnemyDirector director = EnemyDirector.Instance;
        bool pileTooHigh = director != null && director.PlayerHeightAboveGround > maxPileHeight;
        bool climbZombies = player.position.y > feet + 0.6f && !pileTooHigh;
        return Probe(new Vector3(b.center.x, feet + 0.3f, b.center.z), dir, reach, climbZombies) ||
               Probe(new Vector3(b.center.x, b.center.y, b.center.z), dir, reach, climbZombies);
    }

    private bool Probe(Vector3 origin, Vector3 dir, float distance, bool climbZombies)
    {
        if (!Physics.SphereCast(origin, 0.2f, dir, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        Collider c = hit.collider;
        if (c == body || c.transform.IsChildOf(transform))
            return false;
        if (player != null && c.transform.IsChildOf(player))
            return false;

        // Otro zombie enfrente: se le trepa encima.
        Rigidbody other = c.attachedRigidbody;
        if (other != null && other.GetComponent<Enemy>() != null)
            return climbZombies;

        // Pared o pendiente muy empinada.
        return Vector3.Angle(hit.normal, Vector3.up) > 40f;
    }

    // Si lleva mucho rato sin avanzar y está lejos, mejor reaparece.
    private void UpdateStuck()
    {
        stuckTimer += Time.fixedDeltaTime;
        if (stuckTimer < 5f)
            return;

        float moved = Vector3.Distance(stuckCheckPosition, transform.position);
        float distance = Vector3.Distance(transform.position, player.position);
        IsStuck = moved < 1.5f && distance > 12f;
        stuckCheckPosition = transform.position;
        stuckTimer = 0f;
    }

    // ---- Tierra que salta al salir/meterse ----
    private static void Dirt(Vector3 point, int count)
    {
        var go = new GameObject("Tierra");
        go.transform.position = point + Vector3.up * 0.05f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.22f, 0.13f, 0.1f), new Color(0.4f, 0.25f, 0.16f));
        main.gravityModifier = 1.6f;
        main.maxParticles = count;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.4f;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        if (dirtMaterial != null)
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = dirtMaterial;

        ps.Play();
    }
}
