using UnityEngine;
using UnityEngine.InputSystem;

// Control del salto de Pogoman, encima del movimiento de PogoPlayer (sin modificarlo):
//  - Rebote automático corto.
//  - Espacio al caer (o justo después) = salto alto; mantenerlo mientras sube = más alto.
//  - Entre más rápido vas, más alto saltas.
//  - Agachado (Ctrl) + mantener espacio = carga; al soltar Ctrl sale el súper salto.
//  - Deslizarte cuesta abajo te da velocidad extra.
//  - Gravedad extra para que todo se sienta rápido.
// Los géiseres siguen siendo lo más alto: ningún salto normal pasa de maxJumpVelocity.
[RequireComponent(typeof(Rigidbody))]
public class PogoJump : MonoBehaviour
{
    [Header("Gravedad")]
    [SerializeField] private float extraGravity = 12f;
    [Tooltip("Fracción de la gravedad extra que se aplica mientras subes con el salto apretado.")]
    [SerializeField, Range(0f, 1f)] private float holdGravityFactor = 0.3f;
    [SerializeField] private float maxHoldTime = 0.35f;

    [Header("Salto con espacio")]
    [SerializeField] private float spaceBoost = 3f;
    [Tooltip("Si apretaste espacio hasta este tiempo antes de tocar el piso, cuenta.")]
    [SerializeField] private float jumpBuffer = 0.15f;
    [Tooltip("Si aprietas espacio hasta este tiempo después del rebote, también cuenta.")]
    [SerializeField] private float lateWindow = 0.1f;

    [Header("Bonus por velocidad")]
    [SerializeField] private float speedBonusPerUnit = 0.09f;
    [SerializeField] private float maxSpeedBonus = 1.5f;

    [Header("Salto agachado (cargado)")]
    [SerializeField] private float crouchChargeTime = 0.8f;
    [SerializeField] private float crouchMaxBonus = 2.5f;

    [Header("Límites")]
    [Tooltip("Velocidad máxima de un salto normal (el géiser lanza a 15).")]
    [SerializeField] private float maxJumpVelocity = 12.5f;
    [Tooltip("Arriba de esta velocidad el impulso viene de un géiser.")]
    [SerializeField] private float geyserVelocity = 12f;

    [Header("Deslizamiento")]
    [Tooltip("Fuerza que mantiene a Pogoman pegado al piso mientras se desliza.")]
    [SerializeField] private float slideStickForce = 30f;
    [Tooltip("Velocidad máxima hacia arriba permitida al deslizarse (evita brincos en desniveles).")]
    [SerializeField] private float slideMaxUpSpeed = 0.5f;

    [Header("Rampas")]
    [SerializeField] private float slopeAcceleration = 16f;
    [SerializeField] private float maxExtraSlideSpeed = 12f;
    [SerializeField] private float extraSpeedDecay = 5f;
    [SerializeField] private float minSlopeAngle = 4f;

    public float CrouchCharge { get; private set; }
    public bool LastJumpWasBoosted { get; private set; }

