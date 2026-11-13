using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PogoCapsule : MonoBehaviour
{
    [Header("Normal")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float airControl = 18f;
    [SerializeField] private float maxAirSpeed = 7f;

    [Header("Slide")]
    [SerializeField] private float slideSpeed = 18f;
    [SerializeField] private float slideControl = 3f;
    [SerializeField] private float diveForce = 35f;
    [SerializeField] private float slideTilt = 70f;

    [Header("Ground")]
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private LayerMask groundLayer = ~0;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private bool isGrounded;
    private bool wasGrounded;
    private bool sliding;

    private Vector3 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(horizontal, 0f, vertical).normalized;

        sliding = Input.GetKey(KeyCode.LeftControl) ||
                  Input.GetKey(KeyCode.RightControl);
    }

    private void FixedUpdate()
    {
        CheckGround();

        if (sliding)
        {
            SlideMovement();
        }
        else
        {
            NormalMovement();
        }

        HandleLanding();
    }

    private void NormalMovement()
    {
        // Control aéreo relativamente difícil.
        Vector3 desiredDirection = moveInput * airControl;

        Vector3 horizontalVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        if (horizontalVelocity.magnitude < maxAirSpeed)
        {
            rb.AddForce(desiredDirection, ForceMode.Acceleration);
        }
    }

    private void SlideMovement()
    {
        // Si todavía estamos en el aire,
        // aceleramos hacia abajo.
        if (!isGrounded)
        {
            rb.AddForce(Vector3.down * diveForce, ForceMode.Acceleration);
        }

        // Dirección hacia donde queremos movernos.
        Vector3 forward = transform.forward;

        // Poco control lateral mientras deslizamos.
        Vector3 lateral = transform.right * moveInput.x;

        Vector3 direction = (forward + lateral * slideControl).normalized;

        rb.AddForce(direction * slideSpeed, ForceMode.Acceleration);

        // Limitar velocidad horizontal.
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontal = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        if (horizontal.magnitude > slideSpeed)
        {
            horizontal = horizontal.normalized * slideSpeed;

            rb.linearVelocity = new Vector3(
                horizontal.x,
                velocity.y,
                horizontal.z
            );
        }

        // Inclinar visualmente la cápsula.
        RotateForSlide();
    }

    private void HandleLanding()
    {
        // Acabamos de tocar el suelo.
        if (isGrounded && !wasGrounded)
        {
            if (!sliding)
            {
                Bounce();
            }
            else
            {
                // Rebote mucho más pequeño mientras deslizamos.
                rb.AddForce(Vector3.up * jumpForce * 0.35f,
                    ForceMode.VelocityChange);
            }
        }

        wasGrounded = isGrounded;
    }

    private void Bounce()
    {
        // Eliminar velocidad vertical anterior.
        Vector3 velocity = rb.linearVelocity;

        rb.linearVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    private void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.05f;

        float radius = capsule.radius * 0.9f;

        float distance =
            capsule.height / 2f - capsule.radius + groundCheckDistance;

        isGrounded = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            distance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    private void RotateForSlide()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontal = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        if (horizontal.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(horizontal.normalized);

        targetRotation *= Quaternion.Euler(
            slideTilt,
            0f,
            0f
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            10f * Time.fixedDeltaTime
        );
    }
}
