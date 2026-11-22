using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Muerte de Pogoman (versión gore caricaturesca):
//  1. Cámara lenta, chorro de sangre y charco.
//  2. Arde en llamas: se va carbonizando, humo negro.
//  3. Revienta: pedazos chamuscados, sangre, el resorte del pogo sale volando, nube de ceniza.
//  4. Mientras tanto, y en la pantalla de fin, comentarios burlones para el jugador.
// GameHUD espera ScreenDelay segundos antes de mostrar la pantalla de fin.
public class PogoDeathFx : MonoBehaviour
{
    public const float ScreenDelay = 3.6f;

    private static readonly string[] Roasts =
    {
        "¿Ese era tu mejor salto? Mi abuelita rebota más.",
        "El Diablo ni sudó. Literal, ni te volteó a ver.",
        "Vendiste tu alma por un pogo... y ni lo sabes usar.",
        "Pogoman a la parrilla, término bien cocido.",
        "Los demonios te van a poner en su historia de Instagram.",
        "Tranquilo, el infierno tiene tutorial. Ah, no, no tiene.",
        "Hasta el hongo te dio lástima.",
        "Rebotaste directo a la tumba. Felicidades.",
        "Saltar y escupir. Dos cosas. Fallaste en las dos.",
        "Te comieron más rápido que unos tacos al pastor.",
        "Skill issue de nivel infernal.",
        "¿Y si mejor juegas con los ojos abiertos?",
        "Ni el Contrato a Mano Pelona te iba a salvar.",
        "Tus restos ya los están vendiendo en la tiendita del Diablo.",
        "Ese pogo merecía un dueño mejor.",
        "Patrichocle lo hizo mejor. Y eso ya es decir.",
        "Gedgar se cayó al limbo y aun así duró más que tú.",
        "Los demonios te mandan saludos... y tus pedazos.",
        "Bien asadito. Le faltó limón nomás.",
        "La próxima, intenta NO pararte enfrente de todos.",
        "Ni modo, el infierno no acepta devoluciones.",
        "Calificación final: 2/10, rebotó bonito al menos.",
        "¿Otra? Seguro esta vez sí... (no).",
        "Hasta las ánimas del río se están riendo.",
    };

    private PogoPlayer player;
    private bool started;
    private Material fx;
    private Text roast;
    private CanvasGroup roastGroup;
    private readonly List<string> bag = new List<string>();
    private static AudioClip sizzle, splat;
    private AudioSource audioSource;

    private void Awake()
    {
        player = GetComponent<PogoPlayer>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (started || player == null || !PogoBridge.IsDead(player))
            return;
        started = true;
        fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        StartCoroutine(Die());
        StartCoroutine(RoastLoop());
    }

    // Material transparente con mezcla normal (el de las señales de los guardianes).
    private static Material AlphaMat => Telegraph.SharedMaterial;

    private Vector3 Body => transform.position + Vector3.up * 0.2f;

