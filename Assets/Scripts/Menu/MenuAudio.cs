using System.Collections;
using UnityEngine;

// Música y efectos del menú. Una sola instancia por escena.
public class MenuAudio : MonoBehaviour
{
    public static MenuAudio Instance { get; private set; }

    [Header("Fuentes de audio")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Efectos")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip impactClip;

    private float musicFade = 1f;

    public bool HasMusic => musicSource != null && musicSource.clip != null;
    public AudioClip MusicClip => HasMusic ? musicSource.clip : null;
    public bool IsMusicPlaying => HasMusic && musicSource.isPlaying;
    public float MusicTime => HasMusic ? (float)musicSource.timeSamples / musicSource.clip.frequency : 0f;

    private void Awake()
    {
        Instance = this;
        GameSettings.VolumeChanged += ApplyVolumes;
        ApplyVolumes();
    }

    private void OnDestroy()
    {
        GameSettings.VolumeChanged -= ApplyVolumes;

        if (Instance == this)
            Instance = null;
    }

    private void ApplyVolumes()
    {
        if (musicSource != null)
            musicSource.volume = GameSettings.MusicVolume * musicFade;

        if (sfxSource != null)
            sfxSource.volume = GameSettings.SfxVolume;
    }

    // Programa la música para que suene exactamente en dspStartTime,
    // empezando desde el segundo startSeconds de la canción.
    public bool ScheduleMusic(double dspStartTime, float startSeconds)
    {
        if (!HasMusic)
            return false;

        AudioClip clip = musicSource.clip;
        float start = Mathf.Clamp(startSeconds, 0f, Mathf.Max(0f, clip.length - 0.1f));

        musicSource.Stop();
        musicSource.timeSamples = Mathf.FloorToInt(start * clip.frequency);
        musicSource.PlayScheduled(dspStartTime);
        return true;
    }

    public void PlayHover() => PlayOneShot(hoverClip);
    public void PlayClick() => PlayOneShot(clickClip);
    public void PlayImpact() => PlayOneShot(impactClip);

    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void FadeOutMusic(float duration)
    {
        if (HasMusic)
            StartCoroutine(FadeRoutine(duration));
    }

    private IEnumerator FadeRoutine(float duration)
    {
        float start = musicFade;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            musicFade = Mathf.Lerp(start, 0f, t / duration);
            ApplyVolumes();
            yield return null;
        }

        musicFade = 0f;
        ApplyVolumes();
    }
}
