using UnityEngine;

// Animación por código del demonio hongo (el modelo no trae animaciones).
// Huesos: "Bone" = cuerpo y sombrero; "Bone.001/.002/.003" = sus tres patitas.
// Camina a saltitos moviendo las patas en trípode, el sombrero se bambolea y se aplasta,
// y para atacar se echa para atrás y lanza una mordida hacia adelante.
// Va en la raíz del modelo (la "Figura" del enemigo).
public class HongoRigAnimator : MonoBehaviour, IDemonRig
{
    [Header("Caminar")]
    [SerializeField] private float stepsPerMeter = 1.4f;
    [SerializeField] private float legSwing = 30f;
    [SerializeField] private float bodyWaddle = 9f;
    [SerializeField] private float bodyLean = 8f;
    [Tooltip("Cuánto se aplasta y estira el cuerpo con cada paso.")]
    [SerializeField] private float squash = 0.08f;

    [Header("Ataque")]
    [SerializeField] private float attackWindupLean = 22f;
    [SerializeField] private float attackBiteLean = -38f;

    private Transform body;
    private EnemyContactDamage attack;
    private Transform spine;
    private Transform[] legs;
    private Quaternion spineRest;
    private Vector3 spineScale;
    private Quaternion[] legRest;
    private SkinnedMeshRenderer[] renderers;
    private Vector3 lastPosition;
    private float phase;
    private float moveBlend;
    private float timeOffset;
    private bool initialized;

    private void Awake() => Init();

    private void Init()
    {
        if (initialized)
            return;
        initialized = true;

        body = transform.parent != null ? transform.parent : transform;
        attack = body.GetComponent<EnemyContactDamage>();

        spine = Find("Bone");
        legs = new[] { Find("Bone.001"), Find("Bone.002"), Find("Bone.003") };
        if (spine != null)
        {
            spineRest = spine.localRotation;
            spineScale = spine.localScale;
        }
        legRest = new Quaternion[legs.Length];
        for (int i = 0; i < legs.Length; i++)
            if (legs[i] != null)
                legRest[i] = legs[i].localRotation;

        lastPosition = body.position;
        timeOffset = Random.value * 10f;
    }

    private Transform Find(string boneName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == boneName)
                return t;
        return null;
    }

    private bool AnyVisible()
    {
        if (renderers == null)
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (Renderer r in renderers)
            if (r != null && r.enabled && r.isVisible)
                return true;
        return false;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 moved = body.position - lastPosition;
        lastPosition = body.position;
        float speed = moved.magnitude / dt;

        if (!AnyVisible())
            return;

        moveBlend = Mathf.MoveTowards(moveBlend, Mathf.Clamp01(speed / 1.5f), dt * 4f);
        phase += speed * dt * stepsPerMeter * Mathf.PI * 2f;

        float attackProgress = attack != null ? attack.AttackProgress : 0f;
        ApplyPose(phase, moveBlend, attackProgress, Time.time + timeOffset);
        ApplyReactions();
    }

    // Se aplasta al recibir daño y se infla (esporas) al quitarte de su cabeza.
    private EnemyTracker tracker;
    private void ApplyReactions()
    {
        if (spine == null) return;
        if (tracker == null) tracker = body.GetComponent<EnemyTracker>();
        if (tracker == null) return;
        float since = Time.time - tracker.LastDamageTime;
        float flinch = since < 0.35f ? Mathf.Sin(Mathf.Clamp01(since / 0.35f) * Mathf.PI) : 0f;
        float puff = tracker.Punish;
        if (flinch <= 0f && puff <= 0f) return;
        Rotate(spine, 18f * flinch, Mathf.Sin(Time.time * 30f) * 10f * puff);
        float sy = 1f - flinch * 0.22f + puff * 0.3f;
        float sw = 1f + flinch * 0.15f + puff * 0.25f;
        spine.localScale = Vector3.Scale(spine.localScale, new Vector3(sw, sy, sw));
    }

    // attackProgress: 0 = nada, 0..0.85 = preparando la mordida, 1 = mordida.
    public void ApplyPose(float phase, float moveBlend, float attackProgress, float time)
    {
        Init();

        if (spine != null)
        {
            spine.localRotation = spineRest;
            spine.localScale = spineScale;
        }
        for (int i = 0; i < legs.Length; i++)
            if (legs[i] != null)
                legs[i].localRotation = legRest[i];

        float breathe = Mathf.Sin(time * 2.2f);
        float windup = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(attackProgress / 0.85f));
        float bite = Mathf.Clamp01((attackProgress - 0.85f) / 0.15f);

        // Cuerpo: se inclina al caminar, se bambolea de lado a lado y se aplasta con cada paso.
        float lean = bodyLean * moveBlend + windup * attackWindupLean * (1f - bite) + bite * attackBiteLean;
        float waddle = Mathf.Sin(phase) * bodyWaddle * moveBlend + breathe * 1.5f;
        Rotate(spine, -lean, waddle);

        if (spine != null)
        {
            float step = Mathf.Abs(Mathf.Sin(phase)) * squash * moveBlend;
            float s = 1f - step + breathe * 0.02f - windup * (1f - bite) * 0.12f + bite * 0.15f;
            float w = 1f + (1f - s) * 0.6f;   // si se aplasta, se ensancha
            spine.localScale = new Vector3(spineScale.x * w, spineScale.y * s, spineScale.z * w);
        }

        // Patas en trípode: cada una con su desfase.
        for (int i = 0; i < legs.Length; i++)
        {
            float offset = i * Mathf.PI * 2f / 3f;
            float swing = Mathf.Sin(phase + offset) * legSwing * moveBlend;
            Rotate(legs[i], swing, Mathf.Cos(phase + offset) * legSwing * 0.3f * moveBlend);
        }
    }

    private void Rotate(Transform bone, float pitch, float roll)
    {
        if (bone == null)
            return;

        Quaternion delta =
            Quaternion.AngleAxis(pitch, transform.right) *
            Quaternion.AngleAxis(roll, transform.forward);

        bone.rotation = delta * bone.rotation;
    }
}
