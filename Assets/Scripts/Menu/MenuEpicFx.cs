using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Efectos del menú principal (se arma desde código, lo agrega MainMenuController):
//  - Brasas que suben por toda la pantalla, viñeta que respira y rayos de fuego girando detrás del logo.
//  - El logo flota y "respira", con el lema "Un salto al infierno..." debajo.
//  - Letras estilizadas sin perder el pixel (PixelTextFx: degradado de fuego + sombra extruida + brillo).
//  - Tablero TOP 10 a la derecha que entra deslizándose, fila por fila.
public class MenuEpicFx : MonoBehaviour
{
    private const int EmberCount = 70;
    private const float BoardScale = 0.68f;

    private MainMenuController menu;
    private MenuIntroSequence intro;
    private RectTransform root, logo, rays, board;
    private CanvasGroup boardGroup, raysGroup;
    private Image vignette;
    private Font font;
    private Vector2 logoBase;
    private Vector3 logoScale;
    private bool started;
    private float since;
    private Text tagline;
    private CanvasGroup taglineGroup;

    private struct Ember { public RectTransform rect; public Image img; public Vector2 pos, vel; public float life, max, phase, size; }
    private Ember[] embers;
    private readonly List<RectTransform> rows = new List<RectTransform>();
    private readonly List<Graphic> mineRow = new List<Graphic>();

    private static Sprite white, raySprite, vignetteSprite;

