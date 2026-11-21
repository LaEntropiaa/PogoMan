using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

// La arena del Diablo Tendero: aparece al cruzar la puerta (ArenaGate), arma al jefe,
// invoca sus oleadas, muestra su barra de vida y al vencerlo lanza la pantalla de victoria.
public class ArenaFight : MonoBehaviour
{
    public static ArenaFight Instance { get; private set; }
    public static PogoPlayer Player { get; private set; }
    public static bool InArena { get; private set; }

    [Header("Arena (inactiva hasta entrar)")]
    [SerializeField] private GameObject arenaRoot;
    [SerializeField] private Transform playerStart;
    [SerializeField] private Transform bossStart;
    [SerializeField] private Transform arenaCenter;
    [Tooltip("Radio del piso donde pueden salir demonios.")]
    [SerializeField] private float floorRadius = 22f;

    [Header("Diablo")]
    [SerializeField] private GameObject devilModel;
    [SerializeField] private AnimationClip devilIdle;
    [SerializeField] private float devilScale = 2.5f;
    [SerializeField] private Enemy pillarBossPrefab;

    [Header("Efectos")]
    [SerializeField] private Material fireballMaterial;
    [SerializeField] private Material fxMaterial;
    [SerializeField] private Font font;
    [SerializeField] private AudioClip arenaMusic;
    [SerializeField] private AudioClip laughClip;
    [SerializeField] private AudioClip castClip;
    [SerializeField] private AudioClip explodeClip;
    [SerializeField] private AudioClip vulnerableClip;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioClip slamClip;

    public Enemy PillarBossPrefab => pillarBossPrefab;
    public Material FireballMaterial => fireballMaterial;
    public Material FxMaterial => fxMaterial;
    public AudioClip ExplodeClip => explodeClip;
    public Vector3 PlayerStart => playerStart.position;
    public Quaternion PlayerFacing => Quaternion.LookRotation(Flat(bossStart.position - playerStart.position));
    public AudioClip Music => arenaMusic;

    private DevilBoss devil;
    private PlayableGraph graph;
    private AudioSource audioSource;
    private float lastHurt;

    // HUD
    private CanvasGroup hud;
    private Text bossName, bossState, banner, bannerSub;
    private RectTransform bossFill, bossFillLag;
    private float bannerTimer, bannerPop, lagHealth = 1f;

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

    private void Awake()
    {
        Instance = this;
        InArena = false;
        audioSource = UiKit.UiAudio(gameObject);
        if (arenaRoot != null)
            arenaRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (graph.IsValid())
            graph.Destroy();
        InArena = false;
    }

    public void PrepareArena()
    {
        if (arenaRoot != null)
            arenaRoot.SetActive(true);
    }

    // ---- Empieza la pelea (Pogoman ya está dentro) ----
    public void Begin(PogoPlayer player)
    {
        Player = player;
        InArena = true;
        BuildHud();
        SpawnDevil();
        ShowBanner("EL DIABLO TENDERO", "", 3.5f);
        Play(laughClip, 1f, 1f);
    }

