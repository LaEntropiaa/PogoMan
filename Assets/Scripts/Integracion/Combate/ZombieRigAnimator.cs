using UnityEngine;

// Animación por código del zombie usando su esqueleto (el modelo no trae animaciones).
// Cada frame el esqueleto vuelve a su pose de reposo y luego se rota hueso por hueso
// de padre a hijo, así el antebrazo sigue al brazo, la espinilla al muslo, etc.
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
    [SerializeField] private float stepsPerMeter = 0.9f;
    [SerializeField] private float legSwing = 22f;
    [SerializeField] private float kneeBend = 25f;
    [Tooltip("Brazos al frente estilo zombie (negativo = hacia adelante).")]
    [SerializeField] private float armsForward = -70f;
    [SerializeField] private float armSwing = 8f;
    [SerializeField] private float elbowBend = -15f;
    [SerializeField] private float spineLean = 8f;
    [SerializeField] private float spineSway = 5f;
    [SerializeField] private float headWobble = 8f;

    [Header("Ataque")]
    [Tooltip("Cuánto levanta los brazos al preparar el golpe (negativo = arriba/adelante).")]
    [SerializeField] private float attackRaise = -60f;
    [SerializeField] private float attackSlam = 40f;

    private Transform body;           // el enemigo que se mueve
    private EnemyContactDamage attack;
    private Transform[] bones;
    private Quaternion[] restLocal;
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

        // Orden de padre a hijo (los hijos heredan la rotación de su padre).
        bones = new[] { spine, head, armLeft, armRight, foreLeft, foreRight, thighLeft, thighRight, shinLeft, shinRight };
        restLocal = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null)
                restLocal[i] = bones[i].localRotation;

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

        float attackProgress = attack != null ? attack.AttackProgress : 0f;
        ApplyPose(phase, moveBlend, attackProgress, Time.time + timeOffset);
    }

    // attackProgress: 0 = nada, 0..0.85 = preparando el golpe, 1 = golpe.
    public void ApplyPose(float phase, float moveBlend, float attackProgress, float time)
    {
        Init();

        // 1) Volver a la pose de reposo.
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null)
                bones[i].localRotation = restLocal[i];

        float s = Mathf.Sin(phase);
        float c = Mathf.Cos(phase);
        float breathe = Mathf.Sin(time * 1.8f);

        // Ataque: levanta los brazos mientras se prepara y los azota al final.
        float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(attackProgress / 0.85f));
        float slam = Mathf.Clamp01((attackProgress - 0.85f) / 0.15f);
        float armsAttack = raise * attackRaise + slam * (attackSlam - attackRaise);
        float attacking = Mathf.Clamp01(attackProgress * 3f);

        // 2) Rotar de padre a hijo (ángulos alrededor del eje X del zombie; negativo = adelante).
        Rotate(spine, spineLean * moveBlend + breathe * 1.5f + attacking * 6f, Mathf.Sin(phase * 0.5f) * spineSway * moveBlend);
        Rotate(head, Mathf.Sin(phase + 1f) * headWobble * moveBlend + breathe * 2f, Mathf.Sin(phase * 0.5f + 0.7f) * headWobble);

        float armBase = armsForward + armsAttack * attacking;
        Rotate(armLeft, armBase + s * armSwing * moveBlend + breathe * 2f, 0f);
        Rotate(armRight, armBase - s * armSwing * moveBlend + breathe * 2f, 0f);
        Rotate(foreLeft, elbowBend, 0f);
        Rotate(foreRight, elbowBend, 0f);

        Rotate(thighLeft, -s * legSwing * moveBlend, 0f);
        Rotate(thighRight, s * legSwing * moveBlend, 0f);
        Rotate(shinLeft, Mathf.Max(0f, c) * kneeBend * moveBlend, 0f);
        Rotate(shinRight, Mathf.Max(0f, -c) * kneeBend * moveBlend, 0f);
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
