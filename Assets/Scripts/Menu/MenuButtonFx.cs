using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Reacción visual de un botón del menú: crece, se ilumina, muestra la flecha
// y suena. Funciona igual con mouse, teclado y control porque todo depende de
// qué botón está "seleccionado" (pasar el mouse encima también lo selecciona).
[RequireComponent(typeof(Selectable))]
public class MenuButtonFx : MonoBehaviour,
    IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    // Mientras sea true, seleccionar un botón no hace sonido (selección automática).
    public static bool MuteSelectionSound;

    [Header("Referencias")]
    [SerializeField] private Image background;
    [SerializeField] private Text label;
    [SerializeField] private RectTransform indicator;

    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightSprite;
    [SerializeField] private Sprite disabledSprite;

    [Header("Colores del texto")]
    [SerializeField] private Color labelNormal = new Color(0.86f, 0.76f, 0.66f);
    [SerializeField] private Color labelHighlight = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private Color labelDisabled = new Color(0.4f, 0.3f, 0.28f);

    [Header("Animación")]
    [SerializeField] private float highlightScale = 1.06f;
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField] private float scaleSpeed = 18f;
    [SerializeField] private float indicatorBob = 6f;

    private Selectable selectable;
    private bool selected;
    private bool pressed;
    private float pressPulse;
    private Vector2 indicatorBase;
    private bool lastInteractable = true;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();

        if (indicator != null)
            indicatorBase = indicator.anchoredPosition;

        if (selectable is Button button)
            button.onClick.AddListener(() =>
            {
                pressPulse = 1f;
                MenuAudio.Instance?.PlayClick();
            });
    }

    private void OnEnable()
    {
        selected = EventSystem.current != null &&
                   EventSystem.current.currentSelectedGameObject == gameObject;
        pressed = false;
        RefreshVisuals();
        transform.localScale = Vector3.one * TargetScale();
    }

    private void Update()
    {
        pressPulse = Mathf.MoveTowards(pressPulse, 0f, Time.unscaledDeltaTime * 6f);

        float k = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * TargetScale(), k);

        if (indicator != null && indicator.gameObject.activeSelf)
        {
            float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 8f) * indicatorBob * 0.5f);
            indicator.anchoredPosition = indicatorBase + new Vector2(bob, 0f);
        }

        // Si el botón se activa o desactiva desde fuera, refrescamos.
        bool interactable = selectable.IsInteractable();
        if (interactable != lastInteractable)
        {
            lastInteractable = interactable;
            RefreshVisuals();
        }
    }

    private float TargetScale()
    {
        if (!selectable.IsInteractable())
            return 1f;

        float s = selected ? highlightScale : 1f;

        if (pressed)
            s = pressScale;

        return s - pressPulse * (highlightScale - pressScale) * 0.6f;
    }

    private void RefreshVisuals()
    {
        bool interactable = selectable == null || selectable.IsInteractable();
        lastInteractable = interactable;   // así Update detecta cuando vuelve a estar activo
        bool lit = selected && interactable;

        if (background != null)
            background.sprite = !interactable && disabledSprite != null ? disabledSprite
                              : lit && highlightSprite != null ? highlightSprite
                              : normalSprite;

        if (label != null)
            label.color = !interactable ? labelDisabled : lit ? labelHighlight : labelNormal;

        if (indicator != null)
            indicator.gameObject.SetActive(lit);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        RefreshVisuals();

        if (!MuteSelectionSound)
            MenuAudio.Instance?.PlayHover();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        pressed = false;
        RefreshVisuals();
    }

    public void OnPointerDown(PointerEventData eventData) => pressed = selectable.IsInteractable();
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
    public void OnSubmit(BaseEventData eventData) => pressPulse = 1f;
}
