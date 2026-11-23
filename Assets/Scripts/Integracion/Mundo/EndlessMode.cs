using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Modo infinito: aceptaste la oferta del Diablo y regresaste al infierno... que ahora quiere eliminarte
// por haber vencido a su jefe. Cada minuto:
//  - La dificultad sube: ×3, ×6, ×9... (vida y daño de los demonios, guardianes y élites).
//  - El averno te lo recuerda: tiembla la pantalla, caen rayos y aparece un mensaje.
//  - Sale un guardián de los pilares a cazarte (hasta 3 a la vez).
//  - Salen más élites (sus cofres son un poco menos probables).
public class EndlessMode : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static int Level { get; private set; }
    public static float Minutes => instance != null ? instance.elapsed / 60f : 0f;
    // Multiplicador de vida y daño: el primer minuto ×1, luego ×3, ×6, ×9, ×12... (sube 3 cada minuto).
    public static float Scale => Active ? (Level == 0 ? 1f : 3f * Level) : 1f;
    public const float EliteChestChance = 0.25f;   // 30 % normal − 5 %

    private static readonly string[] Threats =
    {
        "EL AVERNO QUIERE ELIMINARTE",
        "MATASTE A SU JEFE. AHORA TODO EL INFIERNO TE BUSCA",
        "EL INFIERNO TE COBRA LA CUENTA",
        "NO HAY SALIDA. SOLO HAY MÁS DEMONIOS",
        "LAS ÁNIMAS PIDEN TU CABEZA",
        "EL DIABLO TENÍA AMIGOS",
        "AQUÍ NADIE SALE VIVO",
        "EL FUEGO SE ALIMENTA DE TI",
        "TU ALMA YA TIENE PRECIO",
        "HASTA EL SUELO TE ODIA",
        "CADA MINUTO ES PEOR QUE EL ANTERIOR",
        "EL ABISMO ABRE LA BOCA",
        "LOS GUARDIANES DESPIERTAN POR TI",
        "NI MUERTO TE VAS A LIBRAR",
        "EL INFIERNO NO PERDONA",
    };

    private static EndlessMode instance;
    private float elapsed;
    private float chestTimer = 20f;
    private Text label, threat;
    private CanvasGroup threatGroup;
    private Image flash;
    private float pop;
    private readonly List<string> bag = new List<string>();
    private AudioSource audioSource;
    private static AudioClip thunder;

    public static void Begin(Font font, AudioClip music = null)
    {
        if (instance != null) return;
        Active = true;
        Level = 0;
        instance = new GameObject("ModoInfinito").AddComponent<EndlessMode>();
        instance.BuildHud(font);
        GuardianKit.KeepMusic = true;   // los guardianes ya no cambian la música
        if (music != null) FindAnyObjectByType<GameMusic>()?.SwitchTo(music, 0.5f);
        instance.StartCoroutine(instance.Wrath(true));
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null; Active = false; Level = 0; EnemyTracker.CoinChanceBonus = 0f;
            GuardianKit.KeepMusic = false;
        }
    }

    private void BuildHud(Font font)
    {
        audioSource = UiKit.UiAudio(gameObject);
        Canvas canvas = UiKit.MakeCanvas("HUDInfinito", 30);
        canvas.transform.SetParent(transform, false);
        label = UiKit.Text(UiKit.Rect("Infinito", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -150f), new Vector2(760f, 40f)),
            font, 28, new Color(1f, 0.4f, 0.3f), TextAnchor.MiddleRight);
        label.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);

        flash = UiKit.Image(UiKit.Stretch("Relampago", canvas.transform), new Color(1f, 0.9f, 1f, 0f));
        flash.raycastTarget = false;

        threat = UiKit.Text(UiKit.Rect("Amenaza", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 230f), new Vector2(1800f, 160f)), font, 72, new Color(1f, 0.25f, 0.15f), TextAnchor.MiddleCenter);
        threat.horizontalOverflow = HorizontalWrapMode.Wrap;
        var fx = threat.gameObject.AddComponent<PixelTextFx>();
        fx.top = new Color(1f, 0.85f, 0.6f);
        fx.bottom = new Color(1f, 0.2f, 0.1f);
        fx.depth = 4;
        fx.step = 3f;
        threatGroup = threat.gameObject.AddComponent<CanvasGroup>();
        threatGroup.alpha = 0f;
    }

    private void Update()
    {
        if (TimeStop.Active || (PlayerStats.Instance != null && PlayerStats.Instance.RunEnded)) return;
        elapsed += Time.deltaTime;
        int lvl = Mathf.FloorToInt(elapsed / 60f);
        if (lvl != Level)
        {
            Level = lvl;
            pop = 1f;
            EnemyTracker.CoinChanceBonus = Mathf.Min(0.5f, 0.05f * Level);   // más monedas
            StartCoroutine(Wrath(false));
        }

        // Cofres nuevos cada vez más seguido (de 45 s hasta 20 s).
        chestTimer -= Time.deltaTime;
        if (chestTimer <= 0f)
        {
            chestTimer = Mathf.Max(20f, 45f - Level * 3f);
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p != null) ChestSpawner.Instance?.SpawnNear(p.transform.position);
        }

        pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime * 1.5f);
        int t = Mathf.FloorToInt(elapsed);
        if (label != null)
        {
            label.text = "INFIERNO INFINITO  ·  NIVEL " + (Level + 1) + "  ·  ×" + FormatScale(Scale) + "  ·  " + (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
            label.rectTransform.localScale = Vector3.one * (1f + pop * 0.3f);
        }
    }

    private static string FormatScale(float s) => s >= 1e6f ? (s / 1e6f).ToString("0.#") + "M" : s >= 1e3f ? (s / 1e3f).ToString("0.#") + "K" : s.ToString("0");

    // ---- La ira del averno (cada minuto) ----
    private IEnumerator Wrath(bool first)
    {
        PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
        if (p == null) yield break;

        if (bag.Count == 0) bag.AddRange(Threats);
        string text = first ? Threats[0] : bag[Random.Range(0, bag.Count)];
        bag.Remove(text);

        CameraShake.Add(2.2f, 1.4f);
        if (thunder == null) thunder = MakeThunder();
        if (boom == null) boom = MakeBoom();
        audioSource.PlayOneShot(thunder, GameSettings.SfxVolume);
        audioSource.PlayOneShot(boom, GameSettings.SfxVolume);
        StartCoroutine(HitStop());
        StartCoroutine(RedPulse());
        StartCoroutine(Lightning(p.transform));
        if (!first) SpawnGuardian(p.transform.position);

        // Mensaje: cae ENORME de golpe sobre la pantalla (con el nivel nuevo) y tiembla.
        threat.text = text + (first ? "" : "\n<size=46>DIFICULTAD ×" + FormatScale(Scale) + "</size>");
        threatGroup.alpha = 1f;
        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.22f;
            threat.rectTransform.localScale = Vector3.one * Mathf.Lerp(3.2f, 1f, k * k);
            threatGroup.alpha = k;
            yield return null;
        }
        threat.rectTransform.localScale = Vector3.one;
        CameraShake.Add(0.6f, 2f);   // el golpe del texto
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            threat.rectTransform.anchoredPosition = new Vector2(Random.Range(-3f, 3f), 230f + Random.Range(-2f, 2f));
            threat.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(t * 8f) * 0.02f);
            yield return null;
        }
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { threatGroup.alpha = 1f - t / 0.6f; yield return null; }
        threatGroup.alpha = 0f;
    }

    // Rayos alrededor de Pogoman (con destello de pantalla). Matan demonios cercanos al caer.
    private IEnumerator Lightning(Transform player)
    {
        int strikes = 4 + Mathf.Min(Level, 6);
        for (int i = 0; i < strikes; i++)
        {
            Vector2 c = Random.insideUnitCircle * 14f;
            Vector3 target = player.position + new Vector3(c.x, 0f, c.y);
            if (Physics.Raycast(target + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                target = hit.point;
            Bolt(target);
            StartCoroutine(Flash(i == 0 ? 0.55f : 0.3f));
            if (i > 0 && thunder != null) audioSource.PlayOneShot(thunder, 0.35f * GameSettings.SfxVolume);
            yield return new WaitForSeconds(Random.Range(0.08f, 0.35f));
        }
    }

    private IEnumerator Flash(float strength)
    {
        for (int k = 0; k < 2; k++)
        {
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(1f, 0.85f, 0.95f, strength * (1f - t / 0.12f));
                yield return null;
            }
        }
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    private static void Bolt(Vector3 ground)
    {
        var go = new GameObject("Rayo");
        var lr = go.AddComponent<LineRenderer>();
        Vector3 top = ground + new Vector3(Random.Range(-6f, 6f), 45f, Random.Range(-6f, 6f));
        int n = 14;
        lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float k = i / (n - 1f);
            Vector3 p = Vector3.Lerp(top, ground, k);
            if (i > 0 && i < n - 1) p += new Vector3(Random.Range(-1.6f, 1.6f), 0f, Random.Range(-1.6f, 1.6f));
            lr.SetPosition(i, p);
        }
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(1f, 0.35f));
        lr.startColor = lr.endColor = new Color(0.9f, 0.75f, 1f);
        lr.numCornerVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        if (fx != null) lr.sharedMaterial = fx;
        var light = go.AddComponent<Light>();
        go.transform.position = ground + Vector3.up * 4f;
        light.type = LightType.Point;
        light.color = new Color(0.8f, 0.7f, 1f);
        light.range = 40f;
        light.intensity = 14f;
        ItemFx.Burst(ground + Vector3.up * 0.3f, 40, 0.6f, new Color(0.85f, 0.7f, 1f), fx, 9f, 0.35f, 0.5f);
        ItemFx.Burst(ground + Vector3.up * 0.3f, 30, 1f, new Color(0.3f, 0.25f, 0.3f, 0.8f), fx, 3f, 0.8f, 1f);
        EnemyTracker.HitArea(ground, 3f, 999999f, HitSource.Other, true, 4f, null, true);
        go.AddComponent<BoltFade>();
    }

    private void SpawnGuardian(Vector3 near)
    {
        GuardianKit kit = GuardianKit.Instance;
        if (kit == null || kit.GuardianPrefabs == null || kit.GuardianPrefabs.Length == 0) return;
        int alive = 0;
        foreach (Guardian g in Guardian.Alive) if (g != null && !g.IsDead) alive++;
        if (alive >= 3) return;
        Enemy prefab = kit.GuardianPrefabs[Random.Range(0, kit.GuardianPrefabs.Length)];
        if (prefab == null) return;
        Vector2 c = Random.insideUnitCircle.normalized * Random.Range(10f, 16f);
        Vector3 at = near + new Vector3(c.x, 0f, c.y);
        if (Physics.Raycast(at + Vector3.up * 25f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
        Enemy e = Instantiate(prefab, at, Quaternion.identity);
        if (e is Guardian guardian) guardian.Empower(Scale);
        e.SetTarget(FindAnyObjectByType<PogoPlayer>()?.transform);
    }

    private static AudioClip boom;
    private Image redPulse;

    // Un instante de cámara lenta: se siente el golpe.
    private IEnumerator HitStop()
    {
        if (Time.timeScale <= 0f) yield break;
        Time.timeScale = 0.15f;
        yield return new WaitForSecondsRealtime(0.18f);
        if (Time.timeScale > 0f) Time.timeScale = 1f;
    }

    // La pantalla late en rojo sangre tres veces.
    private IEnumerator RedPulse()
    {
        if (redPulse == null)
        {
            redPulse = UiKit.Image(UiKit.Stretch("PulsoRojo", flash.transform.parent), new Color(0.6f, 0f, 0f, 0f));
            redPulse.raycastTarget = false;
            redPulse.transform.SetSiblingIndex(flash.transform.GetSiblingIndex());
        }
        for (int i = 0; i < 3; i++)
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                redPulse.color = new Color(0.6f, 0f, 0f, 0.45f * Mathf.Sin(t / 0.4f * Mathf.PI) * (1f - i * 0.25f));
                yield return null;
            }
        redPulse.color = new Color(0.6f, 0f, 0f, 0f);
    }

    // Golpe grave tipo trailer: bombo enorme + ruido sucio.
    private static AudioClip MakeBoom()
    {
        const int rate = 22050;
        int n = (int)(rate * 2.2f);
        var d = new float[n];
        var rng = new System.Random(21);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float f = Mathf.Lerp(70f, 28f, Mathf.Clamp01(t / 0.8f));
            float sub = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 1.6f);
            float w = (float)rng.NextDouble() * 2f - 1f;
            lp += (w - lp) * 0.08f;
            float dirt = lp * 4f * Mathf.Exp(-t * 3f);
            d[i] = Mathf.Clamp((float)System.Math.Tanh((sub * 1.4f + dirt) * 2.2f) * 0.9f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("GolpeAverno", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    private static AudioClip MakeThunder()
    {
        const int rate = 22050;
        int n = (int)(rate * 2.5f);
        var d = new float[n];
        var rng = new System.Random(3);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float crack = t < 0.1f ? 1f : 0f;
            float env = Mathf.Exp(-t * 1.6f) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 9f, 0f)) + crack * 0.7f;
            float w = (float)rng.NextDouble() * 2f - 1f;
            lp += (w - lp) * 0.06f;
            lp2 += (lp - lp2) * 0.1f;
            d[i] = Mathf.Clamp((lp2 * 7f + w * crack * 0.35f) * env, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("TruenoAverno", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}

// Rayo: parpadea y se apaga rápido.
public class BoltFade : MonoBehaviour
{
    private float age;
    private LineRenderer lr;
    private Light lightComp;
    private void Awake() { lr = GetComponent<LineRenderer>(); lightComp = GetComponent<Light>(); }
    private void Update()
    {
        age += Time.deltaTime;
        float k = age / 0.35f;
        bool on = k < 0.25f || (k > 0.45f && k < 0.6f);
        if (lr != null) lr.enabled = on;
        if (lightComp != null) lightComp.intensity = on ? 14f * (1f - k) : 0f;
        if (k >= 1f) Destroy(gameObject);
    }
}

// Temblor de la cámara (encima de lo que haga PogoCamera ese frame).
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;
    private float time, strength;
    private Vector3 applied;

    public static void Add(float seconds, float amount)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (instance == null) instance = cam.gameObject.AddComponent<CameraShake>();
        instance.time = Mathf.Max(instance.time, seconds);
        instance.strength = Mathf.Max(instance.strength, amount);
    }

    // Antes de que PogoCamera calcule (Update va antes que cualquier LateUpdate) se quita el temblor
    // del frame anterior: así la cámara nunca "hereda" el desplazamiento y no se va acumulando.
    private void Update()
    {
        transform.position -= applied;
        applied = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (time <= 0f) return;
        time -= Time.deltaTime;
        float k = Mathf.Clamp01(time / 0.6f) * strength;
        applied = new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f, 0f) * 0.9f * k;
        applied = transform.rotation * applied;
        transform.position += applied;
        if (time <= 0f) strength = 0f;
    }
}
