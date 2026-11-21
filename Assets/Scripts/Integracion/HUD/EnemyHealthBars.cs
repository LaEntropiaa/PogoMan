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
    }

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

            frame.SetActive(false);
            bars.Add(new Bar { rect = rect, fill = fillRect });
        }

        if (bossGroup != null)
            bossGroup.alpha = 0f;
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
            if (e == null || e.IsDead)
                continue;

            if (e.IsBoss)
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
            SetFill(bar.fill, e.Health / e.MaxHealth);
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
