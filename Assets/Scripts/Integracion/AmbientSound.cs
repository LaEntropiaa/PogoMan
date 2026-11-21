using UnityEngine;

// Sonido ambiental en bucle (lava, viento...). Respeta el volumen de efectos.
[RequireComponent(typeof(AudioSource))]
public class AmbientSound : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        GameSettings.VolumeChanged += ApplyVolume;
        ApplyVolume();

        if (source.clip != null)
            source.Play();
    }

    private void OnDestroy() => GameSettings.VolumeChanged -= ApplyVolume;

    private void ApplyVolume()
    {
        if (source != null)
            source.volume = volume * GameSettings.SfxVolume;
    }
}
