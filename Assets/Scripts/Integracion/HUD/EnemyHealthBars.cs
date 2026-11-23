using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Barritas de vida sobre los enemigos (solo cuando acaban de recibir daño)
// y una barra grande arriba de la pantalla para el jefe.
public class EnemyHealthBars : MonoBehaviour
{
    [Header("Barras pequeñas")]
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Sprite fillSprite;
    [SerializeField] private Vector2 barSize = new Vector2(96, 14);
    [SerializeField] private float visibleAfterHit = 3f;
    [SerializeField] private int poolSize = 24;

    [Header("Barra de jefe")]
    [SerializeField] private CanvasGroup bossGroup;
    [SerializeField] private RectTransform bossFill;
    [SerializeField] private Text bossName;
    [SerializeField] private float bossShowDistance = 35f;

    private class Bar
    {
        public RectTransform rect;
        public RectTransform fill;
        public Image fillImage;
    }

    private static readonly Color Normal = new Color(0.9f, 0.15f, 0.12f);
    private static readonly Color EliteColor = new Color(0.8f, 0.3f, 1f);
    private static readonly Color Low = new Color(1f, 0.55f, 0.1f);

    private readonly List<Bar> bars = new List<Bar>();
    private RectTransform root;
    private Transform player;

    private void Awake()
    {
        root = (RectTransform)transform;

        for (int i = 0; i < poolSize; i++)
        {
            var frame = new GameObject("BarraEnemigo", typeof(RectTransform), typeof(Image));
            frame.layer = gameObject.layer;
            frame.transform.SetParent(transform, false);
            var frameImage = frame.GetComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.pixelsPerUnitMultiplier = 0.5f;
            frameImage.raycastTarget = false;
            var rect = (RectTransform)frame.transform;
            rect.sizeDelta = barSize;

            var fill = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
            fill.layer = gameObject.layer;
            fill.transform.SetParent(frame.transform, false);
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = fillSprite;
            fillImage.raycastTarget = false;
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(1, 1);
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            fillRect.pivot = new Vector2(0, 0.5f);

            // Brillo arriba y sombra abajo: más volumen sin hacerla más grande.
            RectTransform hi = FancyBar.Img("Brillo", fillRect, new Color(1f, 1f, 1f, 0.35f)).rectTransform;
            hi.anchorMin = new Vector2(0f, 0.55f); hi.anchorMax = new Vector2(1f, 0.85f); hi.offsetMin = hi.offsetMax = Vector2.zero;
            RectTransform sh = FancyBar.Img("Sombra", fillRect, new Color(0f, 0f, 0f, 0.35f)).rectTransform;
            sh.anchorMin = Vector2.zero; sh.anchorMax = new Vector2(1f, 0.3f); sh.offsetMin = sh.offsetMax = Vector2.zero;
            frameImage.color = new Color(1f, 0.85f, 0.6f);

            frame.SetActive(false);
            bars.Add(new Bar { rect = rect, fill = fillRect, fillImage = fillImage });
        }

        if (bossGroup != null)
            bossGroup.alpha = 0f;
        // Barra de los guardianes: brillo, sombra y nombre con fuego.
        if (bossFill != null)
        {
            RectTransform hi = FancyBar.Img("Brillo", bossFill, new Color(1f, 1f, 1f, 0.3f)).rectTransform;
            hi.anchorMin = new Vector2(0f, 0.6f); hi.anchorMax = new Vector2(1f, 0.85f); hi.offsetMin = hi.offsetMax = Vector2.zero;
            RectTransform sh = FancyBar.Img("Sombra", bossFill, new Color(0f, 0f, 0f, 0.3f)).rectTransform;
            sh.anchorMin = Vector2.zero; sh.anchorMax = new Vector2(1f, 0.28f); sh.offsetMin = sh.offsetMax = Vector2.zero;
        }
        if (bossName != null && bossName.GetComponent<PixelTextFx>() == null)
        {
            var fx = bossName.gameObject.AddComponent<PixelTextFx>();
            fx.top = new Color(1f, 0.9f, 1f); fx.bottom = new Color(0.8f, 0.4f, 1f);
            fx.extrudeFront = new Color(0.2f, 0.03f, 0.3f); fx.extrudeBack = new Color(0.05f, 0f, 0.1f); fx.depth = 3; fx.shineEvery = 4f;
        }
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        if (player == null)
        {
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p != null)
                player = p.transform;
        }

        int used = 0;
        EnemyTracker boss = null;
        float bossDistance = float.MaxValue;

        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || e.CustomUi)
                continue;

            // En la arena la barra de arriba es la del Diablo: los mini jefes llevan barra normal.
            if (e.IsBoss && !ArenaFight.InArena)
            {
                float d = player != null ? Vector3.Distance(player.position, e.Center) : 0f;
                if (d < bossDistance)
                {
                    bossDistance = d;
                    boss = e;
                }
                continue;
            }

            if (Time.time - e.LastDamageTime > visibleAfterHit || used >= bars.Count)
                continue;

            Vector3 screen = WorldToScreen(cam, e.Top + Vector3.up * 0.35f);
            if (screen.z < 0f)
                continue;

            Bar bar = bars[used++];
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
            bar.rect.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));
            float pct = e.Health / e.MaxHealth;
            SetFill(bar.fill, pct);
            // Élites en morado; con poca vida late en naranja.
            bar.fillImage.color = e.IsElite ? EliteColor : pct < 0.3f && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? Low : Normal;
            bar.rect.localScale = e.IsElite ? new Vector3(1.3f, 1.2f, 1f) : Vector3.one;
            bar.rect.gameObject.SetActive(true);
        }

        for (int i = used; i < bars.Count; i++)
            if (bars[i].rect.gameObject.activeSelf)
                bars[i].rect.gameObject.SetActive(false);

        UpdateBossBar(boss, bossDistance);
    }

    private void UpdateBossBar(EnemyTracker boss, float distance)
    {
        if (bossGroup == null)
            return;

        bool show = boss != null && distance < bossShowDistance;
        bossGroup.alpha = Mathf.MoveTowards(bossGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);

        if (boss != null)
        {
            SetFill(bossFill, boss.Health / boss.MaxHealth);
            if (bossName != null)
                bossName.text = boss.DisplayName;
        }
    }

    private static void SetFill(RectTransform fill, float percent)
    {
        if (fill == null)
            return;
        Vector2 max = fill.anchorMax;
        max.x = Mathf.Clamp01(percent);
        fill.anchorMax = max;
    }

    // Usa coordenadas de viewport para que funcione aunque la cámara dibuje a una
    // textura de menor resolución (filtro retro).
    private static Vector3 WorldToScreen(Camera cam, Vector3 world)
    {
        Vector3 vp = cam.WorldToViewportPoint(world);
        return new Vector3(vp.x * Screen.width, vp.y * Screen.height, vp.z);
    }
}
