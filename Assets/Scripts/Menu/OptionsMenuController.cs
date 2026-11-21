using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Submenú de opciones: volúmenes, modo de pantalla y resolución.
// Todo se guarda en GameSettings (PlayerPrefs).
public class OptionsMenuController : MonoBehaviour
{
    [Header("Volumen (0 a 10)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Text masterValue;
    [SerializeField] private Text musicValue;
    [SerializeField] private Text sfxValue;

    [Header("Pantalla")]
    [SerializeField] private OptionCycler displayModeOption;
    [SerializeField] private OptionCycler resolutionOption;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private int resolutionIndex;
    private bool fullscreen;
    private bool initializing;

    private void Awake()
    {
        initializing = true;

        Setup(masterSlider, masterValue, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        Setup(musicSlider, musicValue, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
        Setup(sfxSlider, sfxValue, GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);

        BuildResolutionList();
        fullscreen = GameSettings.Fullscreen;

        if (displayModeOption != null)
            displayModeOption.Changed += _ => { fullscreen = !fullscreen; ApplyDisplay(); };

        if (resolutionOption != null)
            resolutionOption.Changed += dir =>
            {
                resolutionIndex = (resolutionIndex + dir + resolutions.Count) % resolutions.Count;
                ApplyDisplay();
            };

        RefreshDisplayTexts();
        initializing = false;
    }

    private void OnDisable()
    {
        GameSettings.Save();
    }

    private void Setup(Slider slider, Text valueText, float current, System.Action<float> apply)
    {
        if (slider == null)
            return;

        slider.minValue = 0;
        slider.maxValue = 10;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(Mathf.Round(current * 10f));
        UpdateValueText(valueText, slider.value);

        slider.onValueChanged.AddListener(v =>
        {
            apply(v / 10f);
            UpdateValueText(valueText, v);

            if (!initializing)
                MenuAudio.Instance?.PlayHover();
        });
    }

    private static void UpdateValueText(Text text, float v)
    {
        if (text != null)
            text.text = Mathf.RoundToInt(v * 10f) + "%";
    }

    private void BuildResolutionList()
    {
        resolutions.Clear();

        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (size.x >= 800 && size.y >= 600 && !resolutions.Contains(size))
                resolutions.Add(size);
        }

        Vector2Int saved = GameSettings.Resolution;
        if (!resolutions.Contains(saved))
            resolutions.Add(saved);

        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        resolutionIndex = resolutions.IndexOf(saved);
    }

    private void ApplyDisplay()
    {
        Vector2Int r = resolutions[resolutionIndex];
        GameSettings.SetDisplay(fullscreen, r.x, r.y);
        RefreshDisplayTexts();
    }

    private void RefreshDisplayTexts()
    {
        if (displayModeOption != null)
            displayModeOption.SetText(fullscreen ? "PANTALLA COMPLETA" : "VENTANA");

        if (resolutionOption != null && resolutions.Count > 0)
        {
            Vector2Int r = resolutions[resolutionIndex];
            resolutionOption.SetText(r.x + "x" + r.y);
        }
    }
}
