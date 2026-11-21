using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Mapa con Tab: al empezar se toma una foto del mapa desde arriba; en pantalla empieza en negro
// y se va descubriendo por donde pasa Pogoman. Marca (solo en zonas descubiertas):
//   cofres cerrados = punto amarillo, pilares = morado grande, llaves sin recoger = ícono de llave.
public class MapScreen : MonoBehaviour
{
    [SerializeField] private Font font;
    [SerializeField] private Sprite keyIcon;
    [SerializeField] private AudioClip openClip;
    [Tooltip("Radio que se descubre alrededor de Pogoman (m).")]
    [SerializeField] private float revealRadius = 40f;
    [SerializeField] private int fogResolution = 128;
    [SerializeField] private int mapPixels = 320;

    public static bool IsOpen { get; private set; }

    private Transform player;
    private Rect world;                 // XZ del mapa
    private Texture2D fog;
    private Color32[] fogPixels;
    private bool fogDirty;
    private float revealTimer;

    private CanvasGroup group;
    private RectTransform mapRect, markerRoot;
    private RawImage mapImage, fogImage;
    private readonly List<Image> pool = new List<Image>();
    private Sprite dot, arrow;
    private AudioSource audioSource;
    private bool ready;

    private IEnumerator Start()
    {
        player = FindAnyObjectByType<PogoPlayer>()?.transform;
        audioSource = UiKit.UiAudio(gameObject);
        dot = MakeCircle(32);
        arrow = MakeArrow(32);
        BuildUi();
        yield return null;   // que el mapa termine de acomodarse (pilares, cofres)
        Texture2D photo = Capture();
        mapImage.texture = photo;
        fog = new Texture2D(fogResolution, fogResolution, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        fogPixels = new Color32[fogResolution * fogResolution];
        for (int i = 0; i < fogPixels.Length; i++) fogPixels[i] = new Color32(4, 2, 6, 255);
        fog.SetPixels32(fogPixels);
        fog.Apply();
        fogImage.texture = fog;
        ready = true;
    }

    // ---- Foto desde arriba ----
    private Texture2D Capture()
    {
        EstigiaGameHooks map = FindAnyObjectByType<EstigiaGameHooks>();
        Transform root = map != null ? map.transform : null;
        var hidden = new List<Renderer>();
        Bounds b = new Bounds(player != null ? player.position : Vector3.zero, Vector3.one * 50f);
        bool first = true;
        foreach (Renderer r in root != null ? root.GetComponentsInChildren<Renderer>() : FindObjectsByType<Renderer>())
        {
            Material m = r.sharedMaterial;
            bool sky = m != null && m.name.Contains("Cielo");
            if (sky || r.bounds.size.x > 800f) { if (r.enabled) { hidden.Add(r); r.enabled = false; } continue; }
            // El encuadre es solo la zona jugable: el suelo (no las rocas y el río de afuera).
            if (m == null || !m.name.Contains("Suelo")) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        foreach (Chest c in FindObjectsByType<Chest>()) b.Encapsulate(c.transform.position);
        foreach (BossPillar p in FindObjectsByType<BossPillar>()) b.Encapsulate(p.transform.position);
        float size = Mathf.Max(b.size.x, b.size.z) * 1.04f;
        world = new Rect(b.center.x - size / 2f, b.center.z - size / 2f, size, size);

        var go = new GameObject("CamaraMapa");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = size / 2f;
        cam.aspect = 1f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.01f, 0.02f);
        cam.nearClipPlane = 1f;
        cam.farClipPlane = b.size.y + 200f;
        go.transform.position = new Vector3(b.center.x, b.max.y + 50f, b.center.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // arriba de la imagen = +Z
        var rt = new RenderTexture(1024, 1024, 24);
        cam.targetTexture = rt;
        bool fogOn = RenderSettings.fog;
        RenderSettings.fog = false;
        cam.Render();
        cam.Render();   // el primer render a veces sale oscuro
        RenderSettings.fog = fogOn;
        foreach (Renderer r in hidden) r.enabled = true;

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var big = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        big.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        big.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        Destroy(go);
        rt.Release();

        // Estilo de mapa: pixelado, más oscuro y en tonos de pergamino quemado.
        var small = new Texture2D(mapPixels, mapPixels, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
        float step = 1024f / mapPixels;
        var px = new Color[mapPixels * mapPixels];
        for (int y = 0; y < mapPixels; y++)
            for (int x = 0; x < mapPixels; x++)
            {
                Color c = big.GetPixel(Mathf.FloorToInt((x + 0.5f) * step), Mathf.FloorToInt((y + 0.5f) * step));
                float l = c.r * 0.5f + c.g * 0.3f + c.b * 0.2f;
                Color tone = Color.Lerp(new Color(0.16f, 0.05f, 0.06f), new Color(0.95f, 0.6f, 0.42f), Mathf.Pow(Mathf.Clamp01(l * 1.8f), 0.8f));
                // El agua se queda azulada para ubicar el río.
                if (c.b > c.r * 1.15f && c.b > 0.15f) tone = Color.Lerp(tone, new Color(0.25f, 0.6f, 0.7f), 0.7f);
                px[y * mapPixels + x] = Color.Lerp(tone, c, 0.25f);
            }
        small.SetPixels(px);
        small.Apply();
        Destroy(big);
        return small;
    }

    // ---- UI ----
    private void BuildUi()
    {
        Canvas canvas = UiKit.MakeCanvas("HUDMapa", 9);
        canvas.transform.SetParent(transform, false);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        Transform root = canvas.transform;

        UiKit.Image(UiKit.Stretch("Oscuro", root), new Color(0f, 0f, 0f, 0.6f));
        RectTransform frame = UiKit.Rect("Marco", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(880, 880));
        UiKit.Image(frame, new Color(0.35f, 0.12f, 0.08f, 1f));
        mapRect = UiKit.Rect("Mapa", frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24, -24));
        mapImage = mapRect.gameObject.AddComponent<RawImage>();
        mapImage.raycastTarget = false;
        RectTransform fogRect = UiKit.Stretch("Niebla", mapRect);
        fogImage = fogRect.gameObject.AddComponent<RawImage>();
        fogImage.raycastTarget = false;
        markerRoot = UiKit.Stretch("Marcas", mapRect);

        UiKit.Text(UiKit.Rect("Titulo", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(800, 60)),
            font, 48, new Color(1f, 0.75f, 0.5f), TextAnchor.MiddleCenter, "MAPA DEL ESTIGIA");
        UiKit.Text(UiKit.Rect("Leyenda", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(1600, 40)),
            font, 26, new Color(0.95f, 0.85f, 0.75f), TextAnchor.MiddleCenter,
            "<color=#FFD23C>COFRE</color>     <color=#C040FF>PILAR</color>     <color=#FFE080>LLAVE</color>     TÚ          TAB  CERRAR");
    }

    private static Sprite MakeCircle(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        float r = s / 2f - 1f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
                t.SetPixel(x, y, d <= r - 3f ? Color.white : d <= r ? new Color(0, 0, 0, 1) : Color.clear);
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    private static Sprite MakeArrow(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float half = (s - y) * 0.42f;   // triángulo que apunta hacia arriba
                bool inside = Mathf.Abs(x - s / 2f) < half && y > 2;
                bool edge = Mathf.Abs(x - s / 2f) < half + 2f && y > 0;
                t.SetPixel(x, y, inside ? Color.white : edge ? Color.black : Color.clear);
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    // ---- Descubrir ----
    private Vector2 ToUv(Vector3 p) => new Vector2((p.x - world.x) / world.width, (p.z - world.y) / world.height);

    private void Reveal()
    {
        Vector2 uv = ToUv(player.position);
        int cx = Mathf.RoundToInt(uv.x * fogResolution), cy = Mathf.RoundToInt(uv.y * fogResolution);
        float rad = revealRadius / world.width * fogResolution;
        int r = Mathf.CeilToInt(rad) + 1;
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= fogResolution) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= fogResolution) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                byte a = (byte)(Mathf.Clamp01((d - rad * 0.7f) / (rad * 0.3f)) * 255f);
                int i = y * fogResolution + x;
                if (a < fogPixels[i].a)
                {
                    fogPixels[i].a = a;
                    fogDirty = true;
                }
            }
        }
    }

