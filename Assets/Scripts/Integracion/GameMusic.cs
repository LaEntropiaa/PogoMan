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
