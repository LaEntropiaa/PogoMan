using System.Collections.Generic;
using UnityEngine;

// Animación por código para Pogoman (el modelo todavía no tiene esqueleto conectado).
//  - Mira hacia donde se mueve.
//  - Se aplasta al rebotar y se estira en el aire (efecto resorte).
//  - Deslizarse (Ctrl en el piso): se echa hacia atrás con los pies por delante, levantando polvo.
//  - Picada (Ctrl en el aire): se encoge y apunta el pogo hacia abajo.
//  - Salto cargado: tiembla mientras se carga.
//  - Daño: tiembla y se pone rojo. Muerte: se cae de lado.
//
// IMPORTANTE: en la escena, el campo "Model" de PogoPlayer va vacío para que
// PogoPlayer no gire el modelo; de eso se encarga este script.
public class PogoAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto que gira hacia donde se mueve (antes lo giraba PogoPlayer).")]
    [SerializeField] private Transform facingRoot;
    [Tooltip("El modelo 3D (hijo de facingRoot).")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform body;
    [SerializeField] private Material dustMaterial;

    [Header("Aplastar y estirar")]
    [SerializeField] private float squashOnBounce = 0.3f;
    [SerializeField] private float stretchPerSpeed = 0.018f;
    [SerializeField] private float maxStretch = 0.2f;
    [SerializeField] private float springStiffness = 220f;
    [SerializeField] private float springDamping = 14f;
    [SerializeField] private float bodySink = 0.35f;

    [Header("Poses")]
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float poseSpeed = 10f;
    [SerializeField] private float leanPerSpeed = 1.3f;
    [SerializeField] private float maxLean = 14f;
    [Tooltip("Ángulo hacia atrás al deslizarse (pies por delante).")]
    [SerializeField] private float slideLean = -62f;
    [SerializeField] private float slideSquash = 0.85f;
    [Tooltip("Inclinación hacia adelante en la picada.")]
    [SerializeField] private float diveLean = 18f;
    [Tooltip("Vaivén lateral mientras se desliza (grados).")]
    [SerializeField] private float slideSway = 4f;
    [Tooltip("Se encoge un poco justo antes de tocar el piso.")]
    [SerializeField] private float landingAnticipation = 0.1f;

    [Header("Daño y muerte")]
    [SerializeField] private float hurtShake = 0.07f;
    [SerializeField] private float hurtDuration = 0.35f;
    [SerializeField] private Color hurtColor = new Color(1f, 0.15f, 0.1f);

    private Rigidbody rb;
    private PogoPlayer player;
    private PogoJump jump;
    private Vector3 rootBaseScale, rootBasePosition, bodyBasePosition;
    private Quaternion rootBaseRotation;
    private float scaleY = 1f, scaleVelocity;
    private float lastVerticalSpeed;
    private float lastHealth;
    private float hurtTimer;
    private float pitch;
    private float yaw;
    private float deathTimer = -1f;
    private ParticleSystem dust;
    private bool wasSliding;
    private float roll;

    private Renderer[] renderers;
    private readonly List<Color[]> baseColors = new List<Color[]>();
    private MaterialPropertyBlock block;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PogoPlayer>();
        jump = GetComponent<PogoJump>();
        block = new MaterialPropertyBlock();

        if (facingRoot != null)
            yaw = facingRoot.eulerAngles.y;

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

        CreateDust();
    }

    private void Start() => lastHealth = PogoBridge.Health(player);

    private void FixedUpdate()
    {
        float vy = rb.linearVelocity.y;

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

        Vector3 v = rb.linearVelocity;
        Vector3 horizontal = new Vector3(v.x, 0f, v.z);
        float speed = horizontal.magnitude;
        bool sliding = player != null && player.IsSliding;
        bool diving = !sliding && PogoBridge.IsCtrlHeld(player) && !PogoBridge.IsGrounded(player);

        // ---- Mirar hacia donde se mueve ----
        if (speed > 0.5f)
            yaw = Mathf.LerpAngle(yaw, Quaternion.LookRotation(horizontal).eulerAngles.y, 1f - Mathf.Exp(-turnSpeed * dt));

        float targetPitch = sliding ? slideLean
                          : diving ? diveLean
                          : Mathf.Min(speed * leanPerSpeed, maxLean);
        pitch = Mathf.Lerp(pitch, targetPitch, 1f - Mathf.Exp(-poseSpeed * dt));

        // Al empezar a deslizarse: aplastón y nube de polvo.
        if (sliding && !wasSliding)
        {
            scaleY = Mathf.Min(scaleY, 0.72f);
            scaleVelocity = 0f;
            if (dust != null && speed > 1f)
                dust.Emit(20);
        }
        wasSliding = sliding;

        // Vaivén suave mientras se desliza.
        float targetRoll = sliding ? Mathf.Sin(Time.time * 7f) * slideSway * Mathf.Clamp01(speed / 6f) : 0f;
        roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-12f * dt));

        if (facingRoot != null)
            facingRoot.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, roll);

        // ---- Resorte de aplastar/estirar ----
        float target;
        if (sliding) target = slideSquash;
        else if (diving) target = 1f + maxStretch;          // estirado como flecha hacia abajo
        else if (v.y < -3f && NearGround(0.7f)) target = 1f - landingAnticipation;   // anticipación del aterrizaje
        else target = 1f + Mathf.Clamp(Mathf.Abs(v.y) * stretchPerSpeed, 0f, maxStretch);

        float accel = (target - scaleY) * springStiffness - scaleVelocity * springDamping;
        scaleVelocity += accel * dt;
        scaleY = Mathf.Clamp(scaleY + scaleVelocity * dt, 0.55f, 1.4f);

        float side = 1f / Mathf.Sqrt(scaleY);
        visualRoot.localScale = Vector3.Scale(rootBaseScale, new Vector3(side, scaleY, side));

        if (body != null)
            body.localPosition = bodyBasePosition + Vector3.up * (scaleY - 1f) * bodySink;

        // ---- Daño ----
        float health = PogoBridge.Health(player);
        if (health < lastHealth - 0.01f)
            hurtTimer = hurtDuration;
        lastHealth = health;

        hurtTimer = Mathf.Max(0f, hurtTimer - dt);
        float hurt = hurtTimer / hurtDuration;

        // Temblor por daño o mientras se carga el salto agachado.
        float charge = jump != null ? jump.CrouchCharge : 0f;
        float shakeAmount = hurtShake * hurt + 0.025f * charge;
        Vector3 shake = shakeAmount > 0f ? Random.insideUnitSphere * shakeAmount : Vector3.zero;
        visualRoot.localPosition = rootBasePosition + shake;
        visualRoot.localRotation = rootBaseRotation;

        Tint(hurt);
        UpdateDust(sliding, speed);
    }

    private bool NearGround(float distance)
    {
        // El centro de Pogoman está ~1 m sobre sus pies.
        return Physics.Raycast(transform.position, Vector3.down, 1f + distance, ~0, QueryTriggerInteraction.Ignore);
    }

    private void CreateDust()
    {
        var go = new GameObject("PolvoDeslizar");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.95f, 0f);

        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startColor = new Color(0.45f, 0.28f, 0.2f, 0.55f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = dust.emission;
        emission.rateOverTime = 0f;

        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.3f;

        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        var color = dust.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        if (dustMaterial != null)
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = dustMaterial;

        dust.Play();
    }

    private void UpdateDust(bool sliding, float speed)
    {
        if (dust == null)
            return;
        var emission = dust.emission;
        emission.rateOverTime = sliding && speed > 2f ? 18f + speed * 3f : 0f;
    }

    private void AnimateDeath(float dt)
    {
        if (deathTimer < 0f)
        {
            deathTimer = 0f;
            Tint(0f);
            UpdateDust(false, 0f);
            if (facingRoot != null)
                facingRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
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
