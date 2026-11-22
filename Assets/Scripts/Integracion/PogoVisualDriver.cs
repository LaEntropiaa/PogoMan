using PogoMan.Rebote;
using UnityEngine;

// Avisa a PogoVisual (animaciones del nuevo PogoMan) SOLO al entrar en cada estado real del jugador.
// No toca las físicas: el rebote, el daño y los efectos siguen saliendo de PogoPlayer/PogoJump.
//   rebote / salto / géiser / doble salto -> Despegar  (tiene la última palabra si toca suelo y rebota en el mismo paso)
//   tocar suelo y quedarse                -> Aterrizar
//   deslizarse (Ctrl en el piso)          -> Cargar     (agachado, resorte comprimido)
//   picada (Ctrl en el aire)              -> Aplastar
//   salir de una cornisa sin impulso      -> Aire
//   giro brusco o dash lateral            -> Giro(-1/+1)
//   daño confirmado (vida o escudo)       -> Golpe
[DefaultExecutionOrder(50)]
public class PogoVisualDriver : MonoBehaviour
{
    [SerializeField] private PogoVisual visual;
    [Tooltip("El objeto que gira hacia donde mira Pogoman (Modelo).")]
    [SerializeField] private Transform facing;

    [SerializeField] private float takeoffDelta = 4f;
    [Tooltip("Tiempo en el aire sin impulso para considerar que se cayó de una cornisa.")]
    [SerializeField] private float ledgeTime = 0.12f;
    [Tooltip("Grados que tiene que girar el modelo dentro de la ventana para contar como giro brusco.")]
    [SerializeField] private float turnAngle = 75f;
    [SerializeField] private float turnWindow = 0.4f;
    [SerializeField] private float turnCooldown = 0.7f;

    private enum Vis { Ground, Air, Dive, Slide }
    private Vis state = Vis.Ground;

    private Rigidbody rb;
    private PogoPlayer player;
    private PogoJump jump;
    private PogoAbilities abilities;
    private bool launched;
    private bool wasGrounded = true;
    private float lastVy;
    private float airTime;
    private int landSteps = -1;
    private float lastHealth = -1f, lastShield;
    private float lastYaw = float.NaN;
    private float turnAccum, turnTime, turnCd;
    private int dashSteps = -1;
    private Vector3 dashRight;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PogoPlayer>();
        jump = GetComponent<PogoJump>();
        if (visual == null)
            visual = GetComponentInChildren<PogoVisual>();
    }

    private void OnEnable()
    {
        if (jump != null)
        {
            jump.AirJumped += OnLaunch;
            jump.SlideJumped += OnLaunch;
            jump.GeyserLaunched += OnLaunch;
        }
    }

    private void OnDisable()
    {
        if (jump != null)
        {
            jump.AirJumped -= OnLaunch;
            jump.SlideJumped -= OnLaunch;
            jump.GeyserLaunched -= OnLaunch;
        }
        if (abilities != null)
            abilities.DashStarted -= OnDash;
    }

    private void OnLaunch() => launched = true;
    // El lado se mide contra hacia dónde miraba al empezar; la velocidad del dash se lee un par de pasos después.
    private void OnDash(Vector3 _)
    {
        dashSteps = 2;
        dashRight = Vector3.ProjectOnPlane((facing != null ? facing : transform).right, Vector3.up).normalized;
    }

    private void FixedUpdate()
    {
        if (visual == null || player == null || PogoBridge.IsDead(player))
            return;

        // PogoAbilities lo agrega RunSystems al iniciar la partida.
        if (abilities == null && (abilities = GetComponent<PogoAbilities>()) != null)
            abilities.DashStarted += OnDash;

        float dt = Time.fixedDeltaTime;
        bool grounded = PogoBridge.IsGrounded(player);
        Vector3 v = rb.linearVelocity;
        bool takeoff = launched || (v.y - lastVy > takeoffDelta && v.y > 2f);
        launched = false;
        bool sliding = player.IsSliding;
        bool diving = !grounded && PogoBridge.IsCtrlHeld(player);
        airTime = grounded ? 0f : airTime + dt;

        if (takeoff)
        {
            visual.Despegar();
            state = Vis.Air;
            landSteps = -1;
        }
        else if (grounded && v.y < 1f && (state == Vis.Air || state == Vis.Dive) && landSteps < 0)
        {
            landSteps = 0;   // espera un paso: si rebota enseguida, solo se ve el Despegar
        }
        else if (landSteps >= 0)
        {
            if (++landSteps >= 2)
            {
                landSteps = -1;
                if (grounded && v.y < 1f)
                {
                    if (sliding) { visual.Cargar(); state = Vis.Slide; }
                    else { visual.Aterrizar(); state = Vis.Ground; }
                }
            }
        }
        else if (state == Vis.Ground && sliding)
        {
            visual.Cargar();
            state = Vis.Slide;
        }
        else if (state == Vis.Slide && grounded && !sliding)
        {
            visual.Reposo();
            state = Vis.Ground;
        }
        else if ((state == Vis.Ground || state == Vis.Slide) && airTime > ledgeTime)
        {
            visual.Aire();
            state = Vis.Air;
        }
        else if (state == Vis.Air && diving)
        {
            visual.Aplastar();
            state = Vis.Dive;
        }
        else if (state == Vis.Dive && !diving && !grounded)
        {
            visual.Aire();
            state = Vis.Air;
        }

        UpdateTurn(v, dt);
        UpdateHurt();

        wasGrounded = grounded;
        lastVy = v.y;
    }

    // Giro brusco = el modelo (Modelo, que PogoAnimator orienta hacia la velocidad) gira mucho en poco tiempo.
    // Se mide en el modelo y no en la velocidad: al dar la vuelta la velocidad pasa por cero.
    private void UpdateTurn(Vector3 v, float dt)
    {
        turnCd -= dt;
        Vector3 flat = new Vector3(v.x, 0f, v.z);

        if (dashSteps >= 0 && --dashSteps < 0)
        {
            float side = flat.sqrMagnitude > 0.01f ? Vector3.Dot(flat.normalized, dashRight) : 0f;
            if (Mathf.Abs(side) > 0.5f)
            {
                visual.Giro(Mathf.Sign(side));
                turnCd = turnCooldown;
                turnAccum = 0f;
            }
        }

        float yaw = (facing != null ? facing : transform).eulerAngles.y;
        if (!float.IsNaN(lastYaw))
        {
            turnAccum += Mathf.DeltaAngle(lastYaw, yaw);   // negativo = izquierda
            turnTime += dt;
            if (turnTime > turnWindow)
            {
                turnAccum *= 0.5f;   // los giros lentos se olvidan
                turnTime = 0f;
            }
            // En picada o deslizándose no: interrumpiría el Aplastar/Cargar.
            if (Mathf.Abs(turnAccum) >= turnAngle && turnCd <= 0f && state != Vis.Dive && state != Vis.Slide)
            {
                visual.Giro(Mathf.Sign(turnAccum));
                turnCd = turnCooldown;
                turnAccum = 0f;
            }
        }
        lastYaw = yaw;
    }

    private void UpdateHurt()
    {
        float health = PogoBridge.Health(player);
        PlayerVitals vitals = PlayerVitals.Instance;
        float shield = vitals != null ? vitals.Shield + vitals.BonusShield : 0f;

        if (lastHealth >= 0f && (health < lastHealth - 0.01f || shield < lastShield - 0.01f))
            visual.Golpe();

        lastHealth = health;
        lastShield = shield;
    }
}
