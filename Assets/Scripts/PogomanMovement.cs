using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PogoPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform model;

    [Header("Air Movement")]
    [SerializeField] private float airAcceleration = 20f;
    [SerializeField] private float airDeceleration = 12f;
    [SerializeField] private float maxAirSpeed = 7f;

    [Header("Pogo")]
    [SerializeField] private float jumpForce = 8f;

    [Header("Dive")]
    [SerializeField] private float diveAcceleration = 35f;
    [SerializeField] private float diveTiltSpeed = 8f;

    [Header("Slide")]
    [SerializeField] private float slideAcceleration = 30f;
    [SerializeField] private float slideDeceleration = 8f;
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
            return;

        Vector3 desiredDirection = new Vector3(
            input.x,
            0f,
            input.y
        );

        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        if (desiredDirection.sqrMagnitude > 0.01f)
        {
            desiredDirection.Normalize();

            // Nos acercamos gradualmente a la velocidad deseada.
            Vector3 targetVelocity =
                desiredDirection * maxAirSpeed;

            Vector3 difference =
                targetVelocity - horizontalVelocity;

            rb.AddForce(
                difference * airAcceleration,
                ForceMode.Acceleration
            );
        }
        else
        {
            // Si no pulsamos nada, frenamos.
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                airDeceleration * Time.fixedDeltaTime
            );

            rb.linearVelocity = new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );
        }

        LimitHorizontalSpeed(maxAirSpeed);
    }

    private void HandleDiveOrSlide()
    {
        if (!grounded)
        {
            HandleDive();
            return;
        }

        HandleSlideMovement();
    }

    private void HandleDive()
    {
        // Caída mucho más rápida mientras mantenemos Ctrl.
        rb.AddForce(
            Vector3.down * diveAcceleration,
            ForceMode.Acceleration
        );

        // Aún podemos controlar la dirección,
        // pero menos que normalmente.
        Vector3 desiredDirection = new Vector3(
            input.x,
            0f,
            input.y
        );

        if (desiredDirection.sqrMagnitude > 0.01f)
        {
            desiredDirection.Normalize();

            Vector3 velocity = rb.linearVelocity;

            Vector3 horizontalVelocity = new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

            Vector3 targetVelocity =
                desiredDirection * maxAirSpeed;

            Vector3 difference =
                targetVelocity - horizontalVelocity;

            rb.AddForce(
                difference * (airAcceleration * 0.5f),
                ForceMode.Acceleration
            );
        }
    }

    private void HandleSlideMovement()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        /*
         * Si acabamos de aterrizar y todavía no tenemos
         * una dirección, usamos el input.
         */
        if (horizontalVelocity.sqrMagnitude < 0.1f)
        {
            Vector3 inputDirection = new Vector3(
                input.x,
                0f,
                input.y
            );

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                horizontalVelocity =
                    inputDirection.normalized * 4f;
            }
            else
            {
                // Dirección inicial por defecto.
                horizontalVelocity =
                    transform.forward * 4f;
            }

            rb.linearVelocity = new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );
        }

        /*
         * El slide siempre continúa hacia delante.
         *
         * WASD ya no determina si nos movemos o no.
         * Una vez que estamos deslizándonos,
         * tenemos velocidad automáticamente.
         */

        Vector3 slideDirection =
            horizontalVelocity.normalized;

        /*
         * A/D/W/S pueden modificar la dirección,
         * pero solamente ligeramente.
         */
        Vector3 inputDirectionDesired = new Vector3(
            input.x,
            0f,
            input.y
        );

        if (inputDirectionDesired.sqrMagnitude > 0.01f)
        {
            inputDirectionDesired.Normalize();

            slideDirection = Vector3.Slerp(
                slideDirection,
                inputDirectionDesired,
                slideTurnStrength * Time.fixedDeltaTime
            );
        }

        /*
         * Aceleramos constantemente en la dirección
         * del slide.
         */
        rb.AddForce(
            slideDirection * slideAcceleration,
            ForceMode.Acceleration
        );

        /*
         * Si dejamos de presionar WASD no frenamos.
         * El slide continúa automáticamente.
         */

        LimitHorizontalSpeed(maxSlideSpeed);
    }

    private void HandleLanding()
    {
        if (grounded && !wasGrounded)
        {
            if (!ctrlHeld)
            {
                Bounce();
            }
            else
            {
                StartSlide();
            }
        }
    }

    private void HandleCtrlRelease()
    {
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

        /*
         * Si ya tenemos velocidad al aterrizar,
         * conservamos esa dirección.
         */
        if (horizontalVelocity.sqrMagnitude < 0.1f)
        {
            horizontalVelocity =
                transform.forward * 4f;
        }

        rb.linearVelocity = new Vector3(
            horizontalVelocity.x,
            0f,
            horizontalVelocity.z
        );
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

        horizontal =
            horizontal.normalized * maxSpeed;

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
                targetTilt = 70f;
            }
            else
            {
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
