using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PogoPlayer : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("References")]
    [SerializeField] private Transform model;
    [SerializeField] private Animator animator;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Ground Attack")]
    [SerializeField] private float attackDamage = 50f;
    [SerializeField] private float attackRadius = 2.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private GameObject impactEffectPrefab;

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

    public bool IsSliding => ctrlHeld && grounded;

    private float currentTilt;
    private float currentHealth;

    private bool isDead;

    private static readonly int AttackTrigger =
        Animator.StringToHash("Attack");

    private static readonly int DeathTrigger =
        Animator.StringToHash("Death");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        currentHealth = maxHealth;

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
            }
            else
            {
                Debug.LogWarning(
                    "PogoPlayer: No camera assigned."
                );
            }
        }
    }

    private void Update()
    {
        if (isDead)
            return;

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
        if (isDead)
            return;

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

    private Vector3 GetInputDirection()
    {
        if (cameraTransform == null)
            return Vector3.zero;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Ignoramos el pitch de la cámara.
        forward.y = 0f;
        right.y = 0f;

        if (forward.sqrMagnitude > 0.001f)
            forward.Normalize();

        if (right.sqrMagnitude > 0.001f)
            right.Normalize();

        Vector3 direction =
            right * input.x +
            forward * input.y;

        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void HandleNormalMovement()
    {
        // El movimiento aéreo solamente ocurre
        // cuando estamos en el aire.
        if (grounded)
            return;

        Vector3 desiredDirection =
            GetInputDirection();

        Vector3 velocity =
            rb.linearVelocity;

        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        if (desiredDirection.sqrMagnitude > 0.01f)
        {
            desiredDirection.Normalize();

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
            horizontalVelocity =
                Vector3.MoveTowards(
                    horizontalVelocity,
                    Vector3.zero,
                    airDeceleration *
                    Time.fixedDeltaTime
                );

            rb.linearVelocity =
                new Vector3(
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
        rb.AddForce(
            Vector3.down * diveAcceleration,
            ForceMode.Acceleration
        );

        Vector3 desiredDirection =
            GetInputDirection();

        if (desiredDirection.sqrMagnitude < 0.01f)
            return;

        desiredDirection.Normalize();

        Vector3 velocity =
            rb.linearVelocity;

        Vector3 horizontalVelocity =
            new Vector3(
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

    private void HandleSlideMovement()
    {
        Vector3 velocity =
            rb.linearVelocity;

        Vector3 slideDirection =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        // Si prácticamente no tenemos velocidad,
        // usamos la dirección de entrada.
        if (slideDirection.sqrMagnitude < 0.1f)
        {
            Vector3 inputDirection =
                GetInputDirection();

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                slideDirection =
                    inputDirection.normalized * 4f;
            }
            else
            {
                // Si tampoco hay input,
                // usamos la dirección actual del modelo.
                Vector3 forward = model != null
                    ? model.forward
                    : transform.forward;

                forward.y = 0f;

                if (forward.sqrMagnitude > 0.01f)
                    slideDirection =
                        forward.normalized * 4f;
            }

            rb.linearVelocity =
                new Vector3(
                    slideDirection.x,
                    velocity.y,
                    slideDirection.z
                );
        }

        slideDirection.y = 0f;

        if (slideDirection.sqrMagnitude < 0.01f)
            return;

        slideDirection.Normalize();

        // La dirección deseada también es relativa
        // a la cámara.
        Vector3 desiredDirection =
            GetInputDirection();

        if (desiredDirection.sqrMagnitude > 0.01f)
        {
            desiredDirection.Normalize();

            slideDirection =
                Vector3.Slerp(
                    slideDirection,
                    desiredDirection,
                    slideTurnStrength *
                    Time.fixedDeltaTime
                );

            slideDirection.Normalize();
        }

        rb.AddForce(
            slideDirection * slideAcceleration,
            ForceMode.Acceleration
        );

        LimitHorizontalSpeed(maxSlideSpeed);
    }

    private void HandleLanding()
    {
        if (!grounded || wasGrounded)
            return;

        PerformGroundAttack();

        if (!ctrlHeld)
        {
            Bounce();
        }
        else
        {
            StartSlide();
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
        Vector3 velocity =
            rb.linearVelocity;

        velocity.y = 0f;

        rb.linearVelocity = velocity;

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.VelocityChange
        );
    }

    private void StartSlide()
    {
        Vector3 velocity =
            rb.linearVelocity;

        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        if (horizontalVelocity.sqrMagnitude < 0.1f)
        {
            Vector3 inputDirection =
                GetInputDirection();

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                horizontalVelocity =
                    inputDirection.normalized * 4f;
            }
            else
            {
                Vector3 forward = model != null
                    ? model.forward
                    : transform.forward;

                forward.y = 0f;

                if (forward.sqrMagnitude > 0.01f)
                {
                    horizontalVelocity =
                        forward.normalized * 4f;
                }
            }
        }

        rb.linearVelocity =
            new Vector3(
                horizontalVelocity.x,
                0f,
                horizontalVelocity.z
            );
    }

    private void PerformGroundAttack()
    {
        if (isDead)
            return;

        if (animator != null)
        {
            animator.SetTrigger(AttackTrigger);
        }

        Vector3 impactPosition =
            new Vector3(
                transform.position.x,
                capsule.bounds.min.y + 0.05f,
                transform.position.z
            );

        if (impactEffectPrefab != null)
        {
            Instantiate(
                impactEffectPrefab,
                impactPosition,
                Quaternion.identity
            );
        }

        Collider[] hits =
            Physics.OverlapSphere(
                impactPosition,
                attackRadius,
                enemyLayer,
                QueryTriggerInteraction.Ignore
            );

        foreach (Collider hit in hits)
        {
            Zombie zombie =
                hit.GetComponentInParent<Zombie>();

            if (zombie == null)
                continue;

            zombie.TakeDamage(attackDamage);
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;

        Debug.Log(
            $"PogoPlayer recibió {damage} de daño. " +
            $"Vida: {currentHealth}/{maxHealth}"
        );

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        rb.linearVelocity =
            Vector3.zero;

        if (animator != null)
        {
            animator.SetTrigger(
                DeathTrigger
            );
        }

        Debug.Log(
            "PogoPlayer murió."
        );
    }

    private void CheckGround()
    {
        Vector3 origin =
            transform.position +
            Vector3.up * 0.05f;

        float radius =
            capsule.radius * 0.9f;

        float distance =
            capsule.height / 2f -
            capsule.radius +
            groundCheckDistance;

        grounded =
            Physics.SphereCast(
                origin,
                radius,
                Vector3.down,
                out _,
                distance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );
    }

    private void LimitHorizontalSpeed(
        float maxSpeed)
    {
        Vector3 velocity =
            rb.linearVelocity;

        Vector3 horizontal =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        if (horizontal.magnitude <= maxSpeed)
            return;

        horizontal =
            horizontal.normalized *
            maxSpeed;

        rb.linearVelocity =
            new Vector3(
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

        currentTilt =
            Mathf.Lerp(
                currentTilt,
                targetTilt,
                diveTiltSpeed *
                Time.deltaTime
            );

        Vector3 horizontalVelocity =
            new Vector3(
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
                movementRotation *
                tiltRotation;
        }
        else
        {
            Quaternion tiltRotation =
                Quaternion.Euler(
                    currentTilt,
                    model.eulerAngles.y,
                    0f
                );

            model.rotation =
                Quaternion.Slerp(
                    model.rotation,
                    tiltRotation,
                    diveTiltSpeed *
                    Time.deltaTime
                );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (capsule == null)
            capsule =
                GetComponent<CapsuleCollider>();

        Vector3 position =
            transform.position;

        if (capsule != null)
        {
            position.y =
                capsule.bounds.min.y + 0.05f;
        }

        Gizmos.DrawWireSphere(
            position,
            attackRadius
        );
    }
}