    private IEnumerator Die()
    {
        if (sizzle == null) MakeSounds();

        // ---- 1. Cámara lenta y sangre ----
        Time.timeScale = 0.35f;
        Blood(Body, 70, 6f);
        BloodPool();
        audioSource.PlayOneShot(splat, 0.9f * GameSettings.SfxVolume);
        yield return new WaitForSecondsRealtime(0.5f);
        if (Time.timeScale > 0f) Time.timeScale = 1f;

        // ---- 2. Arde ----
        ParticleSystem fire = Flames();
        ParticleSystem smoke = Smoke();
        audioSource.PlayOneShot(sizzle, 0.8f * GameSettings.SfxVolume);
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        var bases = new List<Color[]>();
        foreach (Renderer r in rends)
        {
            var mats = r.sharedMaterials;
            var cols = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                cols[i] = mats[i] != null && mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : Color.white;
            bases.Add(cols);
        }
        var block = new MaterialPropertyBlock();
        Color charred = new Color(0.07f, 0.045f, 0.04f);
        for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / 1.3f);
            float flick = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 31f);
            for (int r = 0; r < rends.Length; r++)
            {
                if (rends[r] == null || rends[r] is ParticleSystemRenderer || rends[r] is TrailRenderer) continue;
                for (int i = 0; i < bases[r].Length; i++)
                {
                    rends[r].GetPropertyBlock(block, i);
                    Color c = Color.Lerp(bases[r][i], charred, k);
                    // Brasas: pulsos naranjas sobre lo quemado.
                    c += new Color(0.5f, 0.15f, 0f) * k * (1f - k) * 2f * flick;
                    block.SetColor("_BaseColor", c);
                    rends[r].SetPropertyBlock(block, i);
                }
            }
            if (Random.value < 0.15f) Blood(Body, 6, 3f);
            yield return null;
        }

        // ---- 3. Revienta ----
        foreach (Renderer r in rends)
            if (r != null && !(r is ParticleSystemRenderer)) r.enabled = false;
        fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Gibs();
        Blood(Body, 120, 9f);
        ItemFx.Burst(Body, 80, 0.6f, new Color(1f, 0.5f, 0.1f), fx, 10f, 0.5f, 0.6f);
        ItemFx.Burst(Body, 50, 1.2f, new Color(0.15f, 0.12f, 0.12f, 0.8f), fx, 3f, 1.2f, 1.8f);
        audioSource.PlayOneShot(splat, GameSettings.SfxVolume);
        yield return new WaitForSecondsRealtime(1.5f);
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ---- Comentarios burlones ----
    private IEnumerator RoastLoop()
    {
        BuildRoastUi();
        yield return new WaitForSecondsRealtime(0.6f);
        while (true)
        {
            if (bag.Count == 0) { bag.AddRange(Roasts); }
            int i = Random.Range(0, bag.Count);
            string line = bag[i];
            bag.RemoveAt(i);
            // Entra de golpe, se escribe, y se va.
            roast.text = "";
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                roastGroup.alpha = t / 0.25f;
                roast.rectTransform.localScale = Vector3.one * (1.4f - 0.4f * (t / 0.25f));
                yield return null;
            }
            roastGroup.alpha = 1f;
            roast.rectTransform.localScale = Vector3.one;
            for (int n = 1; n <= line.Length; n++)
            {
                roast.text = "“" + line.Substring(0, n) + (n == line.Length ? "”" : "");
                yield return new WaitForSecondsRealtime(0.022f);
            }
            for (float t = 0f; t < 2.6f; t += Time.unscaledDeltaTime)
            {
                roast.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2f) * 1.5f);
                yield return null;
            }
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { roastGroup.alpha = 1f - t / 0.3f; yield return null; }
        }
    }

    private void BuildRoastUi()
    {
        Canvas canvas = UiKit.MakeCanvas("ComentariosMuerte", 70);
        Font font = null;
        foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include)) if (t.font != null && t.font.name.Contains("Pogo")) { font = t.font; break; }
        if (font == null) foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include)) if (t.font != null) { font = t.font; break; }
        RectTransform r = UiKit.Rect("Comentario", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 70f), new Vector2(1700f, 120f));
        roast = UiKit.Text(r, font, 44, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        roast.horizontalOverflow = HorizontalWrapMode.Wrap;
        roast.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
        roast.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(6f, -6f);
        roastGroup = roast.gameObject.AddComponent<CanvasGroup>();
        roastGroup.alpha = 0f;
    }

    // ---- Efectos ----
    private void Blood(Vector3 at, int count, float speed)
    {
        ParticleSystem ps = ItemFx.Burst(at, count, 0.3f, new Color(0.55f, 0.02f, 0.02f), fx, speed, 0.2f, 1.1f);
        var main = ps.main;
        main.gravityModifier = 2.2f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.03f, 0.03f), new Color(0.35f, 0f, 0f));
        // El material de efectos suma luz (la sangre saldría naranja): uno transparente normal.
        if (AlphaMat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AlphaMat;
    }

    private void BloodPool()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) return;
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pool.GetComponent<Collider>());
        pool.name = "CharcoDeSangre";
        pool.transform.position = hit.point + Vector3.up * 0.02f;
        pool.transform.localScale = new Vector3(0.1f, 0.01f, 0.1f);
        ItemFx.Paint(pool, new Color(0.35f, 0.01f, 0.01f), 0f);
        StartCoroutine(Grow(pool.transform, new Vector3(2.6f, 0.01f, 2.2f), 2.5f));
    }

    private static IEnumerator Grow(Transform t, Vector3 to, float seconds)
    {
        Vector3 from = t.localScale;
        for (float k = 0f; k < 1f && t != null; k += Time.unscaledDeltaTime / seconds)
        {
            t.localScale = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k));
            yield return null;
        }
    }

    private ParticleSystem Flames()
    {
        var go = new GameObject("LlamasPogoman");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.down * 0.2f;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;
        main.gravityModifier = -0.3f;
        var em = ps.emission;
        em.rateOverTime = 180f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.radius = 0.55f;
        sh.angle = 18f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.05f), 0.4f), new GradientColorKey(new Color(0.6f, 0.05f, 0f), 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) },
        };
        ItemFx.FadeOut(ps);
        ItemFx.Apply(ps, fx);
        ps.Play();

        var lg = new GameObject("LuzLlamas");
        lg.transform.SetParent(go.transform, false);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.5f, 0.15f);
        l.range = 9f;
        l.intensity = 4f;
        StartCoroutine(FlickerLight(l));
        return ps;
    }

    private static IEnumerator FlickerLight(Light l)
    {
        for (float t = 0f; t < 4f && l != null; t += Time.unscaledDeltaTime)
        {
            l.intensity = (3f + Mathf.PerlinNoise(t * 12f, 0f) * 3f) * Mathf.Clamp01((4f - t) / 1.5f);
            yield return null;
        }
        if (l != null) Destroy(l.gameObject);
    }

    private ParticleSystem Smoke()
    {
        ParticleSystem ps = new GameObject("HumoPogoman").AddComponent<ParticleSystem>();
        ps.transform.position = Body + Vector3.up * 1.2f;   // sale de arriba: no tapa las llamas
        ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startColor = new Color(0.1f, 0.08f, 0.08f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;
        var em = ps.emission;
        em.rateOverTime = 16f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.radius = 0.4f;
        sh.angle = 15f;
        ItemFx.FadeOut(ps);
        if (AlphaMat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AlphaMat;
        ps.Play();
        Destroy(ps.gameObject, 8f);
        return ps;
    }

    private void Gibs()
    {
        Collider own = GetComponent<Collider>();
        Color[] colors =
        {
            new Color(0.08f, 0.05f, 0.04f), new Color(0.12f, 0.07f, 0.05f), new Color(0.5f, 0.03f, 0.03f),
            new Color(0.7f, 0.08f, 0.06f), new Color(0.95f, 0.9f, 0.85f),   // un hueso por ahí
        };
        for (int i = 0; i < 22; i++)
        {
            var go = GameObject.CreatePrimitive(i % 7 == 0 ? PrimitiveType.Capsule : PrimitiveType.Cube);
            go.name = "PedazoPogoman";
            go.transform.position = Body + Random.insideUnitSphere * 0.4f;
            go.transform.rotation = Random.rotation;
            float s = Random.Range(0.12f, 0.32f);
            go.transform.localScale = new Vector3(s, s * Random.Range(0.7f, 1.6f), s);
            Color c = colors[Random.Range(0, colors.Length)];
            ItemFx.Paint(go, c, c.r > 0.9f ? 0f : 0.05f);
            if (own != null) Physics.IgnoreCollision(go.GetComponent<Collider>(), own);
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.linearVelocity = (Random.insideUnitSphere + Vector3.up * 1.3f).normalized * Random.Range(5f, 11f);
            body.angularVelocity = Random.insideUnitSphere * 15f;
            if (i % 3 == 0 && AlphaMat != null) ItemFx.Trail(go, new Color(0.5f, 0.02f, 0.02f), 0.12f, 0.35f, AlphaMat);
            Destroy(go, 12f);
        }
        // El resorte del pogo sale volando, intacto.
        var spring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spring.name = "ResorteDelPogo";
        spring.transform.position = Body;
        spring.transform.localScale = new Vector3(0.18f, 0.35f, 0.18f);
        ItemFx.Paint(spring, new Color(0.95f, 0.6f, 0.15f), 0.4f);
        if (own != null) Physics.IgnoreCollision(spring.GetComponent<Collider>(), own);
        var sb = spring.AddComponent<Rigidbody>();
        sb.linearVelocity = Vector3.up * 14f + Random.insideUnitSphere * 2f;
        sb.angularVelocity = new Vector3(20f, 3f, 0f);
        Destroy(spring, 12f);
    }

    // ---- Sonidos sintetizados ----
    private static void MakeSounds()
    {
        const int rate = 22050;
        var rng = new System.Random(13);
        int n = rate * 2;
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = (float)rng.NextDouble() * 2f - 1f;
            lp += (w - lp) * 0.35f;
            float crackle = rng.NextDouble() < 0.004 ? ((float)rng.NextDouble() * 2f - 1f) * 0.9f : 0f;
            d[i] = Mathf.Clamp((w - lp) * 0.35f * Mathf.Clamp01(t / 0.1f) * Mathf.Exp(-t * 0.9f) + crackle, -1f, 1f);
        }
        sizzle = AudioClip.Create("Chisporroteo", n, 1, rate, false);
        sizzle.SetData(d, 0);

        n = (int)(rate * 0.7f);
        d = new float[n];
        lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = (float)rng.NextDouble() * 2f - 1f;
            lp += (w - lp) * 0.08f;
            float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 45f, t / 0.3f) * t) * Mathf.Exp(-t * 12f);
            d[i] = Mathf.Clamp((lp * 3f * Mathf.Exp(-t * 7f) + thud) * 0.9f, -1f, 1f);
        }
        splat = AudioClip.Create("Splash", n, 1, rate, false);
        splat.SetData(d, 0);
    }
}
