using UnityEngine;
using UnityEngine.SceneManagement;

// Cambios de escena desde el juego (pausa y pantalla de muerte).
public static class SceneNav
{
    public const string MainMenuScene = "MainMenu";

    public static void ToMainMenu()
    {
        Time.timeScale = 1f;
        MenuIntroSequence.SkipNextIntro = true;   // al volver no repetimos la entrada del logo
        SceneManager.LoadScene(MainMenuScene);
    }

    public static void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void SetGameplayCursor(bool playing)
    {
        Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;

        PogoCamera cam = Object.FindAnyObjectByType<PogoCamera>();
        if (cam != null)
            cam.enabled = playing;   // que la cámara no gire mientras estás en un menú
    }
}
