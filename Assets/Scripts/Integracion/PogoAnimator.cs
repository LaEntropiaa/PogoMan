using System.Collections.Generic;
using UnityEngine;

// Animación por código para Pogoman (el modelo todavía no tiene esqueleto conectado):
//  - se aplasta al rebotar y se estira en el aire (con efecto resorte),
//  - el cuerpo se hunde un poco sobre el pogo al aterrizar,
//  - se inclina según la velocidad,
//  - tiembla y se pone rojo al recibir daño,
//  - se cae de lado al morir.
// No toca el objeto "model" que gira PogoPlayer: anima el modelo que va dentro.
public class PogoAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El modelo 3D (hijo del objeto que gira PogoPlayer).")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform body;

    [Header("Aplastar y estirar")]
    [SerializeField] private float squashOnBounce = 0.3f;
    [SerializeField] private float stretchPerSpeed = 0.018f;
    [SerializeField] private float maxStretch = 0.2f;
    [SerializeField] private float springStiffness = 220f;
    [SerializeField] private float springDamping = 14f;
    [SerializeField] private float bodySink = 0.35f;

    [Header("Inclinación")]
    [SerializeField] private float leanPerSpeed = 1.3f;
    [SerializeField] private float maxLean = 14f;

    [Header("Daño y muerte")]
    [SerializeField] private float hurtShake = 0.07f;
    [SerializeField] private float hurtDuration = 0.35f;
    [SerializeField] private Color hurtColor = new Color(1f, 0.15f, 0.1f);

    private Rigidbody rb;
    private PogoPlayer player;
    private Vector3 rootBaseScale, rootBasePosition, bodyBasePosition;
    private Quaternion rootBaseRotation;
    private float scaleY = 1f, scaleVelocity;
    private float lastVerticalSpeed;
    private float lastHealth;
    private float hurtTimer;
    private float lean;
    private float deathTimer = -1f;

    private Renderer[] renderers;
    private readonly List<Color[]> baseColors = new List<Color[]>();
    private MaterialPropertyBlock block;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PogoPlayer>();
        block = new MaterialPropertyBlock();

        if (visualRoot == null)
            return;

        rootBaseScale = visualRoot.localScale;
        rootBasePosition = visualRoot.localPosition;
        rootBaseRotation = visualRoot.localRotation;
        if (body != null)
            bodyBasePosition = body.localPosition;

        renderers = visualRoot.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            Material[] mats = r.sharedMaterials;
            var colors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                colors[i] = mats[i] != null && mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : Color.white;
            baseColors.Add(colors);
        }
    }

    private void Start() => lastHealth = PogoBridge.Health(player);

    private void FixedUpdate()
    {
        float vy = rb.linearVelocity.y;

        // Rebote: la velocidad vertical salta hacia arriba de golpe.
        if (vy - lastVerticalSpeed > 4f && vy > 2f)
        {
            scaleY = 1f - squashOnBounce;
            scaleVelocity = 0f;
        }

        lastVerticalSpeed = vy;
    }

    private void Update()
    {
        if (visualRoot == null)
            return;

        float dt = Time.deltaTime;

        if (PogoBridge.IsDead(player))
        {
            AnimateDeath(dt);
            return;
        }

        // ---- Resorte de aplastar/estirar ----
        float vy = rb.linearVelocity.y;
        float target = 1f + Mathf.Clamp(Mathf.Abs(vy) * stretchPerSpeed, 0f, maxStretch);
        float accel = (target - scaleY) * springStiffness - scaleVelocity * springDamping;
        scaleVelocity += accel * dt;
        scaleY += scaleVelocity * dt;
        scaleY = Mathf.Clamp(scaleY, 0.55f, 1.4f);

        float side = 1f / Mathf.Sqrt(scaleY);   // conserva el "volumen"
        visualRoot.localScale = Vector3.Scale(rootBaseScale, new Vector3(side, scaleY, side));

        if (body != null)
            body.localPosition = bodyBasePosition + Vector3.up * (scaleY - 1f) * bodySink;

        // ---- Inclinación hacia adelante según la velocidad ----
        Vector3 v = rb.linearVelocity;
        float horizontal = new Vector2(v.x, v.z).magnitude;
        float targetLean = player != null && player.IsSliding ? 0f : Mathf.Min(horizontal * leanPerSpeed, maxLean);
        lean = Mathf.Lerp(lean, targetLean, 8f * dt);

        // ---- Daño ----
        float health = PogoBridge.Health(player);
        if (health < lastHealth - 0.01f)
            hurtTimer = hurtDuration;
        lastHealth = health;

        hurtTimer = Mathf.Max(0f, hurtTimer - dt);
        float hurt = hurtTimer / hurtDuration;

        Vector3 shake = hurt > 0f ? Random.insideUnitSphere * hurtShake * hurt : Vector3.zero;
        visualRoot.localPosition = rootBasePosition + shake;
        visualRoot.localRotation = rootBaseRotation * Quaternion.Euler(lean, 0f, 0f);

        Tint(hurt);
    }

    private void AnimateDeath(float dt)
    {
        if (deathTimer < 0f)
        {
            deathTimer = 0f;
            Tint(0f);
        }

        deathTimer += dt;
        float k = Mathf.Clamp01(deathTimer / 0.6f);
        float ease = 1f - Mathf.Pow(1f - k, 3f);
        float bounce = k < 1f ? 0f : Mathf.Sin(Mathf.Min((deathTimer - 0.6f) * 10f, Mathf.PI)) * 4f;

        visualRoot.localRotation = rootBaseRotation * Quaternion.Euler(0f, 0f, 90f * ease - bounce);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, rootBaseScale, 10f * dt);
    }

    private void Tint(float amount)
    {
        if (renderers == null)
            return;

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer rend = renderers[r];
            Color[] colors = baseColors[r];
            for (int i = 0; i < colors.Length; i++)
            {
                rend.GetPropertyBlock(block, i);
                block.SetColor("_BaseColor", Color.Lerp(colors[i], hurtColor, amount * 0.8f));
                rend.SetPropertyBlock(block, i);
            }
        }
    }
}
