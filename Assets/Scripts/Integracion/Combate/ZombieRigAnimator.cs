using UnityEngine;

// Animación por código del zombie usando su esqueleto (el modelo no trae animaciones).
// Camina arrastrando los pies con los brazos al frente, respira cuando está quieto
// y lanza los brazos cuando tiene a Pogoman cerca.
// Va en la raíz del modelo (la "Figura" del enemigo).
public class ZombieRigAnimator : MonoBehaviour
{
    [Header("Huesos (se buscan por nombre si están vacíos)")]
    [SerializeField] private Transform spine;       // Bone
    [SerializeField] private Transform head;        // Bone.001
    [SerializeField] private Transform armLeft;     // Bone.003
    [SerializeField] private Transform armRight;    // Bone.008
    [SerializeField] private Transform foreLeft;    // Bone.005
    [SerializeField] private Transform foreRight;   // Bone.010
    [SerializeField] private Transform thighLeft;   // Bone.019
    [SerializeField] private Transform thighRight;  // Bone.014
    [SerializeField] private Transform shinLeft;    // Bone.021
    [SerializeField] private Transform shinRight;   // Bone.016

    [Header("Caminar")]
    [SerializeField] private float stepsPerMeter = 1.1f;
    [SerializeField] private float legSwing = 28f;
    [SerializeField] private float kneeBend = 30f;
    [SerializeField] private float armsForward = -75f;
    [SerializeField] private float armSwing = 12f;
    [SerializeField] private float spineLean = 12f;
    [SerializeField] private float headWobble = 10f;

    [Header("Ataque")]
    [SerializeField] private float attackDistance = 2.2f;
    [SerializeField] private float attackLunge = -35f;

    private Transform body;           // el enemigo (se mueve)
    private Transform[] bones;
    private Quaternion[] rest;        // rotación de reposo relativa a la raíz
    private Vector3 lastPosition;
    private float phase;
    private float moveBlend;
    private float attackBlend;
    private Transform player;
    private float randomOffset;

    private bool initialized;

    private void Awake() => Init();

    private void Init()
    {
        if (initialized)
            return;
        initialized = true;

        body = transform.parent != null ? transform.parent : transform;

        if (spine == null) spine = Find("Bone");
        if (head == null) head = Find("Bone.001");
        if (armLeft == null) armLeft = Find("Bone.003");
        if (armRight == null) armRight = Find("Bone.008");
        if (foreLeft == null) foreLeft = Find("Bone.005");
        if (foreRight == null) foreRight = Find("Bone.010");
        if (thighLeft == null) thighLeft = Find("Bone.019");
        if (thighRight == null) thighRight = Find("Bone.014");
        if (shinLeft == null) shinLeft = Find("Bone.021");
        if (shinRight == null) shinRight = Find("Bone.016");

        bones = new[] { spine, head, armLeft, armRight, foreLeft, foreRight, thighLeft, thighRight, shinLeft, shinRight };
        rest = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null)
                rest[i] = Quaternion.Inverse(transform.rotation) * bones[i].rotation;

        lastPosition = body.position;
        randomOffset = Random.value * 10f;
    }

    private Transform Find(string boneName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == boneName)
                return t;
        return null;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 moved = body.position - lastPosition;
        moved.y = 0f;
        lastPosition = body.position;
        float speed = moved.magnitude / dt;

        moveBlend = Mathf.MoveTowards(moveBlend, Mathf.Clamp01(speed / 1.5f), dt * 4f);
        phase += speed * dt * stepsPerMeter * Mathf.PI * 2f;

        if (player == null)
        {
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p != null)
                player = p.transform;
        }

        bool near = player != null && Vector3.Distance(player.position, body.position) < attackDistance;
        attackBlend = Mathf.MoveTowards(attackBlend, near ? 1f : 0f, dt * 5f);

        ApplyPose(phase, moveBlend, attackBlend, Time.time + randomOffset);
    }

    // Pone el esqueleto en una pose del ciclo. También sirve para previsualizar en el editor.
    public void ApplyPose(float phase, float moveBlend, float attackBlend, float time)
    {
        Init();
        float s = Mathf.Sin(phase);
        float c = Mathf.Cos(phase);
        float idle = Mathf.Sin(time * 1.8f);   // respiración
        float lunge = attackBlend * (0.6f + 0.4f * Mathf.Sin(time * 9f));

        // Ángulos en grados alrededor del eje X local de la raíz (negativo = hacia adelante).
        Pose(0, spineLean * moveBlend + idle * 2f + lunge * 8f, Mathf.Sin(phase * 0.5f) * 5f * moveBlend);
        Pose(1, -headWobble * 0.5f + Mathf.Sin(phase + 1f) * headWobble * moveBlend + idle * 3f, Mathf.Sin(phase * 0.5f + 0.7f) * 8f);
        Pose(2, armsForward + s * armSwing * moveBlend + idle * 3f + lunge * attackLunge, 0f);
        Pose(3, armsForward - s * armSwing * moveBlend + idle * 3f + lunge * attackLunge, 0f);
        Pose(4, -10f * moveBlend - lunge * 10f, 0f);
        Pose(5, -10f * moveBlend - lunge * 10f, 0f);
        Pose(6, -s * legSwing * moveBlend, 0f);
        Pose(7, s * legSwing * moveBlend, 0f);
        Pose(8, Mathf.Max(0f, c) * kneeBend * moveBlend, 0f);
        Pose(9, Mathf.Max(0f, -c) * kneeBend * moveBlend, 0f);
    }

    private void Pose(int index, float pitch, float roll)
    {
        Transform bone = bones[index];
        if (bone == null)
            return;
        bone.rotation = transform.rotation * Quaternion.Euler(pitch, 0f, roll) * rest[index];
    }
}
