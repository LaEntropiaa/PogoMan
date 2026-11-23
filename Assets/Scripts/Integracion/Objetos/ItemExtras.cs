using System.Collections.Generic;
using UnityEngine;

// Piezas pequeñas de los objetos nuevos: ánima, rebote del escupitajo, chiflador, ficha y señuelo.

// Campanita del Animero: ánima que ataca una vez por segundo durante 5 s (máx. 2 a la vez).
public class SpiritCompanion : MonoBehaviour
{
    private static readonly List<SpiritCompanion> alive = new List<SpiritCompanion>();
    private ItemEffects fx;
    private float life = 5f, timer = 0.3f, phase;
    private Vector3 vel;

    public static void Summon(ItemEffects fx, Vector3 at)
    {
        alive.RemoveAll(s => s == null);
        if (alive.Count >= 2) { Destroy(alive[0].gameObject); alive.RemoveAt(0); }   // se renueva la más antigua
        var go = new GameObject("Anima");
        go.transform.position = at + Vector3.up;
        var s = go.AddComponent<SpiritCompanion>();
        s.fx = fx;
        s.phase = Random.value * 6f;
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f);
        var r = body.GetComponent<Renderer>();
        r.sharedMaterial = GuardianKit.Instance != null ? GuardianKit.Instance.SoulMaterial : fx.FairyMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ItemFx.Trail(go, new Color(0.5f, 1f, 0.9f), 0.25f, 0.4f, fx.FxMaterial);
        ItemFx.Burst(go.transform.position, 18, 0.4f, new Color(0.5f, 1f, 0.9f), fx.FxMaterial, 3f, 0.3f, 0.6f);
        alive.Add(s);
    }

    private void Update()
    {
        if (TimeStop.Active) return;
        life -= Time.deltaTime;
        if (life <= 0f || fx == null || fx.Player == null) { Destroy(gameObject); return; }
        phase += Time.deltaTime;
        Vector3 target = fx.Player.position + new Vector3(Mathf.Cos(phase * 2f) * 1.6f, 2.2f + Mathf.Sin(phase * 3f) * 0.2f, Mathf.Sin(phase * 2f) * 1.6f);
        transform.position = Vector3.SmoothDamp(transform.position, target, ref vel, 0.2f);
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = 1f;
        EnemyTracker e = ItemEffects.Nearest(transform.position, 12f);
        if (e != null)
            FairyBolt.Fire(transform.position, e, fx.PogoDamage * 0.3f, fx.FxMaterial);
    }

    private void OnDestroy() => alive.Remove(this);
}

// Chiflador de Banqueta: cohete que vuela al frente y explota al tocar a alguien o a los 12 m.
public class Rocket : MonoBehaviour
{
    private Vector3 dir;
    private float damage, travelled;
    private Material fx;
    private AudioClip clip;

    public static void Fire(Vector3 from, Vector3 dir, float damage, Material fx, AudioClip clip)
    {
        var go = new GameObject("Chiflador");
        go.transform.position = from;
        var r = go.AddComponent<Rocket>();
        r.dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        r.damage = damage; r.fx = fx; r.clip = clip;
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
        body.transform.localRotation = Quaternion.LookRotation(r.dir) * Quaternion.Euler(90f, 0f, 0f);
        ItemFx.Paint(body, new Color(0.9f, 0.15f, 0.1f), 1f);
        ItemFx.Trail(go, new Color(1f, 0.8f, 0.3f), 0.3f, 0.35f, fx);
    }

    private void Update()
    {
        if (TimeStop.Active) return;
        float step = 24f * Time.deltaTime;
        Vector3 next = transform.position + dir * step;
        travelled += step;
        bool hitWall = Physics.Linecast(transform.position, next, out RaycastHit h, ~0, QueryTriggerInteraction.Ignore) && h.rigidbody == null;
        bool hitEnemy = false;
        foreach (EnemyTracker e in EnemyTracker.All)
            if (e != null && !e.IsDead && (e.Center - next).sqrMagnitude < 1.1f * 1.1f) { hitEnemy = true; break; }
        transform.position = next;
        if (Random.value < 0.4f)
            ItemFx.Burst(transform.position, 3, 0.05f, new Color(1f, 0.7f, 0.2f), fx, 2f, 0.12f, 0.25f);
        if (hitWall || hitEnemy || travelled >= 12f)
        {
            ItemEffects.Instance?.Explode(hitWall ? h.point : next, 2.5f, damage, new Color(1f, 0.55f, 0.15f), clip);
            Destroy(gameObject);
        }
    }
}

