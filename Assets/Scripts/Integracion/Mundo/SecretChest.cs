using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Cofre especial escondido en la gruta (morado con dorado, un poco más grande).
// Pide una clave para abrirse; con la correcta se activa su evento y regala un objeto.
public class SecretChest : MonoBehaviour
{
    private static readonly string[] Markers = { "Alan_A_", "Chago_C_", "Cristix_CX_", "Jerry_G_" };
    private const string Secret = "TLBZ";
    private const float Reach = 3.2f;

    private static readonly Color Purple = new Color(0.5f, 0.12f, 0.85f);
    private static readonly Color Gold = new Color(1f, 0.78f, 0.25f);

    private Transform player;
    private Renderer[] renderers;
    private bool opened, panelOpen;
    private Font font;
    private Text prompt;
    private GameObject panel;
    private InputField field;
    private Text feedback;
    private Light glow;
    private ParticleSystem sparkles;
    private AudioSource audioSource;

    public static SecretChest Create(Transform mapRoot)
    {
        Vector3 sum = Vector3.zero;
        int found = 0;
        foreach (Transform t in mapRoot.GetComponentsInChildren<Transform>(true))
            foreach (string m in Markers)
                if (t.name.StartsWith(m))
                {
                    Renderer r = t.GetComponent<Renderer>();
                    sum += r != null ? r.bounds.center : t.position;
                    found++;
                }
        if (found == 0) return null;
        Vector3 center = sum / found;

        // Piso de la gruta (sin chocar con el techo: se busca desde poco arriba).
        Vector3 ground = center;
        float best = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(center + Vector3.up * 1.2f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
            if (h.rigidbody == null && h.distance < best) { best = h.distance; ground = h.point; }

        Chest template = null;
        foreach (Chest c in FindObjectsByType<Chest>()) { template = c; break; }
        if (template == null) return null;

        var holder = new GameObject("CofreSecretoHolder");
        holder.SetActive(false);
        Renderer tr = template.GetComponent<Renderer>();
        float baseOffset = tr != null ? template.transform.position.y - tr.bounds.min.y : 0f;
        GameObject copy = Instantiate(template.gameObject, holder.transform);
        copy.name = "CofreDeLosAntiguosGuerreros";
        DestroyImmediate(copy.GetComponent<Chest>());
        foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
            if (child != copy.transform && (child.GetComponent<Light>() != null || child.GetComponent<ParticleSystem>() != null))
                DestroyImmediate(child.gameObject);
        const float size = 1.35f;
        copy.transform.localScale = template.transform.lossyScale * size;
        copy.transform.position = ground + Vector3.up * baseOffset * size;
        // Que mire hacia el centro de los esqueletos (de frente al que entra).
        copy.transform.rotation = template.transform.rotation;
        copy.transform.SetParent(null, true);
        Destroy(holder);
        var chest = copy.AddComponent<SecretChest>();
        return chest;
    }

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>()?.transform;
        renderers = GetComponentsInChildren<Renderer>();
        foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include)) if (t.font != null) { font = t.font; break; }
        audioSource = UiKit.UiAudio(gameObject);
        Paint(1f);

        var lg = new GameObject("BrilloSecreto");
        lg.transform.SetParent(transform, false);
        lg.transform.position = Center + Vector3.up * 1f;
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.75f, 0.35f, 1f);
        glow.range = 7f;
        glow.shadows = LightShadows.None;

        sparkles = new GameObject("ChispasSecretas").AddComponent<ParticleSystem>();
        sparkles.transform.SetParent(transform, false);
        sparkles.transform.position = Center;
        sparkles.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparkles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.45f, 1f), Gold);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var em = sparkles.emission;
        em.rateOverTime = 14f;
        var sh = sparkles.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.8f;
        ItemFx.FadeOut(sparkles);
        ItemFx.Apply(sparkles, ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null);
        sparkles.Play();

        Canvas canvas = UiKit.MakeCanvas("HUDCofreSecreto", 35);
        canvas.transform.SetParent(transform, false);
        prompt = UiKit.Text(UiKit.Rect("Prompt", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 190), new Vector2(1400, 60)), font, 38, Gold, TextAnchor.MiddleCenter);
        prompt.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
    }

    private Vector3 Center => renderers != null && renderers.Length > 0 ? renderers[0].bounds.center : transform.position;

    // Morado con detalles dorados (el primer material es el cuerpo, los demás los herrajes).
    private void Paint(float brightness)
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            int n = r.sharedMaterials.Length;
            for (int i = 0; i < n; i++)
            {
                r.GetPropertyBlock(block, i);
                Color c = i == 0 && n > 1 ? Gold : Purple;   // cuerpo morado, herrajes dorados
                block.SetColor("_BaseColor", c * brightness);
                r.SetPropertyBlock(block, i);
            }
        }
    }

    private void Update()
    {
        if (player == null || prompt == null) return;
        if (glow != null && !opened)
            glow.intensity = 2f + Mathf.Sin(Time.time * 2.4f) * 0.8f;
        if (opened || panelOpen) { prompt.text = ""; return; }

        bool near = Vector3.Distance(player.position, Center) < Reach && Time.timeScale > 0f && !ChestUI.IsOpen;
        prompt.text = near ? "E  ABRIR EL COFRE DE LOS ANTIGUOS GUERREROS" : "";
        if (!near) return;
        bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
                       (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
        if (pressed) OpenPanel();
    }

    // ---- Ventana de la clave ----
    private void OpenPanel()
    {
        panelOpen = true;
        Time.timeScale = 0f;
        SceneNav.SetGameplayCursor(false);
        Canvas canvas = UiKit.MakeCanvas("ClaveCofre", 45);
        panel = canvas.gameObject;
        Transform r = canvas.transform;
        UiKit.Image(UiKit.Stretch("Fondo", r), new Color(0.05f, 0f, 0.08f, 0.8f)).raycastTarget = true;
        RectTransform box = UiKit.Rect("Caja", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 380f));
        FancyBar.Stretch(FancyBar.Img("Oro", box, Gold), -6f);
        FancyBar.Stretch(FancyBar.Img("Morado", box, new Color(0.16f, 0.04f, 0.26f)), 0f);
        Text title = UiKit.Text(UiKit.Rect("Titulo", box, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(880f, 70f)),
            font, 46, Color.white, TextAnchor.MiddleCenter, "EL COFRE PIDE UNA CLAVE");
        var fx = title.gameObject.AddComponent<PixelTextFx>();
        fx.top = new Color(1f, 0.95f, 0.7f); fx.bottom = Gold; fx.extrudeFront = new Color(0.3f, 0.05f, 0.45f); fx.depth = 3;

        RectTransform input = UiKit.Rect("Campo", box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(560f, 76f));
        Image bg = UiKit.Image(input, new Color(0.3f, 0.12f, 0.45f));
        bg.raycastTarget = true;
        Text text = UiKit.Text(UiKit.Stretch("Texto", input, 12f), font, 44, Color.white, TextAnchor.MiddleCenter);
        text.supportRichText = false;
        Text ph = UiKit.Text(UiKit.Stretch("Placeholder", input, 12f), font, 36, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter, "CLAVE");
        field = input.gameObject.AddComponent<InputField>();
        field.textComponent = text;
        field.placeholder = ph;
        field.targetGraphic = bg;
        field.characterLimit = 12;
        field.onEndEdit.AddListener(_ =>
        {
            if (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed)) Try();
        });
        feedback = UiKit.Text(UiKit.Rect("Aviso", box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(880f, 40f)),
            font, 26, new Color(1f, 0.8f, 0.9f), TextAnchor.MiddleCenter, "ENTER PARA PROBAR  ·  ESC PARA SALIR");
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(input.gameObject);
        field.ActivateInputField();
        StartCoroutine(PanelLoop(box));
    }

    private IEnumerator PanelLoop(RectTransform box)
    {
        float shake = 0f;
        while (panelOpen && panel != null)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { ClosePanel(true); yield break; }
            if (feedback.color.g < 0.5f) shake = 1f;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 3f);
            box.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 60f) * 14f * shake, 0f);
            yield return null;
        }
    }

    private void ClosePanel(bool resume)
    {
        panelOpen = false;
        if (panel != null) Destroy(panel);
        if (resume) { Time.timeScale = 1f; SceneNav.SetGameplayCursor(true); }
    }

    private void Try()
    {
        if (field == null) return;
        if (field.text.Trim().ToUpperInvariant() != Secret)
        {
            feedback.text = "CLAVE INCORRECTA";
            feedback.color = new Color(1f, 0.3f, 0.3f);
            field.text = "";
            field.ActivateInputField();
            StartCoroutine(ResetFeedback());
            return;
        }
        ClosePanel(false);
        opened = true;
        StartCoroutine(Event());
    }

    private IEnumerator ResetFeedback()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        if (feedback != null) feedback.color = new Color(1f, 0.55f, 0.6f);
    }

    // ---- El evento: la última ventisca ----
    private IEnumerator Event()
    {
        Material fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        Time.timeScale = 1f;
        ItemFx.Burst(Center, 120, 1f, new Color(0.8f, 0.45f, 1f), fx, 10f, 0.4f, 1f);
        ItemFx.Burst(Center, 80, 0.8f, Gold, fx, 7f, 0.3f, 1.2f);
        if (glow != null) { glow.intensity = 12f; glow.range = 18f; glow.color = new Color(0.85f, 0.9f, 1f); }
        Blizzard(fx);
        audioSource.PlayOneShot(MakeWind(), GameSettings.SfxVolume);
        CameraShake.Add(1.2f, 0.5f);

        // Texto grande, en dos renglones, que se escribe solo.
        Canvas canvas = UiKit.MakeCanvas("LaUltimaVentisca", 44);
        Image white = UiKit.Image(UiKit.Stretch("Blanco", canvas.transform), new Color(0.9f, 0.95f, 1f, 0f));
        white.raycastTarget = false;
        Text line1 = Line(canvas.transform, 60f, 52);
        Text line2 = Line(canvas.transform, -30f, 80);
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { white.color = new Color(0.9f, 0.95f, 1f, 0.6f * (1f - t / 0.5f)); yield return null; }
        white.color = Color.clear;
        yield return Type(line1, "EL REGALO DE LOS ANTIGUOS GUERREROS,");
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Type(line2, "LA ÚLTIMA VENTISCA");
        yield return new WaitForSecondsRealtime(2.2f);
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            line1.color = line2.color = new Color(1f, 1f, 1f, 1f - t / 0.6f);
            yield return null;
        }
        Destroy(canvas.gameObject);
        Paint(0.45f);
        if (sparkles != null) sparkles.Stop();

        // El regalo: ZA WARUDO (la Estampita de San Pogo detiene el tiempo cuando ibas a morir).
        ChestUI chestUi = FindAnyObjectByType<ChestUI>();
        ItemDatabase db = chestUi != null ? PogoBridge.Get<ItemDatabase>(chestUi, "database") : null;
        ItemDefinition gift = db != null ? db.items.Find(i => i != null && i.id == ItemEffects.Estampita) : null;
        AudioClip za = ItemEffects.Instance != null ? PogoBridge.Get<AudioClip>(ItemEffects.Instance, "zaWarudoClip") : null;
        if (za != null) audioSource.PlayOneShot(za, GameSettings.SfxVolume);
        if (gift != null && chestUi != null)
        {
            bool done = false;
            chestUi.ShowGift(gift, () => done = true);
            while (!done) yield return null;
        }
        SceneNav.SetGameplayCursor(true);
    }

    private Text Line(Transform parent, float y, int size)
    {
        Text t = UiKit.Text(UiKit.Rect("Linea", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1800f, 110f)),
            font, size, Color.white, TextAnchor.MiddleCenter);
        var fx = t.gameObject.AddComponent<PixelTextFx>();
        fx.top = new Color(0.95f, 0.98f, 1f); fx.bottom = new Color(0.55f, 0.75f, 1f);
        fx.extrudeFront = new Color(0.25f, 0.08f, 0.45f); fx.extrudeBack = new Color(0.08f, 0.02f, 0.15f); fx.depth = 4; fx.step = 3f; fx.shineEvery = 2f;
        return t;
    }

    private static IEnumerator Type(Text t, string s)
    {
        for (int i = 1; i <= s.Length; i++) { t.text = s.Substring(0, i); yield return new WaitForSecondsRealtime(0.045f); }
    }

    // Copos que soplan alrededor de Pogoman.
    private void Blizzard(Material fx)
    {
        if (player == null) return;
        var ps = new GameObject("UltimaVentisca").AddComponent<ParticleSystem>();
        ps.transform.SetParent(player, false);
        ps.transform.localPosition = new Vector3(-8f, 4f, 0f);
        ps.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 6f;
        main.loop = false;
        main.startLifetime = 2.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.75f, 0.85f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.15f;
        main.maxParticles = 900;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission;
        em.rateOverTime = 260f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(14f, 8f, 1f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.5f;
        noise.frequency = 0.6f;
        ItemFx.Apply(ps, fx);
        ps.Play();
    }

    private static AudioClip MakeWind()
    {
        const int rate = 22050;
        int n = rate * 6;
        var d = new float[n];
        var rng = new System.Random(77);
        float lp = 0f, bp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = (float)rng.NextDouble() * 2f - 1f;
            float gust = 0.5f + 0.5f * Mathf.PerlinNoise(t * 0.8f, 3f);
            lp += (w - lp) * (0.02f + 0.06f * gust);
            bp += (lp - bp) * 0.3f;
            float env = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((6f - t) / 1.5f);
            d[i] = Mathf.Clamp((lp - bp) * 9f * gust * env + Mathf.Sin(2f * Mathf.PI * (420f + 120f * gust) * t) * 0.03f * env, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("UltimaVentisca", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
