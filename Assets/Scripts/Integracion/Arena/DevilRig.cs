using System.Collections.Generic;
using UnityEngine;

// Animación por código del Diablo Tendero sobre su esqueleto (su FBX solo trae la reverencia).
// Poses: volando (piernas colgando, brazos abiertos), lanzar bola de fuego, invocar (brazos arriba),
// risa, aturdido (encorvado) y muerte. Cada frame parte de la pose de reposo y suma giros.
public class DevilRig : MonoBehaviour
{
    public enum Pose { Intro, Fly, Stunned, Dying }

    public Pose State { get; set; } = Pose.Intro;
    public Transform LookTarget { get; set; }

    // Signos de giro (para ajustar si un brazo sale al revés).
    [SerializeField] private float armRaiseSign = 1f;
    [SerializeField] private float legBendSign = 1f;
    [SerializeField] private float throwSign = 1f;

    private Transform character;
    private Transform hips, spine, chest, neck, head;
    private Transform upperL, upperR, foreL, foreR, handL, handR;
    private Transform thighL, thighR, shinL, shinR;
    private readonly List<Transform> bones = new List<Transform>();
    private readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
    private float castT = -1f, summonT = -1f, laughT = -1f, hitT = -1f;
    private float time;
    private float poseBlend;
    private Pose shown = Pose.Intro;

