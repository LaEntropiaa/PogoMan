using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Los pilares ya no despiertan solos: te acercas y con E despiertas al guardián.
// Usa el BossPillar de Alonso tal cual: solo le apaga la activación por distancia
// y llama a su SpawnBoss cuando el jugador interactúa.
public class PillarAltar : MonoBehaviour
{
    [SerializeField] private float interactDistance = 5f;
    [SerializeField] private Font font;
    [SerializeField] private Material fxMaterial;
    [SerializeField] private AudioClip awakenClip;

    private static readonly MethodInfo SpawnBoss =
        typeof(BossPillar).GetMethod("SpawnBoss", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly List<BossPillar> pillars = new List<BossPillar>();
    private readonly Dictionary<BossPillar, ParticleSystem> glows = new Dictionary<BossPillar, ParticleSystem>();
    private Transform player;
    private Text prompt;
    private AudioSource audioSource;

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>()?.transform;
        audioSource = UiKit.UiAudio(gameObject);
        foreach (BossPillar p in FindObjectsByType<BossPillar>())
        {
            PogoBridge.SetFloat(p, "activationRadius", -1f);   // nunca por distancia
            pillars.Add(p);
            glows[p] = MakeGlow(p.transform);
        }

        Canvas canvas = UiKit.MakeCanvas("HUDPilar", 6);
        canvas.transform.SetParent(transform, false);
        prompt = UiKit.Text(UiKit.Rect("Prompt", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 190), new Vector2(1200, 60)), font, 40, new Color(0.85f, 0.55f, 1f), TextAnchor.MiddleCenter);
        prompt.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
    }

    // Humito morado que sube del pilar mientras su guardián duerme.
    private ParticleSystem MakeGlow(Transform pillar)
    {
        var go = new GameObject("AuraPilar");
        go.transform.SetParent(pillar, false);
        go.transform.localPosition = Vector3.up * 0.5f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 2.2f;
        main.startSpeed = 1.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startColor = new Color(0.65f, 0.2f, 1f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var em = ps.emission;
        em.rateOverTime = 22f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.6f;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ItemFx.FadeOut(ps);
        ItemFx.Apply(ps, fxMaterial);
        ps.Play();
        return ps;
    }

    private static bool Awakened(BossPillar p) => PogoBridge.GetBool(p, "bossSpawned");

    private void Update()
    {
        if (player == null)
            return;

        BossPillar near = null;
        float best = interactDistance;
        foreach (BossPillar p in pillars)
        {
            if (p == null || !p.isActiveAndEnabled || Awakened(p))
                continue;
            Vector3 d = p.transform.position - player.position;
            d.y = 0f;
            if (d.magnitude < best)
            {
                best = d.magnitude;
                near = p;
            }
        }

        prompt.text = near != null && Time.timeScale > 0f && !ChestUI.IsOpen ? "E  DESPERTAR AL GUARDIÁN DEL PILAR" : "";
        if (near == null || Time.timeScale <= 0f || ChestUI.IsOpen)
            return;

        bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
                       (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
        if (!pressed)
            return;

        SpawnBoss?.Invoke(near, null);
        if (glows.TryGetValue(near, out ParticleSystem glow) && glow != null)
            glow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        ItemFx.Burst(near.transform.position + Vector3.up * 2f, 70, 1.5f, new Color(0.7f, 0.2f, 1f), fxMaterial, 10f, 0.5f, 0.9f);
        if (awakenClip != null)
            audioSource.PlayOneShot(awakenClip, GameSettings.SfxVolume);
    }

    public IReadOnlyList<BossPillar> Pillars => pillars;
    public static bool IsAwake(BossPillar p) => Awakened(p);
}
