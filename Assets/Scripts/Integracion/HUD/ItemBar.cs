using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Barra de objetos en la parte de abajo del HUD: imagen de cada objeto y "x2", "x3"...
public class ItemBar : MonoBehaviour
{
    [SerializeField] private Font font;
    [SerializeField] private float slotSize = 52f;
    [SerializeField] private float spacing = 8f;

    private class Slot { public RectTransform rect; public Text count; public float pop; public Image cooldown; public Image icon; }
    private readonly Dictionary<ItemDefinition, Slot> slots = new Dictionary<ItemDefinition, Slot>();

    private void Start()
    {
        if (ItemInventory.Instance != null)
        {
            ItemInventory.Instance.ItemAdded += OnItemAdded;
            ItemInventory.Instance.ItemRemoved += OnItemRemoved;
        }
    }

    private void OnDestroy()
    {
        if (ItemInventory.Instance != null)
        {
            ItemInventory.Instance.ItemAdded -= OnItemAdded;
            ItemInventory.Instance.ItemRemoved -= OnItemRemoved;
        }
    }

    // Se gastó uno (Estampita): baja la cantidad o quita el ícono.
    private void OnItemRemoved(ItemDefinition item, int count)
    {
        if (!slots.TryGetValue(item, out Slot slot))
            return;

        if (count <= 0)
        {
            Destroy(slot.rect.gameObject);
            slots.Remove(item);
            Layout();
            return;
        }

        slot.count.text = count > 1 ? "x" + count : "";
        slot.pop = 1f;
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

        // Sombra que baja mientras el objeto se recarga (Escapulario).
        var cd = new GameObject("Recarga", typeof(RectTransform), typeof(Image));
        cd.layer = gameObject.layer;
        cd.transform.SetParent(go.transform, false);
        cd.transform.SetSiblingIndex(0);
        var cdRect = (RectTransform)cd.transform;
        cdRect.anchorMin = Vector2.zero;
        cdRect.anchorMax = Vector2.one;
        cdRect.offsetMin = cdRect.offsetMax = Vector2.zero;
        var cdImage = cd.GetComponent<Image>();
        cdImage.sprite = item.icon;
        cdImage.preserveAspect = true;
        cdImage.type = Image.Type.Filled;
        cdImage.fillMethod = Image.FillMethod.Vertical;
        cdImage.fillOrigin = (int)Image.OriginVertical.Top;
        cdImage.color = new Color(0f, 0f, 0f, 0.7f);
        cdImage.fillAmount = 0f;
        cdImage.raycastTarget = false;
        cd.transform.SetAsLastSibling();
        t.transform.SetAsLastSibling();

        return new Slot { rect = rect, count = text, cooldown = cdImage, icon = img };
    }

    // Centra los íconos abajo; si son muchos se achican y se reparten en dos filas.
    private void Layout()
    {
        const float maxWidth = 1100f;
        int n = slots.Count;
        float size = slotSize;
        int perRow = n;
        while (perRow * (size + spacing) > maxWidth && size > 34f) size -= 2f;
        if (perRow * (size + spacing) > maxWidth) perRow = Mathf.CeilToInt(n / 2f);
        int i = 0;
        foreach (Slot s in slots.Values)
        {
            int row = i / perRow, col = i % perRow;
            int inRow = Mathf.Min(perRow, n - row * perRow);
            float width = inRow * size + (inRow - 1) * spacing;
            s.rect.sizeDelta = new Vector2(size, size);
            s.rect.anchoredPosition = new Vector2(-width / 2f + size / 2f + col * (size + spacing), row * (size + spacing));
            s.count.fontSize = size < 44f ? 14 : 18;
            i++;
        }
    }

    private void Update()
    {
        ItemEffects fx = ItemEffects.Instance;
        foreach (KeyValuePair<ItemDefinition, Slot> pair in slots)
        {
            Slot s = pair.Value;
            s.pop = Mathf.MoveTowards(s.pop, 0f, Time.unscaledDeltaTime * 3f);
            s.rect.localScale = Vector3.one * (1f + s.pop * 0.35f);

            // Escapulario: si no le quedan cargas se oscurece y se va aclarando con la recarga.
            float dim = 0f;
            if (fx != null && pair.Key.id == ItemEffects.Escapulario && fx.BlockCharges <= 0 && fx.BlockRechargeTotal > 0f)
                dim = Mathf.Clamp01(fx.BlockRechargeLeft / fx.BlockRechargeTotal);
            s.cooldown.fillAmount = dim;
        }
    }
}