    public void Setup(Transform characterRoot, Transform model)
    {
        character = characterRoot;
        Transform F(string n)
        {
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == n)
                    return t;
            return null;
        }
        hips = F("Hips"); spine = F("Spine"); chest = F("Chest"); neck = F("Neck"); head = F("Head");
        upperL = F("UpperArm.L"); upperR = F("UpperArm.R"); foreL = F("Forearm.L"); foreR = F("Forearm.R");
        handL = F("Hand.L"); handR = F("Hand.R");
        thighL = F("Thigh.L"); thighR = F("Thigh.R"); shinL = F("Shin.L"); shinR = F("Shin.R");
        foreach (Transform t in new[] { hips, spine, chest, neck, head, upperL, upperR, foreL, foreR, handL, handR, thighL, thighR, shinL, shinR })
            if (t != null)
            {
                bones.Add(t);
                rest[t] = t.localRotation;
            }
    }

    public void Cast() => castT = 0f;
    public void Summon() => summonT = 0f;
    public void Laugh() => laughT = 0f;
    public void Hit() => hitT = 0f;

    // Gira un hueso alrededor de un eje del personaje (right/up/forward), en grados.
    private void Rot(Transform b, Vector3 charAxis, float deg)
    {
        if (b == null || Mathf.Abs(deg) < 0.01f)
            return;
        b.rotation = Quaternion.AngleAxis(deg, character.TransformDirection(charAxis)) * b.rotation;
    }

    private static float Ease(float k) => k * k * (3f - 2f * k);
    private static float Bump(float t, float a, float b) => t < a ? Ease(Mathf.Clamp01(t / a)) : 1f - Ease(Mathf.Clamp01((t - a) / b));

    private void LateUpdate()
    {
        if (character == null || State == Pose.Intro)
            return;

        float dt = Time.deltaTime;
        time += dt;
        if (shown != State) { shown = State; poseBlend = 0f; }
        poseBlend = Mathf.MoveTowards(poseBlend, 1f, dt * 3f);
        float pb = Ease(poseBlend);

        foreach (Transform b in bones)
            b.localRotation = rest[b];

        Vector3 R = Vector3.right, U = Vector3.up, Fw = Vector3.forward;
        float s = armRaiseSign, l = legBendSign;

        switch (State)
        {
            case Pose.Fly:
            {
                float sway = Mathf.Sin(time * 1.6f);
                // Piernas colgando que se mecen, rodillas un poco dobladas.
                Rot(thighL, R, l * (-18f + sway * 8f) * pb);
                Rot(thighR, R, l * (-14f - sway * 8f) * pb);
                Rot(shinL, R, l * (30f + sway * 6f) * pb);
                Rot(shinR, R, l * (26f - sway * 6f) * pb);
                // Brazos abiertos como dueño del lugar.
                Rot(upperL, Fw, -s * (55f + Mathf.Sin(time * 2.1f) * 6f) * pb);
                Rot(upperR, Fw, s * (55f + Mathf.Sin(time * 2.1f + 1f) * 6f) * pb);
                Rot(foreL, U, 25f * pb);
                Rot(foreR, U, -25f * pb);
                Rot(chest, R, 6f * pb);
                break;
            }
            case Pose.Stunned:
            {
                // Hincado: de rodillas (espinillas en el suelo hacia atrás), encorvado y con la cabeza
                // colgando, para que Pogoman lo alcance con el pogo. DevilBoss baja el cuerpo y el collider.
                float wob = Mathf.Sin(time * 6f);
                Rot(thighL, R, l * 12f * pb);
                Rot(thighR, R, l * 12f * pb);
                Rot(shinL, R, l * 100f * pb);
                Rot(shinR, R, l * 100f * pb);
                Rot(spine, R, 28f * pb);
                Rot(chest, R, 24f * pb);
                Rot(neck, R, 15f * pb);
                Rot(head, R, 20f * pb + wob * 5f);
                Rot(head, Fw, Mathf.Sin(time * 4f) * 10f);
                Rot(upperL, R, -40f * pb);
                Rot(upperR, R, -40f * pb);
                Rot(foreL, R, -30f * pb);
                Rot(foreR, R, -30f * pb);
                break;
            }
            case Pose.Dying:
            {
                Rot(chest, R, -25f * pb);
                Rot(head, R, -30f * pb);
                Rot(upperL, Fw, -s * (90f + Mathf.Sin(time * 14f) * 40f) * pb);
                Rot(upperR, Fw, s * (90f + Mathf.Sin(time * 13f + 2f) * 40f) * pb);
                Rot(thighL, R, l * Mathf.Sin(time * 11f) * 30f * pb);
                Rot(thighR, R, l * Mathf.Sin(time * 12f + 1f) * 30f * pb);
                break;
            }
        }

        // ---- Acciones encima de la pose ----
        if (castT >= 0f)
        {
            castT += dt;
            float k = castT / 0.65f;
            // Sube el brazo derecho atrás y lo lanza hacia adelante.
            float up = k < 0.45f ? Ease(k / 0.45f) : 1f - Ease(Mathf.Clamp01((k - 0.45f) / 0.3f));
            float throwF = k < 0.45f ? 0f : Bump(k - 0.45f, 0.12f, 0.45f);
            Rot(upperR, Fw, s * 95f * up);                 // brazo derecho sobre la cabeza
            Rot(foreR, Fw, s * 40f * up);
            Rot(upperR, R, throwSign * 85f * throwF);      // y lo baja hacia adelante
            Rot(upperR, Fw, -s * 50f * throwF);
            Rot(chest, R, 14f * throwF);
            Rot(chest, U, -15f * up + 20f * throwF);
            if (castT > 0.9f) castT = -1f;
        }
        if (summonT >= 0f)
        {
            summonT += dt;
            float k = Bump(summonT, 0.35f, 0.9f);
            Rot(upperL, Fw, -s * 120f * k);
            Rot(upperR, Fw, s * 120f * k);
            Rot(chest, R, -15f * k);
            Rot(head, R, -20f * k);
            if (summonT > 1.3f) summonT = -1f;
        }
        if (laughT >= 0f)
        {
            laughT += dt;
            float k = Bump(laughT, 0.2f, 1.3f);
            float shake = Mathf.Sin(laughT * 34f) * 5f * k;
            Rot(chest, R, -10f * k + shake);
            Rot(head, R, -22f * k + shake);
            if (laughT > 1.6f) laughT = -1f;
        }
        if (hitT >= 0f)
        {
            hitT += dt;
            float k = 1f - Mathf.Clamp01(hitT / 0.25f);
            Rot(chest, R, -12f * k);
            Rot(head, R, -15f * k);
            if (hitT > 0.25f) hitT = -1f;
        }

        // La cabeza sigue a Pogoman (un poco).
        if (LookTarget != null && head != null && State != Pose.Dying)
        {
            Vector3 to = character.InverseTransformPoint(LookTarget.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -50f, 50f);
            Rot(head, U, yaw * 0.6f);
        }
    }
}
