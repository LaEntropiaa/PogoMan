using UnityEngine;

// Lengua de Pogoman (huesos Tongue.01..03 del modelo animado): se le sale y se zarandea con cada
// rebote como resorte, cuelga hacia atrás yendo rápido y da un lengüetazo al escupir.
// Va encima de la animación del Animator (LateUpdate, después de que el Animator escribe la pose).
[DefaultExecutionOrder(100)]
public class PogoTongue : MonoBehaviour
{
    [Tooltip("Signo del giro para sacar la lengua (si sale hacia adentro, cambia a -1).")]
    [SerializeField] private float outSign = 1f;
    [SerializeField] private float stiffness = 140f;
    [SerializeField] private float damping = 9f;
    [SerializeField] private float maxAngle = 55f;

    private Transform[] bones;
    private Vector3[] baseScale;
    private Transform root;
    private Rigidbody body;
    private float angle, angVel;      // flexión arriba/abajo (resorte)
    private float side, sideVel;      // de lado
    private float lick;               // lengüetazo al escupir (0..1)
    private Vector3 lastVel;
    private float t;

    public static PogoTongue Attach(Transform visual, Rigidbody body)
    {
        var tongue = visual.GetComponent<PogoTongue>();
        if (tongue == null) tongue = visual.gameObject.AddComponent<PogoTongue>();
        tongue.body = body;
        return tongue;
    }

    private void Start()
    {
        root = transform;
        bones = new Transform[3];
        foreach (Transform c in GetComponentsInChildren<Transform>(true))
        {
            if (c.name == "Tongue.01") bones[0] = c;
            else if (c.name == "Tongue.02") bones[1] = c;
            else if (c.name == "Tongue.03") bones[2] = c;
        }
        baseScale = new Vector3[3];
        for (int i = 0; i < 3; i++)
            if (bones[i] != null) baseScale[i] = bones[i].localScale;
        SpitAttack.VolleyFired += OnSpit;
        if (body != null) lastVel = body.linearVelocity;
    }

    private void OnDestroy() => SpitAttack.VolleyFired -= OnSpit;

    private void OnSpit(SpitProjectile _) => lick = 1f;

    private void LateUpdate()
    {
        if (bones == null || bones[0] == null || body == null)
            return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        t += dt;

        // Aceleración que siente la cabeza: al rebotar (sube de golpe) la lengua se va para abajo y luego rebota.
        Vector3 v = body.linearVelocity;
        Vector3 acc = (v - lastVel) / dt;
        lastVel = v;
        Vector3 localAcc = root.InverseTransformDirection(acc);
        float speed = new Vector2(v.x, v.z).magnitude;

        // Resortes: el objetivo es "colgando" hacia atrás según la velocidad; la aceleración la empuja.
        float target = Mathf.Clamp(speed * 1.2f, 0f, 22f);
        angVel += ((target - angle) * stiffness - angVel * damping - localAcc.y * 0.9f) * dt;
        angle = Mathf.Clamp(angle + angVel * dt, -maxAngle * 0.5f, maxAngle);
        sideVel += ((-side) * stiffness * 0.8f - sideVel * damping + localAcc.x * 0.8f) * dt;
        side = Mathf.Clamp(side + sideVel * dt, -35f, 35f);

        lick = Mathf.MoveTowards(lick, 0f, dt * 5f);
        float licking = Mathf.Sin(lick * Mathf.PI);   // sale y regresa
        float wag = Mathf.Sin(t * 16f) * 4f * Mathf.Clamp01(speed / 8f);

        for (int i = 0; i < 3; i++)
        {
            Transform b = bones[i];
            if (b == null) continue;
            float w = (i + 1) / 3f;   // la punta se mueve más
            float pitch = (angle * w * 0.6f - licking * 25f) * outSign;
            b.rotation = Quaternion.AngleAxis(pitch, root.right) * Quaternion.AngleAxis(side * w * 0.6f + wag * w, root.up) * b.rotation;
            // Lengüetazo: se estira (más la base, para que salga de la boca).
            float stretch = 1f + licking * (i == 0 ? 0.6f : 0.25f);
            b.localScale = new Vector3(baseScale[i].x, baseScale[i].y * stretch, baseScale[i].z);
        }
    }
}
