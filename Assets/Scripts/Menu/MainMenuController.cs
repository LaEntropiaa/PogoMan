using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Controlador del menú principal: botones, submenús y navegación.
public class MainMenuController : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string gameSceneName = "Nivel1";

    [Header("Botones principales (en orden)")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Acomodo de botones")]
    [Tooltip("Contenedores de cada botón, en el mismo orden que arriba.")]
    [SerializeField] private RectTransform[] buttonSlots;
    [SerializeField] private float firstSlotY = 40f;
    [SerializeField] private float slotSpacing = 92f;

    [Header("Paneles")]
    [SerializeField] private CanvasGroup mainButtonsGroup;
    [SerializeField] private MenuPanel optionsPanel;
    [SerializeField] private MenuPanel creditsPanel;
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button creditsBackButton;
    [SerializeField] private Button recordsButton;
    [SerializeField] private MenuPanel recordsPanel;
    [SerializeField] private Button recordsBackButton;

    [Header("Otros")]
    [SerializeField] private MenuIntroSequence intro;
    [SerializeField] private SceneTransition transition;

    private MenuPanel openPanel;
    private GameObject lastMainSelection;
    private bool leaving;

    private void Awake()
    {
        startButton.onClick.AddListener(StartGame);
        continueButton.onClick.AddListener(ContinueGame);
        optionsButton.onClick.AddListener(() => OpenPanel(optionsPanel));
        creditsButton.onClick.AddListener(() => OpenPanel(creditsPanel));
        if (recordsButton != null && recordsPanel != null)
            recordsButton.onClick.AddListener(() => OpenPanel(recordsPanel));
        if (recordsBackButton != null)
            recordsBackButton.onClick.AddListener(ClosePanel);
        quitButton.onClick.AddListener(QuitGame);

        if (optionsBackButton != null)
            optionsBackButton.onClick.AddListener(ClosePanel);
        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(ClosePanel);

        // "Continuar" solo aparece si hay partida guardada.
        bool hasSave = SaveGameProbe.HasSave;
        continueButton.transform.parent.gameObject.SetActive(hasSave);

        // El botón de RÉCORDS ya no hace falta: el top 10 se ve a la derecha del menú.
        if (recordsButton != null)
            recordsButton.transform.parent.gameObject.SetActive(false);

        var visible = LayoutSlots();
        ChainNavigation(visible);

        SetMainInteractable(false);

        if (intro != null)
            intro.SetButtons(visible);

        // Brasas, relámpagos, letras con estilo y el TOP 10 a la derecha.
        gameObject.AddComponent<MenuEpicFx>();
    }

    private void Start()
    {
        // El juego bloquea el mouse; en el menú lo necesitamos libre.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        if (intro == null || intro.IsFinished)
            OnIntroFinished();
        else
            intro.Finished += OnIntroFinished;
    }

    // Acomoda los botones visibles de arriba hacia abajo y devuelve los botones en orden.
    // Arriba/abajo entre los botones visibles (sin pasar por los que se escondieron).
    private static void ChainNavigation(List<RectTransform> visible)
    {
        var buttons = new List<Selectable>();
        foreach (RectTransform r in visible)
        {
            Selectable s = r.GetComponent<Selectable>();
            if (s != null) buttons.Add(s);
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            Navigation nav = buttons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = buttons[(i - 1 + buttons.Count) % buttons.Count];
            nav.selectOnDown = buttons[(i + 1) % buttons.Count];
            buttons[i].navigation = nav;
        }
    }

    private List<RectTransform> LayoutSlots()
    {
        var visible = new List<RectTransform>();
        int index = 0;

        foreach (RectTransform slot in buttonSlots)
        {
            if (slot == null || !slot.gameObject.activeSelf)
                continue;

            slot.anchoredPosition = new Vector2(slot.anchoredPosition.x, firstSlotY - index * slotSpacing);
            visible.Add((RectTransform)slot.GetChild(0));
            index++;
        }

        return visible;
    }

    // Al ganar la partida el juego regresa al menú directo a los créditos.
    public static bool OpenCreditsOnLoad;

    private void OnIntroFinished()
    {
        if (openPanel != null)
            return;

        if (OpenCreditsOnLoad)
        {
            OpenCreditsOnLoad = false;
            OpenPanel(creditsPanel);
            return;
        }

        SetMainInteractable(true);
        SelectSilently(startButton.gameObject);
    }

    private void SetMainInteractable(bool value)
    {
        if (mainButtonsGroup == null)
            return;

        mainButtonsGroup.interactable = value;
        mainButtonsGroup.blocksRaycasts = value;
    }

    private void Update()
    {
        if (leaving)
            return;

        if (BackPressed() && openPanel != null)
            ClosePanel();

        // Si se perdió la selección (clic en el vacío), la recuperamos al usar teclado o control.
        EventSystem es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject == null && NavigationPressed())
        {
            GameObject fallback = openPanel != null ? null : (lastMainSelection != null ? lastMainSelection : startButton.gameObject);
            if (fallback != null && mainButtonsGroup.interactable)
                SelectSilently(fallback);
        }
    }

    private void OpenPanel(MenuPanel panel)
    {
        if (panel == null || openPanel != null)
            return;

        lastMainSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        SetMainInteractable(false);
        openPanel = panel;
        panel.Show();
    }

    private void ClosePanel()
    {
        if (openPanel == null)
            return;

        openPanel.Hide();
        openPanel = null;
        MenuAudio.Instance?.PlayClick();

        SetMainInteractable(true);
        SelectSilently(lastMainSelection != null ? lastMainSelection : startButton.gameObject);
    }

    private void StartGame()
    {
        leaving = true;
        SetMainInteractable(false);

        if (transition != null)
            transition.LoadScene(gameSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }

    private void ContinueGame()
    {
        if (SaveGameProbe.ContinueAction != null)
        {
            leaving = true;
            SetMainInteractable(false);
            SaveGameProbe.ContinueAction();
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        // En el editor Application.Quit() no hace nada; detenemos el modo Play.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void SelectSilently(GameObject go)
    {
        if (EventSystem.current == null || go == null)
            return;

        MenuButtonFx.MuteSelectionSound = true;
        EventSystem.current.SetSelectedGameObject(go);
        MenuButtonFx.MuteSelectionSound = false;
    }

    private static bool BackPressed()
    {
        bool key = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool pad = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        return key || pad;
    }

    private static bool NavigationPressed()
    {
        Keyboard k = Keyboard.current;
        bool key = k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                                 k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame ||
                                 k.enterKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame);
        Gamepad g = Gamepad.current;
        bool pad = g != null && (g.dpad.up.wasPressedThisFrame || g.dpad.down.wasPressedThisFrame ||
                                 g.leftStick.ReadValue().sqrMagnitude > 0.25f || g.buttonSouth.wasPressedThisFrame);
        return key || pad;
    }
}
