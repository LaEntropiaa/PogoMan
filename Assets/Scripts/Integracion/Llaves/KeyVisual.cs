using UnityEngine;

// Llave dorada armada con piezas simples (va en Key.prefab en lugar del cubo):
// flota, gira, brilla y suelta chispitas para que se vea desde lejos.
public class KeyVisual : MonoBehaviour
{
    [SerializeField] private Material goldMaterial;
    [SerializeField] private Material sparkleMaterial;
    [SerializeField] private float size = 1f;
    [SerializeField] private float height = 1.1f;

    private Transform pivot;
    private Light glow;
    private float phase;

    private void Start()
    {
        phase = Random.value * 10f;
        pivot = new GameObject("Llave3D").transform;
        pivot.SetParent(transform, false);
        pivot.localPosition = Vector3.up * height;
        pivot.localScale = Vector3.one * size;

        // Argolla: 10 piezas en círculo
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            Part(PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.22f, 0.32f + Mathf.Sin(a) * 0.22f, 0f),
                new Vector3(0f, 0f, a * Mathf.Rad2Deg), new Vector3(0.09f, 0.16f, 0.07f));
        }
        // Gema roja al centro de la argolla
        ItemFx.Paint(Part(PrimitiveType.Sphere, new Vector3(0f, 0.32f, 0f), Vector3.zero, Vector3.one * 0.13f), new Color(0.9f, 0.1f, 0.1f), 1f);
        // Tubo
        Part(PrimitiveType.Cylinder, new Vector3(0f, -0.12f, 0f), Vector3.zero, new Vector3(0.08f, 0.25f, 0.08f));
        // Dientes
        Part(PrimitiveType.Cube, new Vector3(0.09f, -0.26f, 0f), Vector3.zero, new Vector3(0.14f, 0.06f, 0.05f));
        Part(PrimitiveType.Cube, new Vector3(0.07f, -0.36f, 0f), Vector3.zero, new Vector3(0.1f, 0.06f, 0.05f));

        glow = pivot.gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.8f, 0.35f);
        glow.range = 5f;
        glow.shadows = LightShadows.None;

        var ps = pivot.gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 0.9f;
        main.startSpeed = 0.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new Color(1f, 0.85f, 0.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.1f;
        var em = ps.emission;
        em.rateOverTime = 14f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;
        ItemFx.FadeOut(ps);
        ItemFx.Apply(ps, sparkleMaterial);
        ps.Play();
    }

    private GameObject Part(PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(pivot, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        if (goldMaterial != null)
            r.sharedMaterial = goldMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    private void Update()
    {
        if (pivot == null)
            return;
        phase += Time.deltaTime;
        pivot.localPosition = Vector3.up * (height + Mathf.Sin(phase * 2.2f) * 0.15f);
        pivot.localRotation = Quaternion.Euler(0f, phase * 120f, 0f);
        glow.intensity = 2.2f + Mathf.Sin(phase * 5f) * 0.6f;
    }
}