    private bool Revealed(Vector3 p)
    {
        Vector2 uv = ToUv(p);
        int x = Mathf.FloorToInt(uv.x * fogResolution), y = Mathf.FloorToInt(uv.y * fogResolution);
        if (x < 0 || y < 0 || x >= fogResolution || y >= fogResolution) return false;
        return fogPixels[y * fogResolution + x].a < 128;
    }

    private void Update()
    {
        if (!ready || player == null)
            return;

        revealTimer -= Time.unscaledDeltaTime;
        if (revealTimer <= 0f && !ArenaFight.InArena)
        {
            revealTimer = 0.2f;
            Reveal();
        }

        bool tab = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
        if (tab && !ArenaFight.InArena && !ChestUI.IsOpen && !LevelUpUI.IsOpen && (Time.timeScale > 0f || IsOpen))
        {
            IsOpen = !IsOpen;
            if (openClip != null)
                audioSource.PlayOneShot(openClip, 0.8f * GameSettings.SfxVolume);
        }
        if (IsOpen && (ArenaFight.InArena || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)))
            IsOpen = false;

        group.alpha = Mathf.MoveTowards(group.alpha, IsOpen ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (group.alpha <= 0f)
            return;

        if (fogDirty)
        {
            fog.SetPixels32(fogPixels);
            fog.Apply();
            fogDirty = false;
        }
        DrawMarkers();
    }

    private void DrawMarkers()
    {
        int used = 0;
        Vector2 size = mapRect.rect.size;

        void Mark(Vector3 worldPos, Sprite sprite, Color color, float px, float angle = 0f)
        {
            if (used >= pool.Count)
            {
                var rt = UiKit.Rect("Marca", markerRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 20f);
                Image im = UiKit.Image(rt, Color.white, sprite);
                pool.Add(im);
            }
            Image img = pool[used++];
            img.gameObject.SetActive(true);
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            Vector2 uv = ToUv(worldPos);
            img.rectTransform.anchoredPosition = new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);
            img.rectTransform.sizeDelta = Vector2.one * px;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        foreach (Chest c in FindObjectsByType<Chest>())
            if (!c.Opened && Revealed(c.transform.position))
                Mark(c.transform.position, dot, new Color(1f, 0.82f, 0.24f), 14f);

        float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
        foreach (BossPillar p in FindObjectsByType<BossPillar>())
            if (Revealed(p.transform.position))
                Mark(p.transform.position, dot, new Color(0.69f, 0.12f, 1f), 28f * pulse);

        foreach (KeyItem k in FindObjectsByType<KeyItem>())
            if (Revealed(k.transform.position))
                Mark(k.transform.position, keyIcon != null ? keyIcon : dot, keyIcon != null ? Color.white : new Color(1f, 0.9f, 0.4f), 34f);

        // Pogoman: flecha que apunta hacia donde mira la cámara.
        Camera cam = Camera.main;
        float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        Mark(player.position, arrow, Color.white, 26f, -yaw);

        for (int i = used; i < pool.Count; i++)
            pool[i].gameObject.SetActive(false);
    }
}
