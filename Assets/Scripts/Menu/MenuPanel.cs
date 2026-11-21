using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ventana del menú (opciones, créditos) que aparece y desaparece con una
// animación corta. Al abrirse selecciona su primer control.
[RequireComponent(typeof(CanvasGroup))]
public class MenuPanel : MonoBehaviour
{
    [SerializeField] private Selectable firstSelected;
    [SerializeField] private float animDuration = 0.18f;
    [SerializeField] private float hiddenScale = 0.92f;

    private CanvasGroup group;
    private float progress;
    private bool open;

    public bool IsOpen => open;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        Apply();
    }

    public void Show()
    {
        open = true;
        gameObject.SetActive(true);
        Apply();

        if (firstSelected != null && EventSystem.current != null)
        {
            MenuButtonFx.MuteSelectionSound = true;
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
            MenuButtonFx.MuteSelectionSound = false;
        }
    }

    public void Hide()
    {
        open = false;
    }

    private void Update()
    {
        float target = open ? 1f : 0f;
        progress = Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / animDuration);
        Apply();

        if (!open && progress <= 0f)
            gameObject.SetActive(false);
    }

    private void Apply()
    {
        if (group == null)
            return;

        float e = 1f - Mathf.Pow(1f - progress, 3f);
        group.alpha = e;
        group.interactable = open;
        group.blocksRaycasts = open;
        transform.localScale = Vector3.one * Mathf.Lerp(hiddenScale, 1f, e);
    }
}
