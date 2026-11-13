using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PogoPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform model;

    [Header("Movement")]
    [SerializeField] private float airAcceleration = 12f;
    [SerializeField] private float maxAirSpeed = 7f;

    [Header("Pogo")]
    [SerializeField] private float jumpForce = 8f;

    [Header("Dive")]
    [SerializeField] private float diveAcceleration = 35f;
    [SerializeField] private float diveTiltSpeed = 8f;

    [Header("Slide")]
    [SerializeField] private float slideAcceleration = 25f;
    [SerializeField] private float maxSlideSpeed = 18f;
    [SerializeField] private float slideTurnStrength = 2.5f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckDistance = 0.12f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Vector2 input;

    private bool grounded;
    private bool wasGrounded;

    private bool ctrlHeld;
    private bool wasCtrlHeld;

    private float currentTilt;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        // El Rigidbody no debe inclinarse físicamente.
        // Solamente inclinaremos el modelo.
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        input = Vector2.ClampMagnitude(input, 1f);

        ctrlHeld =
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl);

        UpdateVisualRotation();
    }

    private void FixedUpdate()
    {
        CheckGround();

        HandleCtrlRelease();

        if (ctrlHeld)
        {
            HandleDiveOrSlide();
        }
        else
        {
            HandleNormalMovement();
        }

        HandleLanding();

        wasGrounded = grounded;
        wasCtrlHeld = ctrlHeld;
    }

    private void HandleNormalMovement()
    {
        if (grounded)
        {
            return;
        }

        // Movimiento WASD en el aire.
        Vector3 inputDirection = new Vector3(
            input.x,
            0f,
            input.y
        );

        rb.AddForce(
            inputDirection * airAcceleration,
            ForceMode.Acceleration
        );

        LimitHorizontalSpeed(maxAirSpeed);
    }

    private void HandleDiveOrSlide()
    {
        if (!grounded)
        {
            // Mientras está en el aire y mantenemos Ctrl,
            // hacemos que caiga mucho más rápido.
            rb.AddForce(
                Vector3.down * diveAcceleration,
                ForceMode.Acceleration
            );

            // Todavía podemos controlar la dirección,
            // pero menos que en el movimiento normal.
            Vector3 airDirection = new Vector3(
                input.x,
                0f,
                input.y
            );

            rb.AddForce(
                airDirection * (airAcceleration * 0.5f),
                ForceMode.Acceleration
            );

            LimitHorizontalSpeed(maxSlideSpeed);

            return;
        }

        // Ya estamos tocando el piso:
        // comienza el slide.
        HandleSlideMovement();
    }

    private void HandleSlideMovement()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        // Si todavía no tenemos velocidad suficiente,
        // usamos el input para empezar a movernos.
        if (horizontalVelocity.sqrMagnitude < 0.1f)
        {
            Vector3 inputDirection = new Vector3(
                input.x,
                0f,
                input.y
            );

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                rb.AddForce(
                    inputDirection * slideAcceleration,
                    ForceMode.Acceleration
                );
            }

            return;
        }

        // Dirección actual del slide.
        Vector3 slideDirection = horizontalVelocity.normalized;

        // Dirección que quiere el jugador.
        Vector3 inputDirectionDesired = new Vector3(
            input.x,
            0f,
            input.y
        );

        if (inputDirectionDesired.sqrMagnitude > 0.01f)
        {
            inputDirectionDesired.Normalize();

            // En vez de girar instantáneamente hacia donde
            // apunta WASD, interpolamos lentamente.
            slideDirection = Vector3.Slerp(
                slideDirection,
                inputDirectionDesired,
                slideTurnStrength * Time.fixedDeltaTime
            );

            rb.AddForce(
                slideDirection * slideAcceleration,
                ForceMode.Acceleration
            );
        }

        LimitHorizontalSpeed(maxSlideSpeed);
    }

    private void HandleLanding()
    {
        // Acabamos de tocar el suelo.
        if (grounded && !wasGrounded)
        {
            if (!ctrlHeld)
            {
                Bounce();
            }
            else
            {
                // Si aterrizamos manteniendo Ctrl,
                // entramos directamente en slide.
                StartSlide();
            }
        }
    }

    private void HandleCtrlRelease()
    {
        // Estaba haciendo slide y acaba de soltar Ctrl.
        if (wasCtrlHeld && !ctrlHeld)
        {
            if (grounded)
            {
                Bounce();
            }
        }
    }

    private void Bounce()
    {
        Vector3 velocity = rb.linearVelocity;

        // Quitamos cualquier velocidad vertical anterior.
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.VelocityChange
        );
    }

    private void StartSlide()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        // Si llegamos al suelo sin suficiente velocidad,
        // empezamos a movernos en la dirección del input.
        if (horizontalVelocity.magnitude < 1f)
        {
            Vector3 inputDirection = new Vector3(
                input.x,
                0f,
                input.y
            );

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                inputDirection.Normalize();

                rb.linearVelocity = new Vector3(
                    inputDirection.x * 5f,
                    0f,
                    inputDirection.z * 5f
                );
            }
        }
    }

    private void CheckGround()
    {
        Vector3 origin =
            transform.position +
            Vector3.up * 0.05f;

        float radius = capsule.radius * 0.9f;

        float distance =
            capsule.height / 2f -
            capsule.radius +
            groundCheckDistance;

        grounded = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            distance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    private void LimitHorizontalSpeed(float maxSpeed)
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontal = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        if (horizontal.magnitude <= maxSpeed)
            return;

        horizontal = horizontal.normalized * maxSpeed;

        rb.linearVelocity = new Vector3(
            horizontal.x,
            velocity.y,
            horizontal.z
        );
    }

    private void UpdateVisualRotation()
    {
        if (model == null)
            return;

        float targetTilt = 0f;

        if (ctrlHeld)
        {
            if (!grounded)
            {
                // En el aire empieza a inclinarse progresivamente.
                targetTilt = 70f;
            }
            else
            {
                // En el suelo queda completamente acostado.
                targetTilt = 90f;
            }
        }

        currentTilt = Mathf.Lerp(
            currentTilt,
            targetTilt,
            diveTiltSpeed * Time.deltaTime
        );

        Vector3 horizontalVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        if (horizontalVelocity.sqrMagnitude > 0.1f)
        {
            Quaternion movementRotation =
                Quaternion.LookRotation(
                    horizontalVelocity.normalized,
                    Vector3.up
                );

            Quaternion tiltRotation =
                Quaternion.AngleAxis(
                    currentTilt,
                    Vector3.right
                );

            model.rotation =
                movementRotation * tiltRotation;
        }
        else
        {
            Quaternion tiltRotation =
                Quaternion.Euler(
                    currentTilt,
                    model.eulerAngles.y,
                    0f
                );

            model.rotation = Quaternion.Slerp(
                model.rotation,
                tiltRotation,
                diveTiltSpeed * Time.deltaTime
            );
        }
    }
}