    private void SpawnDevil()
    {
        var go = new GameObject("DiabloTendero");
        go.transform.SetPositionAndRotation(bossStart.position, Quaternion.LookRotation(Flat(playerStart.position - bossStart.position)));
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.height = 2.6f * devilScale;
        capsule.radius = 0.45f * devilScale;
        capsule.center = Vector3.up * capsule.height * 0.5f;

        devil = go.AddComponent<DevilBoss>();
        go.AddComponent<EnemyTracker>();

        // Modelo (con su animación de reverencia en bucle).
        Transform model = new GameObject("Modelo").transform;
        model.SetParent(go.transform, false);
        if (devilModel != null)
        {
            GameObject m = Instantiate(devilModel, model);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one * devilScale;
            Animator animator = m.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = m.AddComponent<Animator>();
            if (devilIdle != null)
            {
                graph = PlayableGraph.Create("Diablo");
                var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
                var clip = AnimationClipPlayable.Create(graph, devilIdle);
                output.SetSourcePlayable(clip);
                graph.Play();
            }
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // Aura (morada mientras es invulnerable).
        var aura = model.gameObject.AddComponent<ParticleSystem>();
        aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = aura.main;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = new Color(0.55f, 0.15f, 0.9f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.15f;
        main.maxParticles = 300;
        var em = aura.emission;
        em.rateOverTime = 30f;
        var shape = aura.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.6f, 2.6f, 1.2f) * devilScale;
        shape.position = Vector3.up * 1.3f * devilScale;
        ItemFx.FadeOut(aura);
        ItemFx.Apply(aura, fxMaterial);
        aura.Play();

        devil.PhaseChanged += p =>
        {
            var e = aura.emission;
            var mm = aura.main;
            bool vulnerable = p == DevilBoss.Phase.Vulnerable;
            e.rateOverTime = vulnerable ? 15f : 30f;
            mm.startColor = vulnerable ? new Color(1f, 0.9f, 0.3f, 0.8f) : new Color(0.55f, 0.15f, 0.9f, 0.7f);
        };

        var rig = go.AddComponent<DevilRig>();
        rig.Setup(go.transform, model);
        rig.LookTarget = Player.transform;
        devil.Rig = rig;
        devil.PhaseChanged += p =>
        {
            // Termina la reverencia: desde aquí lo anima DevilRig.
            if (p != DevilBoss.Phase.Intro && graph.IsValid())
                graph.Destroy();
        };

        devil.Setup(this, model, arenaCenter.position, bossStart.position, Player);
    }

    // ---- Utilidades para el Diablo ----
    public Vector3 GroundAt(Vector3 point, Vector3 fallback)
    {
        Vector3 from = new Vector3(point.x, arenaCenter.position.y + 25f, point.z);
        var hits = Physics.RaycastAll(from, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 result = fallback;
        foreach (RaycastHit h in hits)
        {
            if (h.rigidbody != null)
                continue;   // demonios, Pogoman, el Diablo
            if (h.distance < best)
            {
                best = h.distance;
                result = h.point;
            }
        }
        // Solo cuenta el piso de la arena (no los muros ni la orilla de afuera).
        Vector3 flat = result - arenaCenter.position;
        flat.y = 0f;
        if (Mathf.Abs(result.y - arenaCenter.position.y) > 3f || flat.magnitude > floorRadius + 6f)
            return fallback;
        return result;
    }

    // Saca un demonio (o jefe de pilar) del suelo de la arena, lejos de Pogoman.
    public bool Summon(Enemy prefab, List<Enemy> list, bool boss)
    {
        EnemyDirector director = EnemyDirector.Instance;
        if (director == null || Player == null)
            return false;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(7f, floorRadius);
            Vector3 p = arenaCenter.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            Vector3 toPlayer = p - Player.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude < 7f)
                continue;
            Vector3 ground = GroundAt(p, Vector3.positiveInfinity);
            if (float.IsInfinity(ground.x))
                continue;

            Enemy e = director.SpawnEnemy(prefab != null ? prefab : director.RandomDemonPrefab(), ground);
            if (e == null)
                return false;
            list.Add(e);
            if (boss)
                ItemFx.Burst(ground + Vector3.up, 40, 1f, new Color(0.8f, 0.2f, 1f), fxMaterial, 8f, 0.5f, 0.8f);
            return true;
        }
        return false;
    }

    // ---- Avisos del Diablo ----
    // Sin indicaciones: solo la barra y cuántos quedan.
    public void OnWaveStarted(int wave, bool pillar) { }