    private void Start()
    {
        menu = GetComponent<MainMenuController>();
        intro = PogoBridge.Get<MenuIntroSequence>(menu, "intro");
        CanvasGroup main = PogoBridge.Get<CanvasGroup>(menu, "mainButtonsGroup");
        Canvas canvas = main != null ? main.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        root = (RectTransform)canvas.rootCanvas.transform;
        logo = intro != null ? PogoBridge.Get<RectTransform>(intro, "logo") : null;

        // Letras: botones con fuego y títulos de los paneles.
        foreach (MenuButtonFx b in FindObjectsByType<MenuButtonFx>(FindObjectsInactive.Include))
        {
            Text label = PogoBridge.Get<Text>(b, "label");
            if (label == null) continue;
            if (font == null) font = label.font;
            var fx = label.gameObject.AddComponent<PixelTextFx>();
            fx.top = new Color(1f, 1f, 0.92f);
            fx.bottom = new Color(1f, 0.62f, 0.38f);
            fx.depth = 3;
            fx.step = 2f;
            fx.shineEvery = 6f;
            fx.shineOffset = rows.Count * 0.35f + Random.value * 0.2f;
        }
        foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include))
            if (t.name == "Titulo" && t.GetComponent<PixelTextFx>() == null)
            {
                var fx = t.gameObject.AddComponent<PixelTextFx>();
                fx.top = new Color(1.4f, 0.9f, 0.6f);
                fx.bottom = new Color(0.9f, 0.5f, 0.4f);
                fx.extrudeFront = new Color(0.55f, 0.33f, 0.14f, 0.85f);
                fx.extrudeBack = new Color(0.3f, 0.16f, 0.06f, 0.6f);
                fx.depth = 2;
                fx.shineEvery = 4f;
            }

        BuildBackgroundFx();
        BuildBoard();
        BuildTagline();
    }

    // ------------------------------------------------------------------ sprites hechos en código
    private static Sprite White()
    {
        if (white == null)
        {
            var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            white = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
        return white;
    }

    // Rayos de fuego en baja resolución (se ven pixelados a propósito).
    private static Sprite Rays()
    {
        if (raySprite != null) return raySprite;
        const int n = 96;
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx);
                float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 8f)), 14f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Cos(a * 3f)));
                float fall = Mathf.Clamp01(1f - r);
                float v = Mathf.Clamp01(ray * fall * fall * 1.2f + Mathf.Clamp01(0.45f - r) * 0.9f);
                v = Mathf.Floor(v * 5f) / 5f;   // en escalones, tipo pixel art
                px[y * n + x] = new Color(1f, 1f, 1f, v);
            }
        tex.SetPixels(px);
        tex.Apply();
        return raySprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    private static Sprite Vignette()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int n = 64;
        var tex = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, (r - 0.55f) / 0.75f));
            }
        tex.SetPixels(px);
        tex.Apply();
        return vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    private Image Img(string name, Transform parent, Sprite sprite, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    private static RectTransform Stretch(Graphic g)
    {
        RectTransform r = g.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    // ------------------------------------------------------------------ fondo: brasas, viñeta, rayos, relámpago
    private void BuildBackgroundFx()
    {
        var layer = new GameObject("EfectosMenu", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(root, false);
        layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one; layer.offsetMin = layer.offsetMax = Vector2.zero;
        layer.SetSiblingIndex(0);   // detrás de todo lo demás del menú (pero encima del fondo 3D)

        vignette = Img("Vineta", layer, Vignette(), new Color(0.12f, 0f, 0f, 0.75f));
        Stretch(vignette);

        embers = new Ember[EmberCount];
        for (int i = 0; i < EmberCount; i++)
        {
            Image img = Img("Brasa", layer, White(), Color.clear);
            embers[i].img = img;
            embers[i].rect = img.rectTransform;
            Respawn(ref embers[i], true);
        }

        // Rayos detrás del logo.
        if (logo != null)
        {
            Image r = Img("RayosLogo", logo.parent, Rays(), new Color(1f, 0.45f, 0.1f, 1f));
            rays = r.rectTransform;
            rays.anchorMin = logo.anchorMin; rays.anchorMax = logo.anchorMax; rays.pivot = new Vector2(0.5f, 0.5f);
            rays.SetSiblingIndex(logo.GetSiblingIndex());
            rays.sizeDelta = new Vector2(1050f, 1050f);
            raysGroup = r.gameObject.AddComponent<CanvasGroup>();
            raysGroup.alpha = 0f;
        }

    }

    private Vector2 Size => root.rect.size;

    private void Respawn(ref Ember e, bool anywhere)
    {
        Vector2 s = Size;
        e.pos = new Vector2(Random.Range(-0.5f, 0.5f) * s.x, anywhere ? Random.Range(-0.5f, 0.5f) * s.y : -s.y * 0.5f - 20f);
        e.vel = new Vector2(Random.Range(-15f, 15f), Random.Range(40f, 120f));
        e.max = Random.Range(4f, 9f);
        e.life = anywhere ? Random.Range(0f, e.max) : 0f;
        e.phase = Random.value * 10f;
        e.size = Random.value < 0.15f ? 10f : Random.value < 0.5f ? 6f : 4f;   // tamaños de pixel "enteros"
        e.rect.sizeDelta = Vector2.one * e.size;
    }

    // ------------------------------------------------------------------ lema bajo el logo
    private const string TaglineText = "UN SALTO AL INFIERNO...";

    private void BuildTagline()
    {
        if (logo == null || font == null) return;
        var go = new GameObject("Lema", typeof(RectTransform));
        go.transform.SetParent(logo, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, 6f);
        r.sizeDelta = new Vector2(1600f, 110f);
        tagline = go.AddComponent<Text>();
        tagline.font = font;
        tagline.fontSize = 72;
        tagline.alignment = TextAnchor.UpperCenter;
        tagline.horizontalOverflow = HorizontalWrapMode.Overflow;
        tagline.color = Color.white;
        tagline.raycastTarget = false;
        tagline.text = "";
        var fx = go.AddComponent<PixelTextFx>();
        fx.top = new Color(1f, 0.92f, 0.55f);
        fx.bottom = new Color(0.95f, 0.25f, 0.08f);
        fx.depth = 4;
        fx.step = 3f;
        fx.extrudeFront = new Color(0.4f, 0.04f, 0.02f, 1f);
        fx.shineEvery = 5f;
        taglineGroup = go.AddComponent<CanvasGroup>();
        taglineGroup.alpha = 0f;
    }

    // Se escribe letra por letra cuando termina la entrada y luego tiembla como brasa.
    private void UpdateTagline(float time)
    {
        if (tagline == null) return;
        int n = Mathf.Clamp(Mathf.FloorToInt((since - 0.2f) * 16f), 0, TaglineText.Length);
        if (tagline.text.Length != n) tagline.text = TaglineText.Substring(0, n);
        taglineGroup.alpha = Mathf.Clamp01(since * 3f) * (0.88f + 0.12f * Mathf.Sin(time * 3.3f));
        tagline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.1f + 1f) * 0.6f);
    }

    // ------------------------------------------------------------------ tablero TOP 10
    private void BuildBoard()
    {
        board = new GameObject("TableroTop10", typeof(RectTransform)).GetComponent<RectTransform>();
        board.SetParent(root, false);
        board.anchorMin = board.anchorMax = new Vector2(1f, 0.5f);
        board.pivot = new Vector2(1f, 0.5f);
        board.sizeDelta = new Vector2(520f, 610f);
        board.anchoredPosition = new Vector2(900f, -110f);
        board.localScale = Vector3.one * BoardScale;
        boardGroup = board.gameObject.AddComponent<CanvasGroup>();
        boardGroup.alpha = 0f;
        boardGroup.interactable = false;
        boardGroup.blocksRaycasts = false;

        // Marco de pixel: borde naranja, borde oscuro y fondo.
        Stretch(Img("Borde", board, White(), new Color(0.95f, 0.45f, 0.1f, 0.95f)));
        RectTransform inner = Stretch(Img("BordeOscuro", board, White(), new Color(0.2f, 0.02f, 0.02f, 1f)));
        inner.offsetMin = new Vector2(6f, 6f); inner.offsetMax = new Vector2(-6f, -6f);
        RectTransform bg = Stretch(Img("Fondo", board, White(), new Color(0.07f, 0.01f, 0.02f, 0.86f)));
        bg.offsetMin = new Vector2(12f, 12f); bg.offsetMax = new Vector2(-12f, -12f);
        // Esquinas (cuadritos de pixel).
        foreach (Vector2 c in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
        {
            RectTransform k = Img("Esquina", board, White(), new Color(1f, 0.8f, 0.3f)).rectTransform;
            k.anchorMin = k.anchorMax = c; k.pivot = new Vector2(0.5f, 0.5f);
            k.sizeDelta = new Vector2(18f, 18f);
        }

        Text title = Label(board, "TOP 10", 58, TextAnchor.UpperCenter, new Vector2(0f, -22f), new Vector2(480f, 70f), new Color(1f, 0.95f, 0.8f));
        var tfx = title.gameObject.AddComponent<PixelTextFx>();
        tfx.top = new Color(1f, 0.95f, 0.5f); tfx.bottom = new Color(1f, 0.3f, 0.1f); tfx.depth = 4; tfx.step = 3f; tfx.shineEvery = 3.5f;
        Text sub = Label(board, "DEL INFIERNO", 24, TextAnchor.UpperCenter, new Vector2(0f, -90f), new Vector2(480f, 32f), new Color(1f, 0.6f, 0.35f));
        sub.gameObject.AddComponent<PixelTextFx>().depth = 2;

        List<RunRecords.Entry> table = RunRecords.Table();
        int count = Mathf.Min(RunRecords.HudSize, table.Count);
        int mine = table.FindIndex(e => !e.house);
        for (int i = 0; i < count; i++)
            AddRow(i, i + 1, table[i], i == mine);
        if (mine < 0 || mine >= count)
        {
            // Tu mejor partida, aunque no estés en el top.
            int best = RunRecords.PersonalBest;
            List<RunRecords.Entry> own = RunRecords.LoadPlayer();
            var you = new RunRecords.Entry { name = own.Count > 0 ? own[0].name : RunRecords.NeedsName ? "TÚ" : RunRecords.SavedName, score = best };
            RunRecords.LiveTable(best, 0f, out int rank);
            AddRow(count, best > 0 ? rank : 0, you, true);
        }
    }

    private Text Label(RectTransform parent, string text, int size, TextAnchor anchor, Vector2 pos, Vector2 sizeDelta, Color c)
    {
        var go = new GameObject(text, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = pos;
        r.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = c;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private void AddRow(int slot, int place, RunRecords.Entry e, bool isMine)
    {
        var row = new GameObject("Fila" + slot, typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(board, false);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(470f, 38f);
        row.anchoredPosition = new Vector2(0f, -148f - slot * 40f - (slot >= RunRecords.HudSize ? 12f : 0f));

        Color c = place == 1 ? new Color(1f, 0.82f, 0.25f) : isMine ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.9f, 0.82f);
        if (place <= 3 || isMine)
        {
            Image strip = Img("Franja", row, White(), place == 1 ? new Color(1f, 0.6f, 0.1f, 0.22f) : isMine ? new Color(0.2f, 1f, 0.3f, 0.16f) : new Color(1f, 0.4f, 0.2f, 0.1f));
            Stretch(strip);
            if (isMine) mineRow.Add(strip);
        }
        string name = e.name.Length > 15 ? e.name.Substring(0, 14) + "." : e.name;
        Text rank = Cell(row, place > 0 ? place.ToString() : "—", 0f, 0.12f, TextAnchor.MiddleRight, c, place == 1 ? 30 : 25);
        Text nm = Cell(row, name, 0.17f, 0.7f, TextAnchor.MiddleLeft, c, 25);
        Text sc = Cell(row, e.score.ToString("N0"), 0.66f, 0.97f, TextAnchor.MiddleRight, c, 25);
        foreach (Text t in new[] { rank, nm, sc })
        {
            var fx = t.gameObject.AddComponent<PixelTextFx>();
            fx.depth = 2;
            fx.top = Color.white;
            fx.bottom = place == 1 ? new Color(1f, 0.55f, 0.2f) : new Color(0.85f, 0.75f, 0.7f);
            if (place == 1) fx.shineEvery = 2.5f;
            if (isMine) mineRow.Add(t);
        }
        rows.Add(row);
        row.localScale = Vector3.zero;
    }

    private Text Cell(RectTransform row, string text, float x0, float x1, TextAnchor a, Color c, int size)
    {
        var go = new GameObject("Celda", typeof(RectTransform));
        go.transform.SetParent(row, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = new Vector2(x0, 0f); r.anchorMax = new Vector2(x1, 1f);
        r.offsetMin = r.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.alignment = a; t.color = c; t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    // ------------------------------------------------------------------ cada frame
    private static float EaseOutBack(float k) { k = Mathf.Clamp01(k); float c = 1.7f; return 1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f); }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;
        UpdateEmbers(dt, time);

        bool ready = intro == null || intro.IsFinished;
        if (!ready)
            return;
        if (!started)
        {
            started = true;
            if (logo != null) { logoBase = logo.anchoredPosition; logoScale = logo.localScale; }
        }
        since += dt;

        vignette.color = new Color(0.14f, 0f, 0f, 0.62f + Mathf.Sin(time * 1.3f) * 0.08f);


        UpdateTagline(time);

        // Logo vivo.
        if (logo != null)
        {
            logo.anchoredPosition = logoBase + new Vector2(0f, Mathf.Sin(time * 1.25f) * 9f);
            logo.localScale = logoScale * (1f + Mathf.Sin(time * 2.1f) * 0.015f);
            logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.8f) * 1.2f);
        }
        if (rays != null)
        {
            raysGroup.alpha = Mathf.MoveTowards(raysGroup.alpha, 0.16f + Mathf.Sin(time * 1.7f) * 0.05f, dt * 0.8f);
            rays.anchoredPosition = logo.anchoredPosition;
            rays.localRotation = Quaternion.Euler(0f, 0f, time * 9f);
            rays.localScale = Vector3.one * (1f + Mathf.Sin(time * 0.9f) * 0.05f);
        }

        // Botón seleccionado: se mece un poquito y suelta chispas.
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (sel != null && sel.GetComponent<MenuButtonFx>() != null)
        {
            var sr = (RectTransform)sel.transform;
            sr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 5f) * 1.4f);
            if (Random.value < dt * 14f) SparkAt(sr);
        }
        if (lastSelected != null && lastSelected != sel) lastSelected.transform.localRotation = Quaternion.identity;
        lastSelected = sel;

        // Tablero: entra deslizándose después del logo, se esconde con los paneles abiertos.
        bool panelOpen = PogoBridge.Get<MenuPanel>(menu, "openPanel") != null;
        float k = Mathf.Clamp01((since - 0.3f) / 0.7f);
        board.anchoredPosition = new Vector2(Mathf.Lerp(900f, -50f, EaseOutBack(k)), -150f + Mathf.Sin(time * 0.9f) * 5f);
        boardGroup.alpha = Mathf.MoveTowards(boardGroup.alpha, panelOpen ? 0f : Mathf.Clamp01(k * 2f), dt * 5f);
        for (int i = 0; i < rows.Count; i++)
        {
            float rk = Mathf.Clamp01((since - 0.8f - i * 0.08f) / 0.35f);
            rows[i].localScale = Vector3.one * EaseOutBack(rk);
        }
        float pulse = 0.65f + 0.35f * Mathf.Sin(time * 4f);
        foreach (Graphic g in mineRow)
            if (g is Image img) img.color = new Color(0.2f, 1f, 0.3f, 0.1f + 0.12f * pulse);
    }

    private GameObject lastSelected;

    private void SparkAt(RectTransform r)
    {
        // Una brasa que nace en la orilla del botón seleccionado.
        for (int i = 0; i < embers.Length; i++)
        {
            if (embers[i].life < embers[i].max * 0.8f) continue;
            Vector3[] corners = new Vector3[4];
            r.GetWorldCorners(corners);
            Vector3 w = Vector3.Lerp(corners[0], corners[3], Random.value);
            Vector2 local = root.InverseTransformPoint(w);
            embers[i].pos = local;
            embers[i].vel = new Vector2(Random.Range(-30f, 30f), Random.Range(60f, 140f));
            embers[i].life = 0f;
            embers[i].max = Random.Range(0.8f, 1.6f);
            break;
        }
    }

    private void UpdateEmbers(float dt, float time)
    {
        if (embers == null) return;
        Vector2 s = Size;
        for (int i = 0; i < embers.Length; i++)
        {
            ref Ember e = ref embers[i];
            e.life += dt;
            if (e.life >= e.max || e.pos.y > s.y * 0.5f + 20f)
            {
                Respawn(ref e, false);
                continue;
            }
            e.pos += (e.vel + new Vector2(Mathf.Sin(time * 1.7f + e.phase) * 25f, 0f)) * dt;
            // Coordenadas en pixeles "enteros" (de 2 en 2): se mueve pixelado.
            e.rect.anchoredPosition = new Vector2(Mathf.Round(e.pos.x / 2f) * 2f, Mathf.Round(e.pos.y / 2f) * 2f);
            float k = e.life / e.max;
            float a = Mathf.Clamp01(k * 6f) * (1f - k) * (0.6f + 0.4f * Mathf.Sin(time * 13f + e.phase));
            e.img.color = Color.Lerp(new Color(1f, 0.85f, 0.35f), new Color(0.9f, 0.15f, 0.05f), k) * new Color(1f, 1f, 1f, a);
        }
    }
}
