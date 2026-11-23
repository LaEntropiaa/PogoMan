using System.Collections.Generic;
using UnityEngine;

// Gema de experiencia que sueltan los enemigos. Se queda en el piso hasta que
// Pogoman pasa cerca (radio de recogida); entonces vuela hacia él.
public class XpGem : MonoBehaviour
{
    public static readonly List<XpGem> All = new List<XpGem>();

    // Las pone RunSystems al iniciar el nivel.
    public static XpGem Prefab;
    public static AudioClip PickupClip;

    [Tooltip("Materiales por valor: 1, 5 y 25 de XP.")]
    [SerializeField] private Material[] tierMaterials;
    [SerializeField] private float maxGems = 400;
    [SerializeField] private float magnetAcceleration = 70f;
    [SerializeField] private float maxSpeed = 32f;

    private static Transform player;
    private static float comboTime;
    private static int combo;

    private float value;
    private Vector3 velocity;
    private bool grounded;
    private bool attracted;
    private float bobOffset;
    private Vector3 restPosition;
    private MeshRenderer meshRenderer;

    public static void Drop(Vector3 position, float value)
    {
        if (Prefab == null)
            return;

        // Demasiadas gemas en el piso: la más vieja se junta con la nueva.
        if (All.Count >= Prefab.maxGems && All.Count > 0)
        {
            XpGem oldest = All[0];
            value += oldest.value;
            All.RemoveAt(0);
            Destroy(oldest.gameObject);
        }

        XpGem gem = Instantiate(Prefab, position, Quaternion.identity);
        gem.SetValue(value);
    }

    private void Awake() => meshRenderer = GetComponentInChildren<MeshRenderer>();
    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void SetValue(float v)
    {
        value = v;
        int tier = v >= 25f ? 2 : v >= 5f ? 1 : 0;
        if (tierMaterials != null && tierMaterials.Length > tier && meshRenderer != null)
            meshRenderer.sharedMaterial = tierMaterials[tier];
        transform.localScale = Vector3.one * (1f + tier * 0.35f);
    }

    // Gema con facetas (una copia girada) y un núcleo que brilla, del color de su valor. Sin partículas.
    private void Decorate()
    {
        if (meshRenderer == null) return;
        MeshFilter mf = meshRenderer.GetComponent<MeshFilter>();
        if (mf == null) return;
        Transform model = meshRenderer.transform;
        var facets = new GameObject("Facetas");
        facets.transform.SetParent(model, false);
        facets.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        facets.transform.localScale = new Vector3(0.85f, 1.1f, 0.85f);
        facets.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var fr = facets.AddComponent<MeshRenderer>();
        fr.sharedMaterial = meshRenderer.sharedMaterial;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Color c = meshRenderer.sharedMaterial != null && meshRenderer.sharedMaterial.HasProperty("_BaseColor") ? meshRenderer.sharedMaterial.GetColor("_BaseColor") : Color.cyan;
        var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(model, false);
        core.transform.localScale = Vector3.one * 0.32f;
        core.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
        ItemFx.Paint(core, Color.Lerp(c, Color.white, 0.6f), 2.5f);
    }

    private void Start()
    {
        Decorate();
        velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(4f, 6f), Random.Range(-2.5f, 2.5f));
        bobOffset = Random.value * 10f;

        if (player == null)
        {
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p != null)
                player = p.transform;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        transform.Rotate(0f, 120f * dt, 0f, Space.World);

        if (player != null)
        {
            Vector3 target = player.position + Vector3.up * 0.4f;
            Vector3 toPlayer = target - transform.position;
            float radius = PlayerStats.Instance != null ? PlayerStats.Instance.Get(PogoStat.PickupRadius) : 3f;

            if (!attracted && toPlayer.sqrMagnitude < radius * radius)
            {
                attracted = true;
                velocity = -toPlayer.normalized * 4f + Vector3.up * 3f;   // pequeño respingo antes de volar
            }

            if (attracted)
            {
                if (toPlayer.magnitude < 0.6f)
                {
                    Collect();
                    return;
                }
                velocity = Vector3.MoveTowards(velocity, toPlayer.normalized * maxSpeed, magnetAcceleration * dt);
                transform.position += velocity * dt;
                return;
            }
        }

        if (grounded)
        {
            // En el piso: flota y sube/baja un poquito.
            transform.position = restPosition + Vector3.up * (0.25f + Mathf.Sin(Time.time * 3f + bobOffset) * 0.08f);
            return;
        }

        // Cayendo: se detiene al tocar el suelo.
        velocity += Physics.gravity * dt;
        Vector3 step = velocity * dt;
        if (velocity.y < 0f && Physics.Raycast(transform.position, step.normalized, out RaycastHit hit,
                step.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<Enemy>() == null &&
            hit.collider.GetComponentInParent<PogoPlayer>() == null)
        {
            grounded = true;
            restPosition = hit.point;
            return;
        }
        transform.position += step;

        if (transform.position.y < -200f)
            grounded = true;   // por si cae al vacío, que no siga calculando
    }

    private void Collect()
    {
        PlayerStats.Instance?.AddXp(value);

        if (PickupClip != null)
        {
            // Si recoges varias seguidas, el sonido va subiendo de tono.
            combo = Time.unscaledTime - comboTime < 0.35f ? Mathf.Min(combo + 1, 14) : 0;
            comboTime = Time.unscaledTime;

            var go = new GameObject("SonidoGema");
            var src = go.AddComponent<AudioSource>();
            src.clip = PickupClip;
            src.pitch = 1f + combo * 0.045f;
            src.volume = 0.4f * GameSettings.SfxVolume;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(go, PickupClip.length / src.pitch + 0.1f);
        }

        Destroy(gameObject);
    }
}
