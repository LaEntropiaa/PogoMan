using PogoMan.Rebote;
using UnityEngine;

// Adaptador del Ojo Alado (kit Assets/PogoMan/Rebote) al sistema de enemigos del juego.
//  - Una sola autoridad de vida: este script (campos currentHealth/maxHealth/isDead, los que leen
//    PogoBridge, EnemyTracker y EnemyDirector). OjoAlado solo recibe una copia para su animación.
//  - El vuelo, la puntería y la embestida los hace OjoAlado; aquí no hay FollowTarget.
//  - Su collider es trigger: el pisotón de PogoPlayer no lo ve, así que el golpe del pogo se
//    detecta aquí (al caer encima o al rebotar cerca, igual que el radio del pogo con los demonios).
//  - Las recompensas y el contador salen de EnemyTracker (una vez, al morir).
[RequireComponent(typeof(OjoAlado))]
public class OjoEnemy : Enemy
{
    public static int Alive { get; private set; }

    [Header("Vida y daño (el EnemyDirector los escala con el tiempo)")]
    [SerializeField] private float maxHealth = 45f;
    [SerializeField] private float damage = 8f;
    [SerializeField] private float knockback = 6f;

    [Header("Golpe del pogo")]
    [Tooltip("Distancia horizontal de los pies al ojo para contar que le cayó encima.")]
    [SerializeField] private float stompReach = 1.1f;
    [SerializeField] private float stompBounce = 11f;

    private float currentHealth;
    private bool isDead;
    private OjoAlado ojo;
    private SphereCollider sphere;
    private PogoPlayer pogo;
    private Rigidbody pogoBody;
    private bool pogoWasGrounded;
    private float stompCooldown;

    public bool IsDead => isDead;

    protected override void Awake()
    {
        base.Awake();
        ojo = GetComponent<OjoAlado>();
        sphere = GetComponent<SphereCollider>();
        currentHealth = maxHealth;
        ojo.alAcertar.AddListener(OnHitPlayer);   // una sola vez: la instancia no se reutiliza
    }

    private void OnEnable() => Alive++;
    private void OnDisable() => Alive--;

    private void Start()
    {
        // El director ya puso maxHealth/currentHealth/moveSpeed/damage escalados.
        ojo.ConfigurarVida(maxHealth, currentHealth);
        ojo.velocidad = moveSpeed;
        ojo.dano = damage;
        if (target != null)
            ojo.objetivo = target;
    }

    private Vector3 Center => transform.TransformPoint(sphere.center);

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (target != null && ojo.objetivo != target)
            ojo.objetivo = target;

        // TimeStop apaga y prende OjoAlado y su OnEnable le devuelve la vida completa: se re-sincroniza.
        if (!Mathf.Approximately(ojo.Vida, currentHealth))
            ojo.ConfigurarVida(maxHealth, currentHealth);

        CheckPogo();
    }

    private void CheckPogo()
    {
        if (pogo == null)
        {
            pogo = target != null ? target.GetComponentInParent<PogoPlayer>() : FindAnyObjectByType<PogoPlayer>();
            if (pogo == null)
                return;
            pogoBody = pogo.GetComponent<Rigidbody>();
        }

        stompCooldown -= Time.fixedDeltaTime;
        bool grounded = PogoBridge.IsGrounded(pogo);
        Vector3 feet = pogo.transform.position + Vector3.down * 1f;   // la cápsula mide 2 m y su centro está en el origen
        Vector3 center = Center;
        float hit = PogoBridge.GetFloat(pogo, "attackDamage", 50f);

        if (grounded && !pogoWasGrounded)
        {
            // Rebote en el suelo: mismo radio que usa PogoPlayer con los demonios.
            float radius = PogoBridge.GetFloat(pogo, "attackRadius", 2.5f);
            if (Vector3.Distance(center, feet) - sphere.radius <= radius)
                TakeDamage(hit);
        }
        else if (!grounded && stompCooldown <= 0f && pogoBody != null && pogoBody.linearVelocity.y < -1f)
        {
            // Le cae encima en el aire: daño y Pogoman rebota en el ojo.
            Vector3 flat = feet - center;
            float above = flat.y;
            flat.y = 0f;
            if (flat.magnitude <= stompReach && above > -0.25f && above < 0.9f)
            {
                stompCooldown = 0.4f;
                TakeDamage(hit);
                Vector3 v = pogoBody.linearVelocity;
                v.y = stompBounce;
                pogoBody.linearVelocity = v;
            }
        }

        pogoWasGrounded = grounded;
    }

    // Lo llaman el escupitajo, los objetos (EnemyTracker.Hit) y el pogo.
    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        ojo.RecibirDano(amount);   // interrumpe el ataque y reproduce Herido/Caer

        if (currentHealth <= 0f)
        {
            isDead = true;
            if (ojo.Vida > 0f)
                ojo.RecibirDano(ojo.Vida);
            Destroy(gameObject, 1.3f);   // OjoAlado cae 1 s y se apaga solo
        }
    }

    private void OnHitPlayer(Transform victim, float amount)
    {
        if (isDead || pogo == null)
            return;

        PlayerVitals.Damage(pogo, damage);

        if (pogoBody != null)
        {
            Vector3 away = victim.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f)
                pogoBody.linearVelocity += away.normalized * knockback + Vector3.up * 3f;
        }
    }

    // El director lo mueve cuando se queda muy lejos de Pogoman.
    public void Teleport(Vector3 position)
    {
        rb.position = position;
        transform.position = position;
    }
}
