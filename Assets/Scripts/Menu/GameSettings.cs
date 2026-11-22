using System;
using UnityEngine;

// Preferencias del jugador (volumen, pantalla y filtro pixel), guardadas con PlayerPrefs.
// Se aplican solas al arrancar el juego, sin importar la escena inicial.
public static class GameSettings
{
    private const string KeyMaster = "pogoman.volumen.general";
    private const string KeyMusic = "pogoman.volumen.musica";
    private const string KeySfx = "pogoman.volumen.efectos";
    private const string KeyFullscreen = "pogoman.pantalla.completa";
    private const string KeyResWidth = "pogoman.pantalla.ancho";
    private const string KeyResHeight = "pogoman.pantalla.alto";
    private const string KeyPixel = "pogoman.pantalla.pixel";

    // Filtro que pixela el juego: 1 = apagado, 2 = suave ... 5 = muy retro.
    public static readonly string[] PixelLevelNames = { "APAGADO", "SUAVE", "MEDIO", "RETRO", "MUY RETRO" };

    public static int PixelSize
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyPixel, 2), 1, PixelLevelNames.Length);
        set => PlayerPrefs.SetInt(KeyPixel, Mathf.Clamp(value, 1, PixelLevelNames.Length));
    }

    // Se dispara cuando cambia algún volumen, para que los AudioSource se actualicen.
    public static event Action VolumeChanged;

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(KeyMaster, 1f);
        set
        {
            PlayerPrefs.SetFloat(KeyMaster, Mathf.Clamp01(value));
            AudioListener.volume = MasterVolume;
            VolumeChanged?.Invoke();
        }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(KeyMusic, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(KeyMusic, Mathf.Clamp01(value));
            VolumeChanged?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(KeySfx, 1f);
        set
        {
            PlayerPrefs.SetFloat(KeySfx, Mathf.Clamp01(value));
            VolumeChanged?.Invoke();
        }
    }

    public static bool Fullscreen => PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

    public static Vector2Int Resolution => new Vector2Int(
        PlayerPrefs.GetInt(KeyResWidth, Screen.currentResolution.width),
        PlayerPrefs.GetInt(KeyResHeight, Screen.currentResolution.height));

    public static void SetDisplay(bool fullscreen, int width, int height)
    {
        PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyResWidth, width);
        PlayerPrefs.SetInt(KeyResHeight, height);
        ApplyDisplay();
    }

    // Sensibilidad de la cámara con el mouse (multiplica la del juego): 0.2x a 3x.
    public static float MouseSensitivity
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("sensibilidad", 1f), 0.2f, 3f);
        set => PlayerPrefs.SetFloat("sensibilidad", Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 0.2f, 3f));
    }

    public static void Save() => PlayerPrefs.Save();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        AudioListener.volume = MasterVolume;

        if (PlayerPrefs.HasKey(KeyResWidth))
            ApplyDisplay();
    }

    private static void ApplyDisplay()
    {
        // En el editor la resolución la controla la ventana Game.
        if (Application.isEditor)
            return;

        Vector2Int res = Resolution;
        Screen.SetResolution(
            res.x,
            res.y,
            Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }
}