    private Rigidbody rb;
    private PogoPlayer player;
    private float lastVerticalSpeed;
    private float lastJumpPress = -10f;
    private float lastBounceTime = -10f;
    private bool boostedThisBounce;
    private bool geyserAscent;
    private float holdTimer;
    private float baseMaxSlideSpeed;
    private float extraSlideSpeed;
    private float notSlidingTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PogoPlayer>();
    }

    private void Start()
    {
        baseMaxSlideSpeed = PogoBridge.GetFloat(player, "maxSlideSpeed", 18f);
    }

    private static bool JumpHeld() =>
        (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
        (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);

    private static bool JumpPressed() =>
        (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
        (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

    private void Update()
    {
        if (JumpPressed())
            lastJumpPress = Time.time;

        bool sliding = player != null && player.IsSliding;

        if (sliding && JumpHeld())
            CrouchCharge = Mathf.Min(1f, CrouchCharge + Time.deltaTime / crouchChargeTime);

        // Si dejas de deslizarte sin saltar, la carga se pierde poco a poco.
        notSlidingTimer = sliding ? 0f : notSlidingTimer + Time.deltaTime;
        if (notSlidingTimer > 0.3f)
            CrouchCharge = 0f;
    }

    private void FixedUpdate()
    {
        if (player != null && PogoBridge.IsDead(player))
            return;

        float dt = Time.fixedDeltaTime;
        Vector3 v = rb.linearVelocity;

        bool crouching = PogoBridge.IsCtrlHeld(player);

        // ---- ¿Acaba de rebotar? (la velocidad vertical saltó hacia arriba) ----
        // Agachado no hay rebotes: si sube es porque el terreno lo empujó.
        if (!crouching && v.y - lastVerticalSpeed > 4f && v.y > 2f)
            OnBounce(ref v);

        // ---- Deslizándose: pegado al piso, sin brincos (los géiseres sí lanzan) ----
        if (crouching && PogoBridge.IsGrounded(player) && v.y < geyserVelocity)
        {
            if (v.y > slideMaxUpSpeed)
                v.y = slideMaxUpSpeed;
            v += Vector3.down * slideStickForce * dt;
        }

        // ---- Espacio apretado justo después del rebote ----
        if (!boostedThisBounce && !geyserAscent && v.y > 0f &&
            Time.time - lastBounceTime < lateWindow && lastJumpPress > lastBounceTime)
        {
            v.y = Mathf.Min(v.y + spaceBoost, maxJumpVelocity);
            boostedThisBounce = true;
            LastJumpWasBoosted = true;
        }

        // ---- Gravedad (más suave mientras subes con el salto apretado) ----
        float gravity = extraGravity;
        if (v.y > 0f && JumpHeld() && holdTimer < maxHoldTime)
        {
            gravity *= holdGravityFactor;
            holdTimer += dt;
        }
        else if (v.y <= 0f)
        {
            holdTimer = maxHoldTime;   // ya no se puede "estirar" este salto
        }

        v += Vector3.down * gravity * dt;

        // ---- Rampas: deslizarte cuesta abajo acelera ----
        HandleSlopes(ref v, dt);

        rb.linearVelocity = v;
        lastVerticalSpeed = v.y;
    }

    private void OnBounce(ref Vector3 v)
    {
        lastBounceTime = Time.time;
        holdTimer = 0f;
        boostedThisBounce = false;
        LastJumpWasBoosted = false;
        geyserAscent = v.y >= geyserVelocity;

        if (geyserAscent)
        {
            CrouchCharge = 0f;
            return;
        }

        float horizontal = new Vector2(v.x, v.z).magnitude;
        float bonus = Mathf.Min(horizontal * speedBonusPerUnit, maxSpeedBonus);

        bool space = JumpHeld() || Time.time - lastJumpPress < jumpBuffer;
        if (space)
        {
            bonus += spaceBoost + CrouchCharge * crouchMaxBonus;
            boostedThisBounce = true;
            LastJumpWasBoosted = true;
        }

        CrouchCharge = 0f;
        v.y = Mathf.Min(v.y + bonus, maxJumpVelocity);
    }

    private void HandleSlopes(ref Vector3 v, float dt)
    {
        bool sliding = player != null && player.IsSliding;
        float slopeSin = 0f;
        Vector3 downhill = Vector3.zero;

        if (sliding && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle > minSlopeAngle && angle < 60f)
            {
                downhill = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;
                slopeSin = Mathf.Sin(angle * Mathf.Deg2Rad);
            }
        }

        if (slopeSin > 0f)
        {
            v += downhill * slopeAcceleration * slopeSin * dt;

            // Si vas cuesta abajo, se te permite ir más rápido que el límite normal.
            if (Vector3.Dot(v, downhill) > 0f)
                extraSlideSpeed = Mathf.Min(maxExtraSlideSpeed, extraSlideSpeed + slopeAcceleration * slopeSin * dt);
        }
        else
        {
            extraSlideSpeed = Mathf.MoveTowards(extraSlideSpeed, 0f, extraSpeedDecay * dt);
        }

        PogoBridge.SetFloat(player, "maxSlideSpeed", baseMaxSlideSpeed + extraSlideSpeed);
    }
}
