using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Estampita de San Pogo: en vez de morir, el tiempo se detiene unos segundos.
// Los demonios se quedan congelados (no se mueven, no atacan) y Pogoman no recibe daño,
// pero él sí puede moverse y pegarles.
public class TimeStop : MonoBehaviour
{
    public static bool Active => instance != null && instance.timeLeft > 0f;

    private static TimeStop instance;
    private static Font font;
    private static AudioClip stopClip, tickClip, resumeClip;
    private static Material fxMaterial;

    private float duration;
    private float timeLeft;
    private float age;
    private Vector3 origin;

    private Volume volume;
    private ColorAdjustments color;
    private Text title, seconds;
    private CanvasGroup group;
    private AudioSource audioSource;
    private int lastTick = -1;

    private class Frozen
    {
        public Rigidbody body;
        public bool wasKinematic;
        public Vector3 velocity;
        public readonly List<Behaviour> disabled = new List<Behaviour>();
    }
    private readonly Dictionary<EnemyTracker, Frozen> frozen = new Dictionary<EnemyTracker, Frozen>();
    private bool directorWasEnabled;

    public static void Configure(Font f, AudioClip stop, AudioClip tick, AudioClip resume, Material fx)
    {
        font = f;
        stopClip = stop;
        tickClip = tick;
        resumeClip = resume;
        fxMaterial = fx;
    }

    public static void Begin(float duration, Vector3 origin)
    {
        if (instance == null)
            instance = new GameObject("TiempoDetenido").AddComponent<TimeStop>();
        instance.StartStop(duration, origin);
    }

    private void Awake()
    {
        audioSource = UiKit.UiAudio(gameObject);

        // Filtro de color encima de todo (prioridad alta).
        var go = new GameObject("FiltroTiempo");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;
        volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        color = volume.profile.Add<ColorAdjustments>(true);

        BuildHud();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        if (volume != null && volume.profile != null)
            Destroy(volume.profile);
    }

    private void BuildHud()
    {
        Canvas canvas = UiKit.MakeCanvas("HUDTiempo", 8);
        canvas.transform.SetParent(transform, false);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        title = UiKit.Text(UiKit.Rect("Titulo", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -200), new Vector2(1600, 110)), font, 80, new Color(1f, 0.92f, 0.55f), TextAnchor.MiddleCenter, "¡SAN POGO DETUVO EL TIEMPO!");
        title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
        seconds = UiKit.Text(UiKit.Rect("Segundos", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -290), new Vector2(400, 90)), font, 72, Color.white, TextAnchor.MiddleCenter);
        seconds.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
    }

    private void StartStop(float seconds, Vector3 at)
    {
        duration = seconds;
        timeLeft = seconds;
        age = 0f;
        origin = at;
        lastTick = Mathf.CeilToInt(seconds);

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.ClockStopped = true;
        if (PlayerVitals.Instance != null)
            PlayerVitals.Instance.InvulnerableUntil = Time.time + seconds + 1f;

        // Mientras dura no salen demonios nuevos.
        EnemyDirector director = EnemyDirector.Instance;
        directorWasEnabled = director != null && director.enabled;
        if (director != null)
            director.enabled = false;

        Play(stopClip, 1f, 1f);
        ItemFx.Burst(at + Vector3.up, 60, 0.6f, new Color(1f, 0.9f, 0.5f), fxMaterial, 14f, 0.5f, 0.7f);
    }

    private void Update()
    {
        if (timeLeft <= 0f)
            return;

        float dt = Time.deltaTime;
        age += dt;
        timeLeft -= dt;

        FreezeNewEnemies();
        UpdateLook();

        int tick = Mathf.CeilToInt(timeLeft);
        if (tick != lastTick && tick > 0)
        {
            lastTick = tick;
            Play(tickClip, tick <= 3 ? 1.25f : 1f, tick <= 3 ? 0.9f : 0.6f);
        }
        this.seconds.text = Mathf.Max(0, Mathf.CeilToInt(timeLeft)).ToString();

        if (timeLeft <= 0f)
            End();
    }

    private void UpdateLook()
    {
        // 0–0.35 s: destello con los colores invertidos; luego gris amarillento; al final regresa el color.
        float flash = Mathf.Clamp01(1f - age / 0.35f);
        float settle = Mathf.Clamp01((age - 0.2f) / 0.4f);
        float outro = Mathf.Clamp01(timeLeft / 0.6f);

        volume.weight = outro;
        color.hueShift.Override(180f * flash);
        color.contrast.Override(Mathf.Lerp(60f * flash, 25f, settle));
        color.saturation.Override(Mathf.Lerp(0f, -85f, settle));
        color.colorFilter.Override(Color.Lerp(Color.white, new Color(1f, 0.95f, 0.78f), settle));
        color.postExposure.Override(flash * 0.8f);

        group.alpha = Mathf.Clamp01(age / 0.3f) * outro;
        float pop = 1f + Mathf.Clamp01(1f - age / 0.4f) * 0.3f;
        title.rectTransform.localScale = Vector3.one * pop;
        seconds.rectTransform.localScale = Vector3.one * (1f + (timeLeft % 1f) * 0.15f);
        seconds.color = timeLeft <= 3f ? new Color(1f, 0.45f, 0.35f) : Color.white;
    }

    // Congela a todos los demonios (también a los que aparezcan mientras tanto, p. ej. jefes).
    private void FreezeNewEnemies()
    {
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || frozen.ContainsKey(e))
                continue;

            var f = new Frozen { body = e.GetComponent<Rigidbody>() };
            if (f.body != null)
            {
                f.wasKinematic = f.body.isKinematic;
                if (!f.wasKinematic)
                    f.velocity = f.body.linearVelocity;
                f.body.isKinematic = true;
            }

            foreach (Behaviour b in e.GetComponentsInChildren<Behaviour>())
            {
                // El tracker sigue (números de daño, muerte); lo demás se apaga.
                if (b == null || !b.enabled || b is EnemyTracker || b is AudioSource)
                    continue;
                b.enabled = false;
                f.disabled.Add(b);
            }
            frozen[e] = f;
        }
    }

    private void End()
    {
        timeLeft = 0f;
        volume.weight = 0f;
        group.alpha = 0f;

        foreach (KeyValuePair<EnemyTracker, Frozen> pair in frozen)
        {
            if (pair.Key == null)
                continue;
            Frozen f = pair.Value;
            foreach (Behaviour b in f.disabled)
                if (b != null)
                    b.enabled = true;
            if (f.body != null)
            {
                f.body.isKinematic = f.wasKinematic;
                if (!f.wasKinematic)
                    f.body.linearVelocity = f.velocity;
            }
        }
        frozen.Clear();

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.ClockStopped = false;
        if (PlayerVitals.Instance != null)
            PlayerVitals.Instance.InvulnerableUntil = Time.time + 1f;   // un respiro al reanudar
        if (directorWasEnabled && EnemyDirector.Instance != null)
            EnemyDirector.Instance.enabled = true;

        Play(resumeClip, 1f, 0.9f);
    }

    private void Play(AudioClip clip, float pitch, float vol)
    {
        if (clip == null)
            return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, vol * GameSettings.SfxVolume);
    }
}