// Ficha del Futbolito: cae en línea recta hasta 10 m y golpea a los demonios que estén debajo.
public class TokenDrop : MonoBehaviour
{
    private float damage, fallen;
    private Material fx;
    private readonly HashSet<EnemyTracker> hit = new HashSet<EnemyTracker>();

    public static void Fire(Vector3 from, float damage, Material coinMat, Material fx)
    {
        var go = new GameObject("Ficha");
        go.transform.position = from;
        var t = go.AddComponent<TokenDrop>();
        t.damage = damage; t.fx = fx;
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.45f, 0.04f, 0.45f);
        var r = body.GetComponent<Renderer>();
        if (coinMat != null) r.sharedMaterial = coinMat;
        ItemFx.Trail(go, new Color(1f, 0.85f, 0.4f), 0.3f, 0.2f, fx);
    }

    private void Update()
    {
        float step = 30f * Time.deltaTime;
        Vector3 next = transform.position + Vector3.down * step;
        fallen += step;
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || hit.Contains(e)) continue;
            Vector3 d = e.Center - next;
            if (new Vector2(d.x, d.z).magnitude < 1.4f && Mathf.Abs(d.y) < 1.4f)
            {
                hit.Add(e);
                e.Hit(damage);
                ItemFx.Burst(e.Center, 12, 0.3f, new Color(1f, 0.85f, 0.4f), fx, 4f, 0.2f, 0.4f);
            }
        }
        bool ground = Physics.Raycast(transform.position, Vector3.down, out RaycastHit h, step + 0.1f, ~0, QueryTriggerInteraction.Ignore) && h.rigidbody == null;
        transform.position = next;
        transform.Rotate(0f, 900f * Time.deltaTime, 0f);
        if (ground || fallen >= 10f)
        {
            ItemFx.Burst(transform.position, 16, 0.4f, new Color(1f, 0.85f, 0.4f), fx, 4f, 0.25f, 0.4f);
            Destroy(gameObject);
        }
    }
}

// Seguro de Daños a Terceros: un doble que atrae a los demonios 2 s y explota.
public class Decoy : MonoBehaviour
{
    private float damage, age;
    private Material fx;
    private AudioClip clip;
    private readonly Dictionary<Enemy, Transform> attracted = new Dictionary<Enemy, Transform>();

    public static void Spawn(Vector3 at, float damage, Material fx, AudioClip clip)
    {
        var go = new GameObject("Senuelo");
        go.transform.position = at;
        var d = go.AddComponent<Decoy>();
        d.damage = damage; d.fx = fx; d.clip = clip;
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = Vector3.up * 1f;
        body.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
        ItemFx.Paint(body, new Color(1f, 0.85f, 0.3f), 0.8f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.3f); l.range = 6f; l.intensity = 2.5f; l.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (TimeStop.Active) return;
        age += Time.deltaTime;
        // Atrae a los demonios en 6 m (los jefes no se dejan engañar).
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || e.IsBoss || e.GetComponent<Guardian>() != null || attracted.ContainsKey(e.Enemy)) continue;
            if ((e.Center - transform.position).sqrMagnitude > 36f) continue;
            attracted[e.Enemy] = PogoBridge.Get<Transform>(e.Enemy, "target");
            e.Enemy.SetTarget(transform);
        }
        transform.localScale = Vector3.one * (1f + Mathf.Sin(age * 20f) * 0.05f * age);
        if (age >= 2f)
        {
            ItemEffects.Instance?.Explode(transform.position + Vector3.up, 3.5f, damage, new Color(1f, 0.75f, 0.25f), clip);
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        foreach (var pair in attracted)
            if (pair.Key != null)
                pair.Key.SetTarget(pair.Value);
    }
}
