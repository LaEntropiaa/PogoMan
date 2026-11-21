using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Fundido a negro y carga de escena.
public class SceneTransition : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeDuration = 0.6f;

    private bool loading;

    public void LoadScene(string sceneName)
    {
        if (loading)
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"SceneTransition: la escena '{sceneName}' no está en la lista de la build.");
            return;
        }

        loading = true;
        MenuAudio.Instance?.FadeOutMusic(fadeDuration);
        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        if (fadeGroup != null)
        {
            fadeGroup.blocksRaycasts = true;

            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                fadeGroup.alpha = t / fadeDuration;
                yield return null;
            }

            fadeGroup.alpha = 1f;
        }

        op.allowSceneActivation = true;
    }
}