    public void OnSlam(Vector3 at)
    {
        Play(slamClip, 1f, 1f);
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            ItemFx.Burst(at + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * 3f, 10, 0.4f, new Color(0.45f, 0.25f, 0.2f), fxMaterial, 8f, 0.7f, 0.7f);
        }
    }

    public void OnRise() => Play(laughClip, Random.Range(0.95f, 1.05f), 0.8f);
    public void OnDescend() { }

    public void OnVulnerable()
    {
        Play(vulnerableClip, 1f, 1f);
    }

    public void OnFireball(Vector3 hand)
    {
        Play(castClip, Random.Range(0.9f, 1.1f), 0.7f);
        ItemFx.Burst(hand, 18, 0.4f, new Color(1f, 0.5f, 0.1f), fxMaterial, 4f, 0.4f, 0.4f);
    }

    public void OnBossHit(float damage)
    {
        if (Time.time - lastHurt > 0.25f)
        {
            lastHurt = Time.time;
            Play(hurtClip, Random.Range(0.9f, 1.1f), 0.8f);
        }
    }

    public void OnBossDefeated(DevilBoss boss) => StartCoroutine(Victory(boss));

    private IEnumerator Victory(DevilBoss boss)
    {
        Play(deathClip, 1f, 1f);


        // Todos los demonios que quedan se mueren con él.
        foreach (EnemyTracker e in new List<EnemyTracker>(EnemyTracker.All))
            if (e != null && !e.IsDead && e.Enemy != boss)
                e.Hit(99999f, HitSource.Other);

        if (PlayerVitals.Instance != null)
            PlayerVitals.Instance.InvulnerableUntil = float.MaxValue;

        // Cámara lenta mientras explota.
        Time.timeScale = 0.35f;
        Transform t = boss.transform;
        Vector3 start = t.position;
        for (float k = 0f; k < 2.2f; k += Time.unscaledDeltaTime)
        {
            if (Random.value < 0.3f)
                ItemFx.Burst(start + Vector3.up * Random.Range(1f, 6f) + Random.insideUnitSphere * 2f, 30, 0.6f,
                    Random.value < 0.5f ? new Color(1f, 0.5f, 0.1f) : new Color(0.7f, 0.2f, 1f), fxMaterial, 8f, 0.6f, 0.6f);
            if (t != null)
            {
                t.position = start + Random.insideUnitSphere * 0.15f;
                t.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, Mathf.Pow(k / 2.2f, 2f));
            }
            yield return null;
        }
        if (t != null)
            Destroy(t.gameObject);
        Time.timeScale = 1f;

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.RunEnded = true;
        Play(victoryClip, 1f, 1f);
        yield return new WaitForSecondsRealtime(1.2f);
        VictoryScreen.Show(font);
    }

    // ---- HUD ----
    private void BuildHud()
    {
        Canvas canvas = UiKit.MakeCanvas("HUDArena", 6);
        canvas.transform.SetParent(transform, false);
        hud = canvas.gameObject.AddComponent<CanvasGroup>();
        hud.interactable = false;
        hud.blocksRaycasts = false;
        Transform root = canvas.transform;

        bossName = UiKit.Text(UiKit.Rect("Nombre", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -70), new Vector2(900, 44)), font, 38, new Color(1f, 0.75f, 0.55f), TextAnchor.MiddleCenter, "EL DIABLO TENDERO");
        RectTransform bar = UiKit.Rect("Barra", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -118), new Vector2(760, 22));
        UiKit.Image(bar, new Color(0.08f, 0.02f, 0.04f, 0.85f));
        bossFillLag = UiKit.Rect("Retraso", bar, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        UiKit.Image(bossFillLag, new Color(1f, 0.85f, 0.6f));
        bossFill = UiKit.Rect("Vida", bar, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        UiKit.Image(bossFill, new Color(0.75f, 0.08f, 0.15f));
        bossState = UiKit.Text(UiKit.Rect("Estado", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -146), new Vector2(1100, 36)), font, 28, Color.white, TextAnchor.MiddleCenter);

        banner = UiKit.Text(UiKit.Rect("Aviso", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 170), new Vector2(1600, 120)), font, 90, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        banner.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4, -4);
        bannerSub = UiKit.Text(UiKit.Rect("AvisoSub", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 95), new Vector2(1400, 50)), font, 36, new Color(1f, 0.8f, 0.7f), TextAnchor.MiddleCenter);
        bannerSub.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
    }

    private void ShowBanner(string title, string sub, float seconds)
    {
        if (banner == null)
            return;
        banner.text = title;
        bannerSub.text = sub;
        bannerTimer = seconds;
        bannerPop = 1f;
    }

    private void Update()
    {
        if (hud == null)
            return;

        float dt = Time.unscaledDeltaTime;
        bannerTimer -= dt;
        bannerPop = Mathf.MoveTowards(bannerPop, 0f, dt * 3f);
        float a = Mathf.Clamp01(bannerTimer / 0.4f);
        banner.color = new Color(banner.color.r, banner.color.g, banner.color.b, a);
        bannerSub.color = new Color(bannerSub.color.r, bannerSub.color.g, bannerSub.color.b, a);
        banner.rectTransform.localScale = Vector3.one * (1f + bannerPop * 0.4f);

        if (devil == null)
        {
            bossName.text = bossState.text = "";
            UiKit.SetFill(bossFill, 0f);
            UiKit.SetFill(bossFillLag, 0f);
            return;
        }

        float health = Mathf.Clamp01(devil.Health / devil.MaxHealth);
        lagHealth = Mathf.MoveTowards(lagHealth, health, dt * 0.25f);
        if (lagHealth < health) lagHealth = health;
        UiKit.SetFill(bossFill, health);
        UiKit.SetFill(bossFillLag, lagHealth);

        switch (devil.State)
        {
            case DevilBoss.Phase.Flying:
                bossState.text = "QUEDAN " + devil.WaveRemaining;
                bossState.color = new Color(0.85f, 0.7f, 1f);
                break;
            default:
                bossState.text = "";
                break;
        }
    }

    private void Play(AudioClip clip, float pitch, float volume)
    {
        if (clip == null)
            return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }
}
