using UnityEngine;

// Ambiente del infierno: un zumbido de fondo en bucle y, cada tanto, sonidos lejanos
// (gritos, cadenas, gruñidos, campanas) que salen de algún lado alrededor de Pogoman.
// En la arena cambia a su propio ambiente.
public class AmbientDirector : MonoBehaviour
{
    [SerializeField] private AudioClip mapLoop;
    [SerializeField] private AudioClip arenaLoop;
    [SerializeField, Range(0f, 1f)] private float loopVolume = 0.35f;
    [SerializeField] private AudioClip[] distantSounds;
    [SerializeField] private Vector2 interval = new Vector2(5f, 12f);
    [SerializeField] private Vector2 distance = new Vector2(14f, 30f);
    [SerializeField, Range(0f, 1f)] private float distantVolume = 0.7f;

    private AudioSource loop;
    private AudioSource oneShot;
    private Transform player;
    private float timer;
    private bool inArena;

    private void Awake()
    {
        loop = gameObject.AddComponent<AudioSource>();
        loop.loop = true;
        loop.playOnAwake = false;
        loop.spatialBlend = 0f;

        var go = new GameObject("SonidoLejano");
        go.transform.SetParent(transform, false);
        oneShot = go.AddComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.spatialBlend = 1f;
        oneShot.rolloffMode = AudioRolloffMode.Linear;
        oneShot.minDistance = 8f;
        oneShot.maxDistance = 60f;
        oneShot.dopplerLevel = 0f;

        GameSettings.VolumeChanged += ApplyVolume;
    }

    private void OnDestroy() => GameSettings.VolumeChanged -= ApplyVolume;

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>()?.transform;
        Play(mapLoop);
        timer = Random.Range(interval.x, interval.y);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null)
            return;
        loop.clip = clip;
        loop.time = Random.Range(0f, clip.length * 0.9f);
        loop.Play();
        ApplyVolume();
    }

    private void ApplyVolume() => loop.volume = loopVolume * GameSettings.SfxVolume;

    private void Update()
    {
        if (!inArena && ArenaFight.InArena)
        {
            inArena = true;
            Play(arenaLoop);
        }

        if (player == null || distantSounds == null || distantSounds.Length == 0)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;
        timer = Random.Range(interval.x, interval.y);

        AudioClip clip = distantSounds[Random.Range(0, distantSounds.Length)];
        if (clip == null)
            return;
        Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(distance.x, distance.y);
        oneShot.transform.position = player.position + new Vector3(dir.x, Random.Range(2f, 8f), dir.y);
        oneShot.pitch = Random.Range(0.85f, 1.1f);
        oneShot.PlayOneShot(clip, distantVolume * GameSettings.SfxVolume);
    }
}
