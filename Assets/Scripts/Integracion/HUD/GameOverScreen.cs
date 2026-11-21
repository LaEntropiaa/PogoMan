using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla de "HAS MUERTO" con el resumen de la partida.
public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Text summary;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private float fadeDuration = 0.8f;

    public bool IsShown { get; private set; }

    private void Awake()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (retryButton != null) retryButton.onClick.AddListener(SceneNav.RestartLevel);
        if (menuButton != null) menuButton.onClick.AddListener(SceneNav.ToMainMenu);
    }

    public void Show()
    {
        if (IsShown)
            return;

        IsShown = true;
        SceneNav.SetGameplayCursor(false);

        PlayerStats s = PlayerStats.Instance;
        if (summary != null && s != null)
        {
            int t = Mathf.FloorToInt(s.RunTime);
            summary.text =
                "SOBREVIVISTE  " + (t / 60).ToString("00") + ":" + (t % 60).ToString("00") + "\n" +
                "ENEMIGOS DERROTADOS  " + s.Kills + "\n" +
                "MONEDAS  " + s.CoinsCollected + "\n" +
                "DAÑO TOTAL  " + Mathf.RoundToInt(s.DamageDealt) + "\n\n" +
                RunRecords.Summary(EndlessMode.Active ? "INFIERNO INFINITO NV " + (EndlessMode.Level + 1) : "MURIÓ") + "\n\n" +
                "EL INFIERNO NO ACEPTA DEVOLUCIONES.";
        }

        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / fadeDuration;
            yield return null;
        }

        group.alpha = 1f;

        group.interactable = true;
        group.blocksRaycasts = true;

        if (retryButton != null && EventSystem.current != null)
        {
            MenuButtonFx.MuteSelectionSound = true;
            EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
            MenuButtonFx.MuteSelectionSound = false;
        }
        // Si entraste al top, se pide el nombre (después de seleccionar el botón, para que no le robe el foco).
        if (RunRecords.LastRank > 0 && summary != null)
            RecordNamePrompt.Show(summary.font);
    }
}
