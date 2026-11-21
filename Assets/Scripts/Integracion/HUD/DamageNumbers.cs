using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum DamageKind
{
    Spit,
    Crit,
    Stomp,
    Player,
    Shield,
}

// Números de daño que flotan sobre los enemigos (y sobre Pogoman cuando lo golpean).
// Va en un objeto de la UI que ocupa toda la pantalla.
public class DamageNumbers : MonoBehaviour
{
    private static DamageNumbers instance;

    [SerializeField] private Font font;
    [SerializeField] private int poolSize = 40;
    [SerializeField] private float lifetime = 0.9f;
    [SerializeField] private float rise = 1.3f;

    private class Item
    {
        public RectTransform rect;
        public Text text;
        public Vector3 world;
        public float age;
        public DamageKind kind;
        public bool active;
        public Color color;
    }

    private readonly List<Item> items = new List<Item>();
    private RectTransform root;

    private void Awake()
    {
        instance = this;
        root = (RectTransform)transform;

        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("Numero", typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(300, 80);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.01f, 0.01f, 1f);
            shadow.effectDistance = new Vector2(3, -3);

            go.SetActive(false);
            items.Add(new Item { rect = rect, text = text });
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void Show(Vector3 worldPosition, float amount, DamageKind kind)
    {
        if (instance != null)
            instance.Spawn(worldPosition, amount, kind);
    }

    private void Spawn(Vector3 world, float amount, DamageKind kind)
    {
        Item item = items.Find(i => !i.active);
        if (item == null)
        {
            item = items[0];
            foreach (Item i in items)
                if (i.age > item.age)
                    item = i;
        }

        int value = Mathf.Max(1, Mathf.RoundToInt(amount));
        Vector2 jitter = Random.insideUnitCircle * 0.35f;

        item.world = world + new Vector3(jitter.x, 0f, jitter.y);
        item.age = 0f;
        item.kind = kind;
        item.active = true;

        switch (kind)
        {
            case DamageKind.Crit:
                item.text.text = value + "!";
                item.text.fontSize = 56;
                item.color = new Color(1f, 0.85f, 0.15f);
                break;
            case DamageKind.Stomp:
                item.text.text = value.ToString();
                item.text.fontSize = 44;
                item.color = new Color(1f, 0.55f, 0.15f);
                break;
            case DamageKind.Shield:
                item.text.text = "-" + value;
                item.text.fontSize = 34;
                item.color = new Color(0.45f, 0.8f, 1f);
                break;
            case DamageKind.Player:
                item.text.text = "-" + value;
                item.text.fontSize = 40;
                item.color = new Color(1f, 0.25f, 0.2f);
                break;
            default:
                item.text.text = value.ToString();
                item.text.fontSize = 36;
                item.color = new Color(1f, 0.96f, 0.86f);
                break;
        }

        item.text.color = item.color;
        item.rect.gameObject.SetActive(true);
        item.rect.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        float dt = Time.unscaledDeltaTime;

        foreach (Item item in items)
        {
            if (!item.active)
                continue;

            item.age += dt;
            float t = item.age / lifetime;

            if (t >= 1f)
            {
                item.active = false;
                item.rect.gameObject.SetActive(false);
                continue;
            }

            float up = 1f - (1f - t) * (1f - t);
            Vector3 screen = WorldToScreen(cam, item.world + Vector3.up * rise * up);

            if (screen.z < 0f)
            {
                item.rect.gameObject.SetActive(false);
                continue;
            }

            item.rect.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
            item.rect.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));

            float pop = item.kind == DamageKind.Crit ? 0.9f : 0.35f;
            float scale = 1f + pop * Mathf.Max(0f, 1f - t * 6f);
            item.rect.localScale = Vector3.one * scale;

            Color c = item.color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            item.text.color = c;
        }
    }

    // Usa coordenadas de viewport para que funcione aunque la cámara dibuje a una
    // textura de menor resolución (filtro retro).
    private static Vector3 WorldToScreen(Camera cam, Vector3 world)
    {
        Vector3 vp = cam.WorldToViewportPoint(world);
        return new Vector3(vp.x * Screen.width, vp.y * Screen.height, vp.z);
    }
}
