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
    [Tooltip("Música de la segunda fase (FURIA).")]
    [SerializeField] private AudioClip furyMusic;
    [Tooltip("Música del modo infinito (el infierno quiere eliminarte).")]
    [SerializeField] private AudioClip endlessMusic;
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
        ShowBanner("EL DIABLO TENDERO", "DUEÑO DEL INFIERNO Y DE LA TIENDITA", 2.6f);
        Play(laughClip, 1f, 1f);
        devil.IntroTime = IntroBow + IntroLines.Length * LineTime + 0.6f;
        StartCoroutine(IntroCinematic());
    }

    // ---- Intro: hace su reverencia y luego te habla ----
    private const float IntroBow = 2.6f;
    private const float LineTime = 2.7f;
    private static readonly string[] IntroLines =
    {
        "Vaya, vaya... el cliente que vendió su alma por un pogo.",
        "¿Creíste que podías rebotar en MI tiendita sin pagar?",
        "Aquí no se fía, mijo. Se paga con el alma... ¡y la tuya ya es MÍA!",
    };
    private static readonly string[] FuryLines =
    {
        "¡JA! ¿Creíste que eso era todo?",
        "¡La tiendita todavía NO CIERRA!",
    };
    private RectTransform barTop, barBottom, speech;
    private Text speechText, speechName;
    private CanvasGroup speechGroup;
    private static AudioClip voiceBlip;

    private IEnumerator IntroCinematic()
    {
        yield return Letterbox(true);
        yield return new WaitForSeconds(IntroBow - 0.4f);
        // Termina la reverencia: desde aquí lo anima DevilRig (hablando).
        if (graph.IsValid()) graph.Destroy();
        if (devil != null && devil.Rig != null) devil.Rig.State = DevilRig.Pose.Talk;
        yield return Speak(IntroLines, LineTime);
        StartCoroutine(Letterbox(false));
    }

    private IEnumerator Speak(string[] lines, float perLine)
    {
        speechGroup.alpha = 1f;
        foreach (string line in lines)
        {
            float start = Time.time;
            int shown = 0;
            while (Time.time - start < perLine)
            {
                int n = Mathf.Min(line.Length, Mathf.FloorToInt((Time.time - start) * 38f));
                if (n > shown)
                {
                    // Un "bla" grave por cada par de letras (voz de diablo).
                    if (n / 2 != shown / 2 && line[n - 1] != ' ')
                        PlayVoice(0.55f + Random.value * 0.12f);
                    shown = n;
                    speechText.text = line.Substring(0, n);
                }
                devil?.Rig?.Talking(n < line.Length);
                speech.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.08f - (Time.time - start)) * 2f);
                yield return null;
            }
            devil?.Rig?.Talking(false);
        }
        for (float a = 1f; a > 0f; a -= Time.deltaTime * 4f) { speechGroup.alpha = a; yield return null; }
        speechGroup.alpha = 0f;
    }

    private IEnumerator Letterbox(bool show)
    {
        for (float k = 0f; k <= 1f; k += Time.unscaledDeltaTime * 3f)
        {
            float e = show ? k : 1f - k;
            e = e * e * (3f - 2f * e);
            barTop.sizeDelta = new Vector2(0f, 130f * e);
            barBottom.sizeDelta = new Vector2(0f, 130f * e);
            yield return null;
        }
        barTop.sizeDelta = barBottom.sizeDelta = new Vector2(0f, show ? 130f : 0f);
    }

    private void PlayVoice(float pitch)
    {
        if (voiceBlip == null)
        {
            const int rate = 44100;
            int n = rate / 14;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Sin(Mathf.PI * i / n);
                // Vocal "a" gruesa: fundamental + formantes aproximados.
                data[i] = env * (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 760f * t) * 0.25f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                    + Mathf.Sin(2f * Mathf.PI * 1150f * t) * 0.12f) * 0.8f;
            }
            voiceBlip = AudioClip.Create("VozDiablo", n, 1, rate, false);
            voiceBlip.SetData(data, 0);
        }
        Play(voiceBlip, pitch * 2f, 0.55f);
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
            e.rateOverTime = vulnerable ? 15f : devil.Fury ? 70f : 30f;
            mm.startColor = vulnerable ? new Color(1f, 0.9f, 0.3f, 0.8f) : devil.Fury ? new Color(1f, 0.2f, 0.05f, 0.85f) : new Color(0.55f, 0.15f, 0.9f, 0.7f);
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

    // ---- Ataques especiales ----
    public void OnSpecial(int kind)
    {
        Play(kind == 2 ? laughClip : castClip, kind == 2 ? 0.8f : 0.7f, 0.9f);
    }

    public void OnPillarErupt(Vector3 at)
    {
        Play(explodeClip, Random.Range(0.8f, 0.95f), 0.6f);
        for (int i = 0; i < 6; i++)
            ItemFx.Burst(at + Vector3.up * (i * 0.8f), 18, 0.6f, i % 2 == 0 ? new Color(1f, 0.45f, 0.05f) : new Color(1f, 0.85f, 0.3f), fxMaterial, 4f + i, 0.5f, 0.6f);
    }

    private float ringFx;
    public void OnRingTick(Vector3 c, float r)
    {
        ringFx -= Time.deltaTime;
        if (ringFx > 0f) return;
        ringFx = 0.05f;
        for (int i = 0; i < 3; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            ItemFx.Burst(c + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * r, 6, 0.3f, new Color(1f, 0.4f, 0.05f), fxMaterial, 3f, 0.35f, 0.35f);
        }
    }

    // Saca un ojo alado en la arena (furia).
    public bool SummonFlyer(List<Enemy> list)
    {
        EnemyDirector director = EnemyDirector.Instance;
        return director != null && director.FlyingPrefab != null && Summon(director.FlyingPrefab, list, false);
    }

    // ---- Segunda fase ----
    private Renderer[] devilRenderers;
    private Material[] devilMaterials;
    private Color[] devilBaseColors;
    private ParticleSystem devilAura;

    public void OnEnrage(DevilBoss boss)
    {
        StartCoroutine(EnrageRoutine(boss));
    }

    private IEnumerator EnrageRoutine(DevilBoss boss)
    {
        Play(vulnerableClip, 0.6f, 1f);
        Play(laughClip, 0.75f, 1f);
        ShowBanner("¡FURIA!", "SEGUNDA FASE", 3f);
        if (furyMusic != null) FindAnyObjectByType<GameMusic>()?.SwitchTo(furyMusic, 0.4f);
        if (PlayerVitals.Instance != null)
            PlayerVitals.Instance.InvulnerableUntil = Mathf.Max(PlayerVitals.Instance.InvulnerableUntil, Time.time + 3.5f);
        ItemFx.Burst(boss.transform.position + Vector3.up * 3f, 120, 2.5f, new Color(1f, 0.15f, 0.05f), fxMaterial, 12f, 0.6f, 1f);
        StartCoroutine(Letterbox(true));
        yield return Speak(FuryLines, 1.5f);
        StartCoroutine(Letterbox(false));
    }

    // Se va poniendo rojo brillante mientras recupera su vida.
    public void OnEnrageTick(float k)
    {
        if (devil == null) return;
        if (devilMaterials == null)
        {
            devilRenderers = devil.GetComponentsInChildren<Renderer>();
            var mats = new List<Material>();
            var cols = new List<Color>();
            foreach (Renderer r in devilRenderers)
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (Material m in r.materials)
                {
                    mats.Add(m);
                    cols.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
                    m.EnableKeyword("_EMISSION");
                }
            }
            devilMaterials = mats.ToArray();
            devilBaseColors = cols.ToArray();
            devilAura = devil.GetComponentInChildren<ParticleSystem>();
        }
        for (int i = 0; i < devilMaterials.Length; i++)
        {
            Material m = devilMaterials[i];
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(devilBaseColors[i], new Color(1f, 0.2f, 0.12f), k * 0.75f));
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.02f) * (k * 2.2f));
        }
        if (devilAura != null)
        {
            var main = devilAura.main;
            main.startColor = new Color(1f, 0.2f, 0.05f, 0.85f);
            var em = devilAura.emission;
            em.rateOverTime = 30f + 40f * k;
        }
        if (Random.value < 0.25f)
            ItemFx.Burst(devil.transform.position + Vector3.up * Random.Range(1f, 6f) + Random.insideUnitSphere, 12, 0.5f, new Color(1f, 0.25f, 0.05f), fxMaterial, 5f, 0.4f, 0.5f);
    }

    public void OnSurviveStarted()
    {
        ShowBanner("¡SOBREVIVE!", "", 1.6f);
    }
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

    public void OnBossDefeated(DevilBoss boss) => StartCoroutine(Defeated(boss));

    // Vencido, cae de rodillas y ofrece un trato.
    private IEnumerator Defeated(DevilBoss boss)
    {
        foreach (EnemyTracker e in new List<EnemyTracker>(EnemyTracker.All))
            if (e != null && !e.IsDead && e.Enemy != boss)
                e.Hit(99999f, HitSource.Other);
        if (PlayerVitals.Instance != null)
            PlayerVitals.Instance.InvulnerableUntil = float.MaxValue;
        if (boss.Rig != null) boss.Rig.State = DevilRig.Pose.Stunned;
        Play(vulnerableClip, 0.8f, 1f);
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(1.4f);
        Time.timeScale = 0f;
        DevilOffer.Show(font, null,
            () => { Time.timeScale = 1f; StartCoroutine(Victory(boss)); },
            () => { Time.timeScale = 1f; StartCoroutine(Deal(boss)); });
    }

    // Aceptaste el trato: legendario al azar y de vuelta al infierno (modo infinito).
    private IEnumerator Deal(DevilBoss boss)
    {
        ChestUI chestUi = FindAnyObjectByType<ChestUI>();
        ItemDatabase db = chestUi != null ? PogoBridge.Get<ItemDatabase>(chestUi, "database") : null;
        ItemDefinition gift = null;
        if (db != null)
        {
            var pool = db.items.FindAll(i => i != null && i.rarity == ItemRarity.Legendaria && ItemEffects.CanOffer(i));
            if (pool.Count == 0) pool = db.items.FindAll(i => i != null && i.rarity == ItemRarity.Legendaria);
            if (pool.Count > 0) gift = pool[Random.Range(0, pool.Count)];
        }
        Play(laughClip, 0.9f, 1f);
        if (gift != null && chestUi != null)
        {
            bool done = false;
            chestUi.ShowGift(gift, () => done = true);
            while (!done) yield return null;
        }

        // El Diablo se ríe y se desvanece.
        if (boss != null)
        {
            ItemFx.Burst(boss.transform.position + Vector3.up * 4f, 80, 2f, new Color(0.6f, 0.15f, 1f), fxMaterial, 8f, 0.6f, 1f);
            Destroy(boss.gameObject);
        }
        yield return new WaitForSeconds(0.8f);

        // De vuelta al mapa, frente a la puerta (que ya no se abre).
        ArenaGate gate = FindAnyObjectByType<ArenaGate>();
        Vector3 back = gate != null ? gate.ReturnPoint : Vector3.zero;
        var rb = Player.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.position = back;
        Player.transform.position = back;
        PogoCamera cam = FindAnyObjectByType<PogoCamera>();
        if (cam != null) PogoBridge.Set(cam, "currentPosition", back - Player.transform.forward * 6f + Vector3.up * 3f);
        InArena = false;
        if (hud != null) hud.alpha = 0f;
        if (arenaRoot != null) arenaRoot.SetActive(false);
        if (EnemyDirector.Instance != null) EnemyDirector.Instance.ArenaMode = false;
        if (gate != null)
        {
            if (gate.MapMusic != null) FindAnyObjectByType<GameMusic>()?.SwitchTo(gate.MapMusic, 1f);
            gate.Close();
        }
        if (PlayerVitals.Instance != null) PlayerVitals.Instance.InvulnerableUntil = Time.time + 3f;
        SceneNav.SetGameplayCursor(true);
        EndlessMode.Begin(font, endlessMusic);
    }

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

        // Barras de cine y globo de diálogo (intro y furia).
        barTop = UiKit.Rect("CineArriba", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
        UiKit.Image(barTop, Color.black);
        barBottom = UiKit.Rect("CineAbajo", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 0f));
        UiKit.Image(barBottom, Color.black);
        speech = UiKit.Rect("Dialogo", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1300f, 150f));
        UiKit.Image(speech, new Color(0.12f, 0.02f, 0.03f, 0.92f));
        speech.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.35f, 0.15f, 0.9f);
        speechGroup = speech.gameObject.AddComponent<CanvasGroup>();
        speechGroup.alpha = 0f;
        speechName = UiKit.Text(UiKit.Rect("Quien", speech, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(24f, -6f), new Vector2(600f, 40f)),
            font, 30, new Color(1f, 0.55f, 0.25f), TextAnchor.LowerLeft, "EL DIABLO TENDERO");
        speechText = UiKit.Text(UiKit.Stretch("Texto", speech, 24f), font, 38, new Color(1f, 0.93f, 0.85f), TextAnchor.MiddleLeft);
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
            case DevilBoss.Phase.Flying when devil.Fury:
                bossState.text = "¡SOBREVIVE!  " + Mathf.CeilToInt(devil.SurviveLeft);
                bossState.color = new Color(1f, 0.45f, 0.3f);
                break;
            case DevilBoss.Phase.Flying:
                bossState.text = "QUEDAN " + devil.WaveRemaining;
                bossState.color = new Color(0.85f, 0.7f, 1f);
                break;
            case DevilBoss.Phase.Vulnerable:
                bossState.text = "¡PÉGALE CON EL POGO!";
                bossState.color = new Color(1f, 0.9f, 0.4f);
                break;
            default:
                bossState.text = "";
                break;
        }
        if (devil.Fury)
        {
            // Barra en rojo brillante que late.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            bossFill.GetComponent<Image>().color = Color.Lerp(new Color(1f, 0.1f, 0.05f), new Color(1f, 0.5f, 0.2f), pulse * 0.5f);
            bossName.color = Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(1f, 0.85f, 0.4f), pulse * 0.4f);
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
