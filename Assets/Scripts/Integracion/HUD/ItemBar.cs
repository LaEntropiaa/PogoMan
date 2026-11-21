using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Barra de objetos en la parte de abajo del HUD: imagen de cada objeto y "x2", "x3"...
public class ItemBar : MonoBehaviour
{
    [SerializeField] private Font font;
    [SerializeField] private float slotSize = 52f;
    [SerializeField] private float spacing = 8f;

    private class Slot { public RectTransform rect; public Text count; public float pop; }
    private readonly Dictionary<ItemDefinition, Slot> slots = new Dictionary<ItemDefinition, Slot>();

    private void Start()
    {
        if (ItemInventory.Instance != null)
            ItemInventory.Instance.ItemAdded += OnItemAdded;
    }

    private void OnDestroy()
    {
        if (ItemInventory.Instance != null)
            ItemInventory.Instance.ItemAdded -= OnItemAdded;
    }

    private void OnItemAdded(ItemDefinition item, int count)
    {
        if (!slots.TryGetValue(item, out Slot slot))
        {
            slot = CreateSlot(item);
            slots[item] = slot;
            Layout();
        }

        slot.count.text = count > 1 ? "x" + count : "";
        slot.pop = 1f;
    }

    private Slot CreateSlot(ItemDefinition item)
    {
        var go = new GameObject(item.id, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(slotSize, slotSize);
        var img = go.GetComponent<Image>();
        img.sprite = item.icon;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var t = new GameObject("Cantidad", typeof(RectTransform), typeof(Text), typeof(Shadow));
        t.layer = gameObject.layer;
        t.transform.SetParent(go.transform, false);
        var tr = (RectTransform)t.transform;
        tr.anchorMin = tr.anchorMax = new Vector2(1f, 0f);
        tr.pivot = new Vector2(1f, 0f);
        tr.anchoredPosition = new Vector2(4f, -2f);
        tr.sizeDelta = new Vector2(60f, 24f);
        var text = t.GetComponent<Text>();
        text.font = font;
        text.fontSize = 18;
        text.alignment = TextAnchor.LowerRight;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.GetComponent<Shadow>().effectDistance = new Vector2(2, -2);

        return new Slot { rect = rect, count = text };
    }

    // Centra los íconos en la parte de abajo.
    private void Layout()
    {
        float width = slots.Count * slotSize + (slots.Count - 1) * spacing;
        float x = -width / 2f + slotSize / 2f;
        foreach (Slot s in slots.Values)
        {
            s.rect.anchoredPosition = new Vector2(x, 0f);
            x += slotSize + spacing;
        }
    }

    private void Update()
    {
        foreach (Slot s in slots.Values)
        {
            s.pop = Mathf.MoveTowards(s.pop, 0f, Time.unscaledDeltaTime * 3f);
            s.rect.localScale = Vector3.one * (1f + s.pop * 0.35f);
        }
    }
}
