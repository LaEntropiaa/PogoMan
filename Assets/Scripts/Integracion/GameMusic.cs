using UnityEngine;

// Música de fondo de un nivel: suena en bucle, entra con un fundido suave
// y respeta el volumen de música de Opciones.
[RequireComponent(typeof(AudioSource))]
public class GameMusic : MonoBehaviour
{
    [SerializeField] private float fadeInSeconds = 1.5f;

    private AudioSource source;
    private float fade;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;

        fade = fadeInSeconds > 0f ? 0f : 1f;
        GameSettings.VolumeChanged += ApplyVolume;
        ApplyVolume();

        if (source.clip != null)
            source.Play();
    }

    public AudioClip CurrentClip => source != null ? source.clip : null;

    // Cambia de canción con fundido (p. ej. al entrar a la arena).
    public void SwitchTo(AudioClip clip, float fadeSeconds = 1.2f)
    {
        if (clip == null || source.clip == clip)
            return;
        StopAllCoroutines();
        StartCoroutine(Switch(clip, fadeSeconds));
    }

    private System.Collections.IEnumerator Switch(AudioClip clip, float seconds)
    {
        for (float f = fade; f > 0f; f -= Time.unscaledDeltaTime / Mathf.Max(0.05f, seconds))
        {
            fade = f;
            ApplyVolume();
            yield return null;
        }
        fade = 0f;
        source.clip = clip;
        source.time = 0f;
        source.Play();
        // Update() hace el fundido de entrada.
    }

    private void OnDestroy()
    {
        GameSettings.VolumeChanged -= ApplyVolume;
    }

    private void Update()
    {
        if (fade >= 1f)
            return;

        fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / fadeInSeconds);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (source != null)
            source.volume = GameSettings.MusicVolume * fade;
    }
}
