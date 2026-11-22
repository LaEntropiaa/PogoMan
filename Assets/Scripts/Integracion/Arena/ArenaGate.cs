using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// La puerta del mapa (Puerta_ del Estigia): con las 4 llaves se abre, se marca en pantalla
// y al acercarte con E te lleva a la arena del Diablo.
public class ArenaGate : MonoBehaviour
{
    [SerializeField] private ArenaFight fight;
    [Tooltip("Inicio del nombre del objeto de la puerta en el mapa.")]
    [SerializeField] private string doorName = "Puerta_";
    [SerializeField] private float interactDistance = 7f;
    [SerializeField] private Font font;
    [SerializeField] private Material fxMaterial;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip enterClip;

    public bool IsOpen { get; private set; }
    public AudioClip MapMusic { get; private set; }
    // A dónde regresa Pogoman si acepta el trato: frente a la puerta, del lado del mapa.
    public Vector3 ReturnPoint => doorPoint + Vector3.up * 1.5f;

    // Después del trato con el Diablo la puerta se cierra para siempre.
    public void Close()
    {
        IsOpen = false;
        if (marker != null) marker.gameObject.SetActive(false);
        if (prompt != null) prompt.text = "";
        var portal = GameObject.Find("PortalArena"); if (portal) Destroy(portal);
        var beam = GameObject.Find("Columna"); if (beam) Destroy(beam);
        enabled = false;
    }

    private KeyManager keys;
    private PogoPlayer player;
    private Vector3 doorPoint;      // al pie de la puerta, del lado de afuera
    private Vector3 doorTop;
    private bool hasDoor;
    private bool entering;
    private AudioSource audioSource;

    private CanvasGroup fade;
    private RectTransform marker;
    private Text markerText, prompt, banner, bannerSub;
    private float bannerTimer;

    private void Start()
    {
        keys = FindAnyObjectByType<KeyManager>();
        player = FindAnyObjectByType<PogoPlayer>();
        audioSource = UiKit.UiAudio(gameObject);
        FindDoor();
        BuildUi();
    }

    private void FindDoor()
    {
        foreach (Renderer r in FindObjectsByType<Renderer>())
        {
            if (!r.name.StartsWith(doorName))
                continue;
            Bounds b = r.bounds;
            doorTop = new Vector3(b.center.x, b.max.y, b.center.z);
            doorPoint = new Vector3(b.center.x, b.min.y, b.center.z);
            hasDoor = true;
            return;
        }
        Debug.LogWarning("ArenaGate: no encontré la puerta \"" + doorName + "\" en el mapa.");
    }

    private void Update()
    {
        if (player == null || entering)
            return;

        if (!IsOpen && keys != null && keys.AllKeysCollected)
            Open();

        float dt = Time.unscaledDeltaTime;
        bannerTimer -= dt;
        float a = Mathf.Clamp01(bannerTimer / 0.5f);
        banner.color = new Color(banner.color.r, banner.color.g, banner.color.b, a);
        bannerSub.color = new Color(bannerSub.color.r, bannerSub.color.g, bannerSub.color.b, a);

        if (!IsOpen || ArenaFight.InArena)
        {
            marker.gameObject.SetActive(false);
            prompt.text = "";
            return;
        }

        UpdateMarker();

        Vector3 d = player.transform.position - doorPoint;
        bool near = new Vector2(d.x, d.z).magnitude < interactDistance && Mathf.Abs(d.y) < 8f;
        prompt.text = near ? "E  ENTRAR A LA ARENA DEL DIABLO" : "";
        if (near && Time.timeScale > 0f && InteractPressed())
            StartCoroutine(Enter());
    }

