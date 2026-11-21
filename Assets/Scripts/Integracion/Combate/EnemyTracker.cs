using System.Collections.Generic;
using UnityEngine;

// Va en cada enemigo. Sin tocar el script del enemigo:
//  - lo registra para que el escupitajo pueda apuntarle,
//  - detecta cuando pierde vida (por escupitajo o por caída del pogo) y muestra el número de daño,
//  - cuenta la muerte en las estadísticas,
//  - anima la figura provisional (mirar al jugador, tambalearse, parpadear al recibir daño).
[RequireComponent(typeof(Enemy))]
public class EnemyTracker : MonoBehaviour
{
    public static readonly List<EnemyTracker> All = new List<EnemyTracker>();

    [SerializeField] private bool isBoss;
    [SerializeField] private string displayName = "DEMONIO";
    [Tooltip("Figura visual (hijo) que se gira hacia el jugador y se anima.")]
    [SerializeField] private Transform figure;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private AudioClip hitClip;
    [Tooltip("Gruñido que suena cada tanto (sonido 3D).")]
    [SerializeField] private AudioClip groanClip;
    [SerializeField] private Vector2 groanInterval = new Vector2(4f, 9f);

    [Header("Monedas al morir")]
    [SerializeField] private Coin coinPrefab;
    [SerializeField] private int coinsMin = 1;
    [SerializeField] private int coinsMax = 3;

    public bool IsBoss => isBoss;
    public string DisplayName => displayName;
    public Enemy Enemy { get; private set; }
    public float Health => PogoBridge.Health(Enemy);
    public float MaxHealth => PogoBridge.MaxHealth(Enemy);
    public bool IsDead => Enemy == null || PogoBridge.IsDead(Enemy);
    public float LastDamageTime { get; private set; } = -100f;

    public Vector3 Center => mainCollider != null ? mainCollider.bounds.center : transform.position;
    public Vector3 Top => mainCollider != null
        ? new Vector3(Center.x, mainCollider.bounds.max.y, Center.z)
        : transform.position + Vector3.up * 2f;

    private Collider mainCollider;
    private Renderer[] renderers;
    private Color[] baseEmission;
    private MaterialPropertyBlock block;
    private float lastHealth;
    private bool deathHandled;
    private float pendingSpitUntil;
    private bool pendingCrit;
    private float flash;
    private float deathTimer;
    private Vector3 lastPosition;
    private Vector3 figureBaseScale = Vector3.one;
    private Vector3 figureBasePosition;
    private float walkPhase;

    private void Awake()
    {
        Enemy = GetComponent<Enemy>();
        mainCollider = GetComponent<Collider>();
        renderers = (figure != null ? figure : transform).GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();

        // Guardamos el brillo original (por ejemplo, los ojos) para sumarle el destello.
        baseEmission = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material m = renderers[i].sharedMaterial;
            baseEmission[i] = m != null && m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
        }

        if (figure != null)
        {
            figureBaseScale = figure.localScale;
            figureBasePosition = figure.localPosition;   // p. ej. el modelo va 0.95 m abajo para que pise el suelo
        }
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private AudioSource voice;
    private float groanTimer;
    private float lastHitSound;

    private void Start()
    {
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;                  // 3D: se oye más fuerte cerca
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 2f;
        voice.maxDistance = 28f;
        voice.pitch = isBoss ? 0.7f : Random.Range(0.9f, 1.1f);
        groanTimer = Random.Range(1f, groanInterval.y);

        lastHealth = Health;
        lastPosition = transform.position;
    }

    // El escupitajo avisa justo antes de hacer daño, para saber si fue crítico.
    public void MarkIncomingSpit(bool crit)
    {
        pendingSpitUntil = Time.time + 0.25f;
        pendingCrit = crit;
    }

    private void LateUpdate()
    {
        if (groanClip != null && voice != null && !deathHandled)
        {
            groanTimer -= Time.deltaTime;
            if (groanTimer <= 0f)
            {
                groanTimer = Random.Range(groanInterval.x, groanInterval.y);
                voice.PlayOneShot(groanClip, 0.7f * GameSettings.SfxVolume);
            }
        }

        float health = Health;

        if (health < lastHealth - 0.01f)
        {
            float damage = lastHealth - health;
            bool fromSpit = Time.time < pendingSpitUntil;
            bool crit = fromSpit && pendingCrit;
            pendingSpitUntil = 0f;

            DamageNumbers.Show(Top + Vector3.up * 0.3f, damage,
                crit ? DamageKind.Crit : fromSpit ? DamageKind.Spit : DamageKind.Stomp);
            PlayerStats.Instance?.RegisterDamage(damage, crit);
            LastDamageTime = Time.time;
            flash = 1f;

            if (hitClip != null && Time.time - lastHitSound > 0.08f)
            {
                lastHitSound = Time.time;
                voice.PlayOneShot(hitClip, 0.9f * GameSettings.SfxVolume);
            }
        }

        lastHealth = health;

        if (!deathHandled && IsDead)
        {
            deathHandled = true;
            PlayerStats.Instance?.RegisterKill();
            DropCoins();
            if (deathClip != null)
                AudioSource.PlayClipAtPoint(deathClip, Center, 0.8f * GameSettings.SfxVolume);
        }

        AnimateFigure();
        ApplyFlash();
    }

    private void DropCoins()
    {
        if (coinPrefab == null)
            return;

        int count = Random.Range(coinsMin, coinsMax + 1);
        for (int i = 0; i < count; i++)
            Instantiate(coinPrefab, Center + Random.insideUnitSphere * 0.3f, Quaternion.Euler(90f, 0f, 0f));
    }

    private void AnimateFigure()
    {
        if (figure == null)
            return;

        if (deathHandled)
        {
            // Se encoge y se hunde (el enemigo se destruye al segundo).
            deathTimer += Time.deltaTime;
            float k = Mathf.Clamp01(deathTimer / 0.8f);
            figure.localScale = figureBaseScale * (1f - k * k);
            figure.localRotation *= Quaternion.Euler(0f, 720f * Time.deltaTime, 0f);
            return;
        }

        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        moved.y = 0f;
        float speed = moved.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        // Mirar hacia donde camina.
        if (moved.sqrMagnitude > 0.00001f)
        {
            Quaternion look = Quaternion.LookRotation(moved.normalized, Vector3.up);
            figure.rotation = Quaternion.Slerp(figure.rotation, look, 10f * Time.deltaTime);
        }

        // Tambaleo de zombie al caminar.
        walkPhase += speed * Time.deltaTime * 3f;
        float sway = Mathf.Sin(walkPhase) * Mathf.Clamp01(speed / 2f);
        figure.localPosition = figureBasePosition + new Vector3(0f, Mathf.Abs(sway) * 0.08f, 0f);
        Vector3 euler = figure.localEulerAngles;
        figure.localRotation = Quaternion.Euler(0f, euler.y, sway * 8f);

        // Pequeño "pop" al recibir daño.
        figure.localScale = figureBaseScale * (1f + flash * 0.12f);
    }

    private void ApplyFlash()
    {
        if (flash <= 0f && !deathHandled)
            return;

        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 6f);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;
            r.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", baseEmission[i] + Color.white * flash * 2f);
            r.SetPropertyBlock(block);
        }
    }
}
