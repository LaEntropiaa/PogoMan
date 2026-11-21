using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Cinemática de introducción. Al terminar (o al saltarla) carga el menú,
// que arranca en negro con su música, así la transición queda continua.
// Para saltar: una pulsación muestra el aviso y la segunda la salta
// (evita saltar 3 minutos de intro por accidente).
public class IntroCinematic : MonoBehaviour
{
    [SerializeField] private VideoPlayer video;
    [SerializeField] private AudioSource videoAudio;
    [SerializeField] private string nextScene = "MainMenu";

    [Header("Saltar")]
    [SerializeField] private CanvasGroup skipHint;
    [SerializeField] private float confirmWindow = 2.5f;
    [SerializeField] private float skipAudioFade = 0.4f;

    [Header("Seguridad")]
    [Tooltip("Si el video no carga en este tiempo, se pasa directo al menú.")]
    [SerializeField] private float prepareTimeout = 10f;

    private bool leaving;
    private float confirmUntil = -1f;
    private float prepareTimer;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        Time.timeScale = 1f;

        if (skipHint != null)
            skipHint.alpha = 0f;

        if (video == null || video.clip == null)
        {
            Leave();
            return;
        }

        if (videoAudio != null)
        {
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.EnableAudioTrack(0, true);
            video.SetTargetAudioSource(0, videoAudio);
        }

        video.errorReceived += (_, message) =>
        {
            Debug.LogWarning("IntroCinematic: error con el video: " + message);
            Leave();
        };
        video.loopPointReached += _ => Leave();
        video.prepareCompleted += v => v.Play();
        video.Prepare();
    }

    private void Update()
    {
        if (leaving)
            return;

        if (!video.isPrepared)
        {
            prepareTimer += Time.unscaledDeltaTime;
            if (prepareTimer > prepareTimeout)
                Leave();
        }

        if (SkipPressed())
        {
            if (Time.unscaledTime < confirmUntil)
            {
                StartCoroutine(SkipRoutine());
                return;
            }

            confirmUntil = Time.unscaledTime + confirmWindow;
        }

        if (skipHint != null)
        {
            float target = Time.unscaledTime < confirmUntil ? 1f : 0f;
            skipHint.alpha = Mathf.MoveTowards(skipHint.alpha, target, Time.unscaledDeltaTime * 4f);
        }
    }

    private IEnumerator SkipRoutine()
    {
        leaving = true;

        if (skipHint != null)
            skipHint.alpha = 0f;

        // Bajamos el audio del video rápido para que no corte en seco.
        float start = videoAudio != null ? videoAudio.volume : 0f;
        for (float t = 0f; t < skipAudioFade && videoAudio != null; t += Time.unscaledDeltaTime)
        {
            videoAudio.volume = Mathf.Lerp(start, 0f, t / skipAudioFade);
            yield return null;
        }

        video.Stop();
        SceneManager.LoadScene(nextScene);
    }

    private void Leave()
    {
        if (leaving)
            return;

        leaving = true;
        SceneManager.LoadScene(nextScene);
    }

    private static bool SkipPressed()
    {
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool pad = Gamepad.current != null &&
                   (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame);
        return key || mouse || pad;
    }
}