    private static bool InteractPressed() =>
        (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
        (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

    private void Open()
    {
        IsOpen = true;
        banner.text = "¡LA PUERTA DEL INFIERNO SE ABRIÓ!";
        bannerSub.text = "Sigue la marca y entra con E";
        bannerTimer = 4.5f;
        if (openClip != null)
            audioSource.PlayOneShot(openClip, GameSettings.SfxVolume);
        if (hasDoor)
            BuildPortalFx();
    }

    // Remolino de chispas y luz en la puerta para que se vea que está abierta.
    private void BuildPortalFx()
    {
        var go = new GameObject("PortalArena");
        go.transform.position = Vector3.Lerp(doorPoint, doorTop, 0.4f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.35f, 0.2f);
        light.range = 14f;
        light.intensity = 4f;
        light.shadows = LightShadows.None;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 1.4f;
        main.startSpeed = 1.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(0.8f, 0.15f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f;
        main.maxParticles = 400;
        var em = ps.emission;
        em.rateOverTime = 90f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(1.5f, (doorTop.y - doorPoint.y) * 0.35f);
        ItemFx.FadeOut(ps);
        ItemFx.Apply(ps, fxMaterial);
        ps.Play();

        // Columna de luz que se ve desde todo el mapa.
        var beam = new GameObject("Columna");
        beam.transform.position = doorTop;
        var bp = beam.AddComponent<ParticleSystem>();
        bp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var bm = bp.main;
        bm.startLifetime = 3f;
        bm.startSpeed = 12f;
        bm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        bm.startColor = new Color(1f, 0.4f, 0.15f, 0.8f);
        bm.maxParticles = 300;
        var be = bp.emission;
        be.rateOverTime = 60f;
        var bs = bp.shape;
        bs.shapeType = ParticleSystemShapeType.Cone;
        bs.angle = 2f;
        bs.radius = 0.6f;
        beam.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ItemFx.Apply(bp, fxMaterial);
        bp.Play();
    }

    private void UpdateMarker()
    {
        Camera cam = Camera.main;
        if (cam == null || !hasDoor)
            return;

        marker.gameObject.SetActive(true);
        var canvasRect = (RectTransform)marker.parent;
        Vector3 world = doorTop + Vector3.up * 2f;
        Vector3 sp = cam.WorldToViewportPoint(world);
        bool behind = sp.z < 0f;
        if (behind)
        {
            sp.x = 1f - sp.x;
            sp.y = 0f;
        }
        // Pegado a los bordes si la puerta está fuera de la pantalla.
        sp.x = Mathf.Clamp(sp.x, 0.06f, 0.94f);
        sp.y = Mathf.Clamp(sp.y, 0.1f, 0.9f);
        Vector2 size = canvasRect.rect.size;
        marker.anchoredPosition = new Vector2((sp.x - 0.5f) * size.x, (sp.y - 0.5f) * size.y);
        float dist = Vector3.Distance(player.transform.position, doorPoint);
        markerText.text = "PUERTA\n" + Mathf.RoundToInt(dist) + " M";
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        markerText.color = Color.Lerp(new Color(1f, 0.55f, 0.25f), Color.white, pulse * 0.4f);
    }

    private IEnumerator Enter()
    {
        entering = true;
        prompt.text = "";
        if (enterClip != null)
            audioSource.PlayOneShot(enterClip, GameSettings.SfxVolume);

        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            fade.alpha = t / 0.7f;
            yield return null;
        }
        fade.alpha = 1f;
        bannerTimer = 0f;
        banner.text = bannerSub.text = "";
        marker.gameObject.SetActive(false);

        // Se acaba el mapa: fuera demonios, pilares y hordas.
        foreach (EnemyTracker e in new System.Collections.Generic.List<EnemyTracker>(EnemyTracker.All))
            if (e != null)
                Destroy(e.gameObject);
        foreach (BossPillar p in FindObjectsByType<BossPillar>())
            p.gameObject.SetActive(false);
        if (EnemyDirector.Instance != null)
            EnemyDirector.Instance.ArenaMode = true;

        fight.PrepareArena();
        yield return null;

        // Pogoman al inicio de la arena, mirando al Diablo.
        var rb = player.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.position = fight.PlayerStart + Vector3.up * 0.5f;
        player.transform.position = rb.position;
        PogoCamera cam = FindAnyObjectByType<PogoCamera>();
        if (cam != null)
        {
            PogoBridge.SetFloat(cam, "yaw", fight.PlayerFacing.eulerAngles.y);
            PogoBridge.Set(cam, "currentPosition", player.transform.position - fight.PlayerFacing * Vector3.forward * 6f + Vector3.up * 3f);
        }
        if (PlayerVitals.Instance != null)
            PlayerVitals.Instance.InvulnerableUntil = Time.time + 3f;

        GameMusic gm = FindAnyObjectByType<GameMusic>();
        if (gm != null)
        {
            MapMusic = gm.CurrentClip;
            gm.SwitchTo(fight.Music, 0.3f);
        }

        yield return new WaitForSecondsRealtime(0.5f);
        fight.Begin(player);

        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
        {
            fade.alpha = 1f - t / 0.8f;
            yield return null;
        }
        fade.alpha = 0f;
        entering = false;   // ya dentro: Update ve ArenaFight.InArena y no muestra nada
    }

    private void BuildUi()
    {
        Canvas canvas = UiKit.MakeCanvas("HUDPuerta", 7);
        canvas.transform.SetParent(transform, false);
        Transform root = canvas.transform;

        marker = UiKit.Rect("Marca", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 90));
        markerText = UiKit.Text(UiKit.Stretch("Texto", marker), font, 30, new Color(1f, 0.55f, 0.25f), TextAnchor.MiddleCenter);
        markerText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
        marker.gameObject.SetActive(false);

        prompt = UiKit.Text(UiKit.Rect("Prompt", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 190), new Vector2(1200, 60)), font, 40, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleCenter);
        prompt.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);

        banner = UiKit.Text(UiKit.Rect("Aviso", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 200), new Vector2(1800, 110)), font, 76, new Color(1f, 0.6f, 0.25f), TextAnchor.MiddleCenter);
        banner.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4, -4);
        bannerSub = UiKit.Text(UiKit.Rect("AvisoSub", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 125), new Vector2(1400, 50)), font, 36, new Color(1f, 0.85f, 0.7f), TextAnchor.MiddleCenter);

        Canvas top = UiKit.MakeCanvas("Fundido", 50);
        top.transform.SetParent(transform, false);
        fade = top.gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0f;
        fade.blocksRaycasts = false;
        fade.interactable = false;
        UiKit.Image(UiKit.Stretch("Negro", top.transform), Color.black);
    }
}
