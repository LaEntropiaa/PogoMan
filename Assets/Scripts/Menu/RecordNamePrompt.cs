using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Ventanita para escribir el nombre de quien hizo el récord (al entrar al top).
public class RecordNamePrompt : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    private InputField field;
    private System.Action done;

    public static void Show(Font font, System.Action onDone = null)
    {
        if (IsOpen) return;
        var go = new GameObject("NombreDelRecord");
        var p = go.AddComponent<RecordNamePrompt>();
        p.done = onDone;
        p.Build(font);
        IsOpen = true;
    }

    private void OnDestroy() => IsOpen = false;

    private void Build(Font font)
    {
        Canvas canvas = UiKit.MakeCanvas("Nombre", 40);
        canvas.transform.SetParent(transform, false);
        Transform r = canvas.transform;
        UiKit.Image(UiKit.Stretch("Fondo", r), new Color(0f, 0f, 0f, 0.65f)).raycastTarget = true;
        RectTransform panel = UiKit.Rect("Panel", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 360f));
        UiKit.Image(panel, new Color(0.12f, 0.04f, 0.06f, 0.98f));
        panel.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.75f, 0.3f);
        UiKit.Text(UiKit.Rect("Titulo", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 60f)),
            font, 44, new Color(1f, 0.8f, 0.35f), TextAnchor.MiddleCenter, "¡ENTRASTE AL TOP!  LUGAR #" + RunRecords.LastRank);
        UiKit.Text(UiKit.Rect("Sub", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(860f, 40f)),
            font, 26, new Color(1f, 0.9f, 0.8f), TextAnchor.MiddleCenter, "¿QUIÉN HIZO ESTE RÉCORD?");

        RectTransform box = UiKit.Rect("Caja", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(600f, 70f));
        Image bg = UiKit.Image(box, new Color(0.3f, 0.12f, 0.12f));
        bg.raycastTarget = true;
        Text text = UiKit.Text(UiKit.Stretch("Texto", box, 12f), font, 36, Color.white, TextAnchor.MiddleCenter);
        text.supportRichText = false;
        Text placeholder = UiKit.Text(UiKit.Stretch("Placeholder", box, 12f), font, 32, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter, "ESCRIBE UN NOMBRE");
        field = box.gameObject.AddComponent<InputField>();
        field.textComponent = text;
        field.placeholder = placeholder;
        field.targetGraphic = bg;
        field.characterLimit = 18;
        field.onEndEdit.AddListener(_ => { if (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed)) Submit(); });

        RectTransform btn = UiKit.Rect("Guardar", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(340f, 62f));
        Image bimg = UiKit.Image(btn, new Color(0.45f, 0.12f, 0.1f));
        bimg.raycastTarget = true;
        var b = btn.gameObject.AddComponent<Button>();
        b.targetGraphic = bimg;
        b.onClick.AddListener(Submit);
        UiKit.Text(UiKit.Stretch("T", btn), font, 30, Color.white, TextAnchor.MiddleCenter, "GUARDAR (ENTER)");

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(box.gameObject);
        field.ActivateInputField();
    }

    private void Submit()
    {
        RunRecords.RenameLast(field.text);
        done?.Invoke();
        Destroy(gameObject);
    }
}
