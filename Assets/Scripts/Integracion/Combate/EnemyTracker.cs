using System.Collections.Generic;
using UnityEngine;

// Va en cada enemigo. Sin tocar el script del enemigo:
//  - lo registra para que el escupitajo pueda apuntarle,
//  - detecta cuando pierde vida (por escupitajo o por caída del pogo) y muestra el número de daño,
//  - cuenta la muerte en las estadísticas,
//  - anima la figura provisional (mirar al jugador, tambalearse, parpadear al recibir daño).
// De dónde vino un golpe (lo usan los objetos: "al golpear con el pogo", "al matar con el pogo"...).
public enum HitSource { Stomp, Spit, Item, Other, Wave }

[RequireComponent(typeof(Enemy))]
public class EnemyTracker : MonoBehaviour
{
    public static readonly List<EnemyTracker> All = new List<EnemyTracker>();

    // Golpe del pogo (caída) a este enemigo, con el daño hecho.
    public static event System.Action<EnemyTracker, float> StompHit;
    // Murió; el origen es el del último golpe que recibió.
    public static event System.Action<EnemyTracker, HitSource> Killed;

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
    [Tooltip("Probabilidad de soltar monedas (con cientos de enemigos, no todos sueltan).")]
    [SerializeField, Range(0f, 1f)] private float coinChance = 0.4f;
    // Modo infinito: más probabilidad de soltar monedas.
    public static float CoinChanceBonus;

    [Header("Experiencia al morir")]
    [SerializeField] private float xpValue = 1f;
    [SerializeField] private float bossXpValue = 25f;
    [Tooltip("Probabilidad de que suelte una gema verde (vale 5).")]
    [SerializeField, Range(0f, 1f)] private float bigGemChance = 0.05f;

    // Con cientos de demonios no pueden gruñir todos: solo los cercanos y unos cuantos por segundo.
    private const float GroanDistance = 22f;
    private const float GroanSpacing = 0.35f;
    private static float nextGroanAllowed;

    public bool IsBoss => isBoss;
    // false = el escupitajo y los objetos no lo apuntan (p. ej. el Diablo mientras es invulnerable).
    public bool Targetable { get; set; } = true;
    // Tiene su propia barra de vida (el Diablo): EnemyHealthBars no le dibuja nada.
    public bool CustomUi { get; set; }
    public string DisplayName => displayName;
    public Enemy Enemy { get; private set; }
    public float Health => PogoBridge.Health(Enemy);
    public float MaxHealth => PogoBridge.MaxHealth(Enemy);
    public bool IsDead => Enemy == null || PogoBridge.IsDead(Enemy);
    public float LastDamageTime { get; private set; } = -100f;
    // Muertes que no cuentan (onda del pilar, ataques de los guardianes): sin experiencia, monedas ni puntos.
    public bool NoReward { get; set; }
    // Élite (corona): más experiencia y monedas, y a veces un cofre (EliteEnemy).
    public bool IsElite { get; set; }
    public static event System.Action<EnemyTracker> EliteKilled;

    // Daño que no da recompensa si lo mata (si sobrevive, se le quita la marca).
    public void HitNoReward(float damage, HitSource source = HitSource.Other)
    {
        if (IsDead) return;
        NoReward = true;
        Hit(damage, source);
        if (!IsDead) NoReward = false;
    }

    private static readonly List<EnemyTracker> areaBuffer = new List<EnemyTracker>();

