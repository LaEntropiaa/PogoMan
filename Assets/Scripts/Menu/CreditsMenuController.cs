using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Créditos editables desde el Inspector. El texto sube solo (como créditos de
// película) y también se puede mover con la rueda del mouse, flechas o el stick.
public class CreditsMenuController : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string role = "ROL";
        [TextArea(1, 4)]
        [Tooltip("Un nombre por línea.")]
        public string name = "NOMBRE";
    }

    [Header("Contenido")]
    [TextArea(1, 4)]
    [SerializeField] private string header = "";
    [SerializeField] private List<Entry> team = new List<Entry>();
    [TextArea(3, 12)]
    [SerializeField] private string notes = "";

    [Header("Estilo")]
    [SerializeField] private Color roleColor = new Color(0.6f, 0.07f, 0.03f);
    [SerializeField] private Color highlightColor = new Color(0.45f, 0.05f, 0.02f);

    [Header("Desplazamiento")]
    [SerializeField] private Text body;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private float autoScrollSpeed = 45f;
    [SerializeField] private float startDelay = 1.2f;
    [SerializeField] private float endPause = 2.5f;
    [SerializeField] private float manualSpeed = 600f;

    private RectTransform content;
    private float offset;
    private float waitTimer;

    private void Awake() => Refresh();

    // Se actualiza en el editor mientras editas el Inspector.
    private void OnValidate() => Refresh();

    private void OnEnable()
    {
        offset = 0f;
        waitTimer = startDelay;
        ApplyOffset();
    }

    public void Refresh()
    {
        if (body == null)
            return;

        string role = "#" + ColorUtility.ToHtmlStringRGB(roleColor);
        string strong = "#" + ColorUtility.ToHtmlStringRGB(highlightColor);
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(header))
        {
            sb.AppendLine($"<color={strong}>{header.Trim()}</color>");
            sb.AppendLine();
        }

        foreach (Entry e in team)
        {
            sb.AppendLine($"<color={role}>{e.role.ToUpperInvariant()}</color>");
            sb.AppendLine(e.name.Trim());
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(notes))
            sb.AppendLine(notes.Trim());

        body.text = sb.ToString().TrimEnd();

        content = body.rectTransform;
        content.sizeDelta = new Vector2(content.sizeDelta.x, body.preferredHeight + 20f);
    }

    private float MaxOffset
    {
        get
        {
            if (content == null || viewport == null)
                return 0f;
            return Mathf.Max(0f, content.sizeDelta.y - viewport.rect.height);
        }
    }

    private void Update()
    {
        if (content == null || viewport == null)
            return;

        float manual = ManualInput();

        if (Mathf.Abs(manual) > 0.01f)
        {
            offset += manual * manualSpeed * Time.unscaledDeltaTime;
            waitTimer = endPause;  // el jugador tomó el control; pausa el automático
        }
        else if (waitTimer > 0f)
        {
            waitTimer -= Time.unscaledDeltaTime;
        }
        else if (offset < MaxOffset)
        {
            offset += autoScrollSpeed * Time.unscaledDeltaTime;
            if (offset >= MaxOffset)
                waitTimer = endPause;
        }
        else
        {
            // Llegó al final: espera y vuelve a empezar.
            offset = 0f;
            waitTimer = startDelay;
        }

        offset = Mathf.Clamp(offset, 0f, MaxOffset);
        ApplyOffset();
    }

    private void ApplyOffset()
    {
        if (content != null)
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Round(offset));
    }

    private static float ManualInput()
    {
        float v = 0f;

        if (Mouse.current != null)
            v -= Mouse.current.scroll.ReadValue().y * 0.05f;

        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.downArrowKey.isPressed || k.sKey.isPressed) v += 1f;
            if (k.upArrowKey.isPressed || k.wKey.isPressed) v -= 1f;
        }

        Gamepad g = Gamepad.current;
        if (g != null)
            v -= g.rightStick.ReadValue().y + g.dpad.ReadValue().y;

        return Mathf.Clamp(v, -3f, 3f);
    }
}
