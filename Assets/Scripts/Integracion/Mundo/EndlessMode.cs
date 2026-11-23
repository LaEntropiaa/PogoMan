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
    // Escalones de dificultad (vida y daño):
    //   1:00 ×2 · 2:00 ×5 · 2:30 ×6 · 3:00 ×10 (riff y anuncio diabólico) · 4:00 ×15 · 5:00 ×20
    //   6:00 ×30 y desde ahí +10 cada minuto.
    private static readonly float[] StepTimes = { 60f, 120f, 150f, 180f, 240f, 300f, 360f };
    private static readonly float[] StepScales = { 2f, 5f, 6f, 10f, 15f, 20f, 30f };
    public const float DiabolicTime = 180f;

    public static int StepAt(float t)
    {
        int n = 0;
        foreach (float s in StepTimes) if (t >= s) n++;
        if (t >= 420f) n += Mathf.FloorToInt((t - 360f) / 60f);
        return n;
    }

    public static float ScaleAt(float t)
    {
        if (t < StepTimes[0]) return 1f;
        if (t >= 420f) return StepScales[StepScales.Length - 1] + 10f * Mathf.FloorToInt((t - 360f) / 60f);
        float v = 1f;
        for (int i = 0; i < StepTimes.Length; i++) if (t >= StepTimes[i]) v = StepScales[i];
        return v;
    }

    public static float Scale => Active && instance != null ? ScaleAt(instance.elapsed) : 1f;
    // Los demonios se vuelven rapidísimos (hasta ×5).
    public static float SpeedScale => Active ? Mathf.Min(5f, 1f + 0.2f * (Scale - 1f)) : 1f;
    public const float EliteChestChance = 0.002f;   // 0.2 %

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
        label = UiKit.Text(UiKit.Rect("Infinito", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -205f), new Vector2(760f, 36f)),
            font, 24, new Color(1f, 0.4f, 0.3f), TextAnchor.MiddleRight);
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
        int lvl = StepAt(elapsed);
        if (lvl != Level)
        {
            Level = lvl;
            pop = 1f;
            EnemyTracker.CoinChanceBonus = Mathf.Min(0.5f, 0.05f * Level);   // más monedas
            bool diabolic = lvl == 4;   // el escalón de las 3:00 (×10)
            StartCoroutine(Wrath(false, diabolic));
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
    private static readonly string[] DiabolicThreats =
    {
        "SATANÁS EN PERSONA QUIERE TU ALMA",
        "EL INFIERNO DESPIERTA DE VERDAD",
    };

    private IEnumerator Wrath(bool first, bool diabolic = false)
    {
        PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
        if (p == null) yield break;

        if (bag.Count == 0) bag.AddRange(Threats);
        string text = first ? Threats[0] : diabolic ? DiabolicThreats[Random.Range(0, DiabolicThreats.Length)] : bag[Random.Range(0, bag.Count)];
        bag.Remove(text);
        if (diabolic) StartCoroutine(Diabolic(p.transform));

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

    // ---- Minuto 3: anuncio diabólico (riff de guitarra, flamas por toda la pantalla, columnas de fuego) ----
    private static AudioClip riff;

    private IEnumerator Diabolic(Transform player)
    {
        if (riff == null) riff = MakeRiff();
        audioSource.PlayOneShot(riff, 1.1f * GameSettings.SfxVolume);
        threat.fontSize = 92;
        CameraShake.Add(3.5f, 1.8f);

        // Fuego de pixel animado (estilo DOOM) que sube desde abajo.
        Transform root = flash.transform.parent;
        DoomFire fire = DoomFire.Create(root, Vector2.zero, new Vector2(1f, 0.5f));
        threat.transform.SetAsLastSibling();
        // Columnas de fuego alrededor de Pogoman.
        Material fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            Vector3 at = player.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 9f;
            for (int k = 0; k < 5; k++)
                ItemFx.Burst(at + Vector3.up * k * 1.4f, 20, 0.8f, k % 2 == 0 ? new Color(1f, 0.35f, 0.05f) : new Color(1f, 0.8f, 0.2f), fx, 6f, 0.7f, 0.9f);
        }

        for (float t = 0f; t < 6.5f; t += Time.unscaledDeltaTime)
        {
            fire.Intensity = t < 4.5f ? Mathf.Clamp01(t / 0.3f) : 0f;   // arde 4.5 s y se apaga sola
            threat.color = Color.Lerp(new Color(1f, 0.15f, 0.05f), new Color(1f, 0.8f, 0.2f), 0.5f + 0.5f * Mathf.Sin(t * 12f));
            yield return null;
        }
        Destroy(fire.gameObject);
        threat.fontSize = 72;
        threat.color = new Color(1f, 0.25f, 0.15f);
    }

    // Riff de metal: chugs en Mi con palm mute, acentos y un acorde final que se queda sonando.
    private static AudioClip MakeRiff()
    {
        const int rate = 44100;
        float bpm = 190f, s16 = 60f / bpm / 4f;
        // (inicio en semicorcheas, duración, nota MIDI raíz, palm mute)
        var notes = new List<(float at, float dur, int root, bool mute)>();
        int[] chug = { 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14 };
        foreach (int k in chug) notes.Add((k, 0.9f, 40, true));
        notes.Add((3, 1f, 43, false)); notes.Add((7, 1f, 45, false)); notes.Add((11, 1f, 46, false)); notes.Add((15, 1f, 45, false));
        foreach (int k in chug) notes.Add((16 + k, 0.9f, 40, true));
        notes.Add((19, 1f, 48, false)); notes.Add((23, 1f, 47, false)); notes.Add((27, 2f, 46, false));
        notes.Add((32, 18f, 40, false));   // acorde final
        int n = (int)(rate * (s16 * 50f));
        var d = new float[n];
        foreach (var nt in notes)
        {
            int start = (int)(nt.at * s16 * rate);
            float dur = nt.dur * s16;
            int len = Mathf.Min(n - start, (int)(rate * (dur + (nt.mute ? 0.02f : 0.6f))));
            float[] fs = { Mtof(nt.root), Mtof(nt.root + 7), Mtof(nt.root + 12) };
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)rate;
                float env = nt.mute ? Mathf.Exp(-t * 26f) : Mathf.Exp(-t * (nt.dur > 10f ? 0.9f : 2.5f));
                float x = 0f;
                for (int j = 0; j < 3; j++)
                {
                    float f = fs[j] * (j == 1 ? 1.003f : 1f);
                    x += (2f * Mathf.Repeat(f * t, 1f) - 1f) * (j == 2 ? 0.6f : 1f);
                    x += (2f * Mathf.Repeat(f * 1.004f * t, 1f) - 1f) * 0.7f;   // segunda toma
                }
                d[start + i] += x * env * 0.25f;
            }
            if (!nt.mute || ((int)nt.at) % 4 == 0)
                for (int i = 0; i < Mathf.Min(len, rate / 4); i++)   // bombo
                {
                    float t = i / (float)rate;
                    d[start + i] += Mathf.Sin(2f * Mathf.PI * (50f + 120f * Mathf.Exp(-t * 30f)) * t) * Mathf.Exp(-t * 9f) * 0.6f;
                }
        }
        // Distorsión + "gabinete" (pasabajos y pasaaltos de un polo).
        float lp = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float y = (float)System.Math.Tanh(d[i] * 7f);
            lp += (y - lp) * 0.32f;
            hp = 0.995f * (hp + lp - prev);
            prev = lp;
            d[i] = Mathf.Clamp(hp * 0.85f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("RiffInfernal", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    private static float Mtof(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);

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
