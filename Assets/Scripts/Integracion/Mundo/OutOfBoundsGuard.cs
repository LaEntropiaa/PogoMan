using System.Collections.Generic;
using UnityEngine;

// Red de seguridad: si Pogoman se sale del mapa (cae por un hoyo de la malla, atraviesa una pared
// con el dash...), lo regresa al último piso firme donde estuvo. En la arena del Diablo, a su entrada.
[DefaultExecutionOrder(300)]
public class OutOfBoundsGuard : MonoBehaviour
{
    [Tooltip("Metros por debajo del piso firme más bajo que pisaste para considerar que caíste al limbo.")]
    [SerializeField] private float fallMargin = 30f;
    [Tooltip("Segundos cayendo sin nada debajo para regresarte.")]
    [SerializeField] private float voidTime = 1.5f;

    private PogoPlayer player;
    private Rigidbody rb;
    private readonly Queue<Vector3> safe = new Queue<Vector3>();
    private Vector3 lastSafe;
    private bool hasSafe;
    private float lowestSafeY = float.MaxValue;
    private float sampleTimer, voidTimer;
    private bool wasInArena;

    public static int Rescues { get; private set; }

    private void Awake()
    {
        player = GetComponent<PogoPlayer>();
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (player == null || PogoBridge.IsDead(player))
            return;

        // Al entrar o salir de la arena, lo guardado ya no sirve.
        if (ArenaFight.InArena != wasInArena)
        {
            wasInArena = ArenaFight.InArena;
            safe.Clear();
            hasSafe = false;
            lowestSafeY = float.MaxValue;
        }

        float dt = Time.fixedDeltaTime;
        sampleTimer -= dt;
        if (sampleTimer <= 0f && PogoBridge.IsGrounded(player) && FirmGround(out Vector3 ground))
        {
            sampleTimer = 0.25f;
            safe.Enqueue(ground);
            while (safe.Count > 6) safe.Dequeue();   // ~1.5 s atrás: no justo en la orilla
            lastSafe = safe.Peek();
            hasSafe = true;
            lowestSafeY = Mathf.Min(lowestSafeY, ground.y);
        }

        bool nothingBelow = rb.linearVelocity.y < -2f &&
            !Physics.Raycast(transform.position, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        voidTimer = nothingBelow ? voidTimer + dt : 0f;

        bool fell = hasSafe && transform.position.y < lowestSafeY - fallMargin;
        bool arenaOut = ArenaFight.InArena && ArenaFight.Instance != null &&
            (transform.position.y < ArenaFight.Instance.PlayerStart.y - 20f ||
             Vector3.Distance(Flat(transform.position), Flat(ArenaFight.Instance.PlayerStart)) > 120f);

        if (fell || arenaOut || voidTimer > voidTime)
            Rescue();
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // Piso del mapa (sin rigidbody), que no sea lava.
    private bool FirmGround(out Vector3 point)
    {
        point = default;
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        if (hit.rigidbody != null || hit.collider.GetComponent<LavaHazard>() != null || hit.normal.y < 0.7f)
            return false;
        point = hit.point;
        return true;
    }

    private void Rescue()
    {
        Vector3 to;
        if (ArenaFight.InArena && ArenaFight.Instance != null)
            to = ArenaFight.Instance.PlayerStart + Vector3.up * 1.5f;
        else if (hasSafe)
            to = lastSafe + Vector3.up * 1.6f;
        else
            return;

        Rescues++;
        voidTimer = 0f;
        rb.linearVelocity = Vector3.zero;
        rb.position = to;
        transform.position = to;
        PogoCamera cam = FindAnyObjectByType<PogoCamera>();
        if (cam != null) PogoBridge.Set(cam, "currentPosition", to - transform.forward * 6f + Vector3.up * 3f);
        if (PlayerVitals.Instance != null)
            PlayerVitals.Instance.InvulnerableUntil = Mathf.Max(PlayerVitals.Instance.InvulnerableUntil, Time.time + 1.5f);
        Material fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        ItemFx.Burst(to, 40, 0.8f, new Color(0.7f, 0.3f, 1f), fx, 6f, 0.3f, 0.6f);
        Debug.Log("OutOfBoundsGuard: Pogoman se salió del mapa, regresado a " + to);
    }
}