    // Golpe en área a los demonios (horizontal, con margen de altura). Regresa cuántos tocó.
    public static int HitArea(Vector3 center, float radius, float damage, HitSource source, bool noReward,
        float maxHeight = 3f, Enemy except = null, bool skipBosses = false)
    {
        areaBuffer.Clear();
        float r2 = radius * radius;
        foreach (EnemyTracker e in All)
        {
            if (e == null || e.IsDead || e.Enemy == except || (skipBosses && e.IsBoss)) continue;
            Vector3 d = e.Center - center;
            if (Mathf.Abs(d.y) > maxHeight) continue;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) areaBuffer.Add(e);
        }
        foreach (EnemyTracker e in areaBuffer)
        {
            if (noReward) e.HitNoReward(damage, source);
            else e.Hit(damage, source);
        }
        return areaBuffer.Count;
    }

    // Un collider apagado (el Ojo Alado al morir) da bounds vacíos en el origen.
    public Vector3 Center => mainCollider != null && mainCollider.enabled ? mainCollider.bounds.center : transform.position + Vector3.up * 0.7f;
    public Vector3 Top => mainCollider != null && mainCollider.enabled
        ? new Vector3(Center.x, mainCollider.bounds.max.y, Center.z)
        : transform.position + Vector3.up * 2f;

    private Collider mainCollider;
    private Renderer[] renderers;
    private Color[] baseEmission;
    private MaterialPropertyBlock block;
    private float lastHealth;
    private bool deathHandled;
    private HitSource lastSource = HitSource.Stomp;
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
    // Daño que no es del pogo (escupitajo, objetos, lava...): se aplica y se cuenta en el momento,
    // así un pisotón en el mismo frame no se confunde con otro origen.
    public void Hit(float damage, HitSource source = HitSource.Item, bool crit = false)
    {
        if (IsDead || damage <= 0f)
            return;

        float before = Health;
        Enemy.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        float dealt = before - Health;
        if (dealt <= 0.01f)
            return;

        lastHealth -= dealt;   // ya contado: LateUpdate no lo toma como pisotón
        lastSource = source;
        DamageKind kind = crit ? DamageKind.Crit : source == HitSource.Spit ? DamageKind.Spit : DamageKind.Item;
        OnDamaged(dealt, kind, crit);
    }

    private void OnDamaged(float damage, DamageKind kind, bool crit)
    {
        DamageNumbers.Show(Top + Vector3.up * 0.3f, damage, kind);
        PlayerStats.Instance?.RegisterDamage(damage, crit);
        LastDamageTime = Time.time;
        flash = 1f;

        if (hitClip != null && voice != null && Time.time - lastHitSound > 0.08f)
        {
            lastHitSound = Time.time;
            voice.PlayOneShot(hitClip, 0.9f * GameSettings.SfxVolume);
        }
    }

    private void LateUpdate()
    {
        if (groanClip != null && voice != null && !deathHandled)
        {
            groanTimer -= Time.deltaTime;
            if (groanTimer <= 0f)
            {
                groanTimer = Random.Range(groanInterval.x, groanInterval.y);
                Camera cam = Camera.main;
                bool near = cam == null || (cam.transform.position - transform.position).sqrMagnitude < GroanDistance * GroanDistance;
                if (near && Time.time >= nextGroanAllowed)
                {
                    nextGroanAllowed = Time.time + GroanSpacing;
                    voice.PlayOneShot(groanClip, 0.7f * GameSettings.SfxVolume);
                }
            }
        }

        float health = Health;

        // Lo que bajó sin pasar por Hit() es el golpe del pogo (PogoPlayer llama TakeDamage directo).
        if (health < lastHealth - 0.01f)
        {
            float damage = lastHealth - health;
            lastSource = HitSource.Stomp;
            OnDamaged(damage, DamageKind.Stomp, false);
            StompHit?.Invoke(this, damage);
        }

        lastHealth = health;

        if (!deathHandled && IsDead)
        {
            deathHandled = true;
            if (!NoReward)
            {
                Killed?.Invoke(this, lastSource);
                PlayerStats.Instance?.RegisterKill();
                DropCoins();
                DropXp();
                if (IsElite) EliteKilled?.Invoke(this);
            }
            if (deathClip != null)
                AudioSource.PlayClipAtPoint(deathClip, Center, 0.8f * GameSettings.SfxVolume);
        }

        AnimateFigure();
        ApplyFlash();
    }

    private void DropCoins()
    {
        if (coinPrefab == null || (!isBoss && !IsElite && Random.value > Mathf.Min(0.95f, coinChance + CoinChanceBonus)))
            return;

        int count = Random.Range(coinsMin, coinsMax + 1) * (IsElite ? 3 : 1);
        for (int i = 0; i < count; i++)
            Instantiate(coinPrefab, Center + Random.insideUnitSphere * 0.3f, Quaternion.Euler(90f, 0f, 0f));
    }

    private void DropXp()
    {
        if (isBoss)
        {
            XpGem.Drop(Center, bossXpValue);
            return;
        }
        if (IsElite)
        {
            XpGem.Drop(Center, 5f);
            XpGem.Drop(Center + Vector3.up * 0.3f, 5f);
            return;
        }
        XpGem.Drop(Center, Random.value < bigGemChance ? 5f : xpValue);
    }

    // Contraataque por pararse en su cabeza (HeadStandPunish lo llama con SendMessage).
    private float punish;
    public void OnHeadPunish() => punish = 1f;
    public float Punish => punish;
    private float idleTime;

    private void AnimateFigure()
    {
        if (figure == null)
            return;
        punish = Mathf.MoveTowards(punish, 0f, Time.deltaTime * 2.2f);

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

        // Tambaleo al caminar (también lo hace el modelo de lejos, que no tiene huesos).
        walkPhase += speed * Time.deltaTime * 3f;
        float walking = Mathf.Clamp01(speed / 2f);
        float sway = Mathf.Sin(walkPhase) * walking;
        idleTime += Time.deltaTime;
        float breathe = Mathf.Sin(idleTime * 2.1f + walkPhase * 0.1f) * (1f - walking);

        // Reacciones: se echa para atrás al recibir daño y se zarandea cuando te quita de su cabeza.
        float flinch = flash * flash;
        float shake = punish > 0f ? Mathf.Sin(idleTime * 42f) * punish : 0f;
        figure.localPosition = figureBasePosition + new Vector3(0f, Mathf.Abs(sway) * 0.08f + punish * 0.15f, 0f);
        Vector3 euler = figure.localEulerAngles;
        figure.localRotation = Quaternion.Euler(-flinch * 14f + breathe * 2f + punish * -10f, euler.y, sway * 8f + shake * 18f);

        // "Pop" al recibir daño, respiración quieto y estirón al contraatacar.
        float stretch = 1f + punish * 0.25f + breathe * 0.025f;
        float wide = 1f + flash * 0.12f + (1f - stretch) * 0.5f;
        figure.localScale = new Vector3(figureBaseScale.x * wide, figureBaseScale.y * stretch * (1f + flash * 0.06f), figureBaseScale.z * wide);
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
