using UnityEngine;
using UnityEngine.SceneManagement;

// Lógica del menú principal. Los botones llaman a Play() y Quit().
public class MainMenu : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string gameSceneName = "SampleScene";

    private void Start()
    {
        // La cámara del juego bloquea el mouse; en el menú lo necesitamos libre.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Por si venimos de un menú de pausa con el tiempo detenido.
        Time.timeScale = 1f;
    }

    public void Play()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        // En el editor Application.Quit() no hace nada, así que detenemos el Play.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
