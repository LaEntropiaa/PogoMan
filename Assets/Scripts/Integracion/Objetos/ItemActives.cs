using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Habilidades activas de los objetos: se asignan a las teclas 1-6 en el orden en que las consigues.
// Todas funcionan igual: picas la tecla, sale el ataque y empieza la recarga.
// En el HUD (abajo a la derecha) cada una muestra su ícono, su tecla, la recarga en círculo,
// los segundos que faltan y, si aplica, sus cargas o el tiempo que le queda activa.
public class ItemActives : MonoBehaviour
{
    public static ItemActives Instance { get; private set; }
    public const int MaxSlots = 6;

    [SerializeField] private float slotSize = 76f;
    [SerializeField] private AudioClip readyClip;
    [SerializeField] private AudioClip denyClip;
    [SerializeField] private AudioClip panicClip;
    [SerializeField] private AudioClip pinataHitClip;

    private class Slot
    {
        public ItemDefinition item;
        public float cooldown, cooldownTotal = 1f;
        public float activeLeft, activeTotal;
        public RectTransform rect;
        public Image icon, ring, frame;
        public Text key, timer, label;
        public float flash;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private RectTransform bar;
    private Font font;
    private AudioSource audioSource;
    private ItemEffects fx;

    // Estado de las activas
    private float timbreTime;
    private float timbreSlamAt = -1f;

    public bool SlotsFull => slots.Count >= MaxSlots;

    private void Awake()
    {
        Instance = this;
        audioSource = UiKit.UiAudio(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        fx = ItemEffects.Instance;
        font = fx != null ? fx.Font : null;
        Canvas canvas = UiKit.MakeCanvas("HUDActivas", 5);
        canvas.transform.SetParent(transform, false);
        bar = UiKit.Rect("Activas", canvas.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 110f), new Vector2(600f, 110f));
    }

    // ---- Alta de una activa nueva ----
    public void Register(ItemDefinition item)
    {
        foreach (Slot s in slots) if (s.item == item) return;
        if (SlotsFull) return;
        var slot = new Slot { item = item };
        slots.Add(slot);
        BuildSlot(slot, slots.Count);
        Layout();
        slot.flash = 1f;
    }

    private void BuildSlot(Slot s, int index)
    {
        s.rect = UiKit.Rect(item(s).id, bar, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.one * slotSize);
        s.frame = UiKit.Image(s.rect, new Color(0.08f, 0.03f, 0.06f, 0.85f));
        var border = s.rect.gameObject.AddComponent<Outline>();
        border.effectColor = item(s).RarityColor;
        border.effectDistance = new Vector2(3, -3);
        s.icon = UiKit.Image(UiKit.Stretch("Icono", s.rect, 6f), Color.white, item(s).icon);
        s.icon.preserveAspect = true;
        // Sombra de recarga que se va destapando en círculo.
        s.ring = UiKit.Image(UiKit.Stretch("Recarga", s.rect, 0f), new Color(0f, 0f, 0f, 0.72f));
        s.ring.type = Image.Type.Filled;
        s.ring.fillMethod = Image.FillMethod.Radial360;
        s.ring.fillOrigin = (int)Image.Origin360.Top;
        s.ring.fillClockwise = false;
        s.timer = UiKit.Text(UiKit.Stretch("Tiempo", s.rect), font, 30, Color.white, TextAnchor.MiddleCenter);
        s.timer.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
        s.key = UiKit.Text(UiKit.Rect("Tecla", s.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -2f), new Vector2(30f, 28f)),
            font, 24, new Color(1f, 0.9f, 0.6f), TextAnchor.UpperLeft, index.ToString());
        s.key.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
        s.label = UiKit.Text(UiKit.Rect("Etiqueta", s.rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(160f, 26f)),
            font, 20, new Color(1f, 0.85f, 0.5f), TextAnchor.UpperCenter);
        s.label.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
    }

    private static ItemDefinition item(Slot s) => s.item;

    private void Layout()
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].rect.anchoredPosition = new Vector2(-(slots.Count - 1 - i) * (slotSize + 12f), 0f);
    }

    // ---- Teclas ----
    private static Key KeyFor(int i) => i switch { 0 => Key.Digit1, 1 => Key.Digit2, 2 => Key.Digit3, 3 => Key.Digit4, 4 => Key.Digit5, _ => Key.Digit6 };

    private void Update()
    {
        if (fx == null) fx = ItemEffects.Instance;
        float dt = Time.deltaTime;
        bool paused = Time.timeScale <= 0f || ChestUI.IsOpen || MapScreen.IsOpen || RecordNamePrompt.IsOpen;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            if (!TimeStop.Active)
            {
                bool wasCooling = s.cooldown > 0f;
                s.cooldown = Mathf.Max(0f, s.cooldown - dt);
                s.activeLeft = Mathf.Max(0f, s.activeLeft - dt);
                if (wasCooling && s.cooldown <= 0f) { s.flash = 1f; Play(readyClip, 1.2f, 0.5f); }
            }

            if (!paused && Keyboard.current != null)
            {
                var k = Keyboard.current[KeyFor(i)];
                if (k.wasPressedThisFrame) Press(s);
            }
        }

        UpdateTimbre();
        RefreshHud(dt);
    }

    private void Deny(Slot s)
    {
        s.flash = -1f;
        Play(denyClip, 1f, 0.6f);
    }

    private void Press(Slot s)
    {
        if (s.cooldown > 0f || fx == null || fx.Pogo == null || PogoBridge.IsDead(fx.Pogo)) { Deny(s); return; }
        switch (s.item.id)
        {
            case ItemEffects.Panico: Panic(s); break;
            case ItemEffects.Detonador: ThrowBomb(s); break;
            case ItemEffects.Matraca: ChargedSpit(s); break;
            case ItemEffects.Timbre: StartTimbre(s); break;
            case ItemEffects.Pinata: ThrowPinata(s); break;
        }
    }

    // Hacia dónde apunta la cámara (sin inclinación, para lanzar en arco).
    private Vector3 AimFlat()
    {
        Vector3 f = Camera.main != null ? Camera.main.transform.forward : fx.Player.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
    }

    private Vector3 Hand => fx.Player.position + AimFlat() * 0.8f + Vector3.up * 0.6f;

    // Demonio más cercano enfrente (cono de ~70°), para que lo que avientas le caiga encima.
    private EnemyTracker AimTarget(float range)
    {
        Vector3 from = fx.Player.position, fwd = AimFlat();
        EnemyTracker best = null;
        float bestD = range;
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || !e.Targetable) continue;
            Vector3 d = e.Center - from; d.y = 0f;
            float dist = d.magnitude;
            if (dist < bestD && dist > 0.5f && Vector3.Angle(fwd, d) < 35f) { best = e; bestD = dist; }
        }
        return best;
    }

    // Velocidad para caer en "to" con esa gravedad (si no hay demonio, cae a "fallback" metros al frente).
    private Vector3 ArcTo(Vector3 from, float gravity, float fallback)
    {
        EnemyTracker t = AimTarget(16f);
        Vector3 to = t != null ? t.Center : ItemEffects.GroundAt(fx.Player.position + AimFlat() * fallback);
        Vector3 d = to - from;
        float time = Mathf.Clamp(new Vector3(d.x, 0f, d.z).magnitude / 12f, 0.35f, 0.9f);
        return d / time + Vector3.up * 0.5f * gravity * time;
    }

    // ---- Botón de Pánico del Oxxo: onda que empuja y daña alrededor ----
    private void Panic(Slot s)
    {
        Vector3 c = fx.Player.position;
        float dmg = fx.PogoDamage * 0.6f;
        foreach (EnemyTracker e in new List<EnemyTracker>(EnemyTracker.All))
        {
            if (e == null || e.IsDead) continue;
            Vector3 d = e.Center - c; d.y = 0f;
            if (d.magnitude > 4.5f) continue;
            e.Hit(dmg);
            bool boss = e.IsBoss || e.GetComponent<Guardian>() != null || e.GetComponent<DevilBoss>() != null;
            var body = e.GetComponent<Rigidbody>();
            if (!boss && body != null && !body.isKinematic)
                body.linearVelocity = (d.sqrMagnitude > 0.01f ? d.normalized : Random.insideUnitSphere) * 12f + Vector3.up * 5f;
        }
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            ItemFx.Burst(c + new Vector3(Mathf.Cos(a), 0.4f, Mathf.Sin(a)) * 2.5f, 8, 0.3f, new Color(1f, 0.25f, 0.2f), fx.FxMaterial, 8f, 0.4f, 0.4f);
        }
        Play(panicClip, 1f, 1f);
        Cool(s, 14f);
    }

    // ---- Detonador del Tianguis: bomba en arco que explota al tocar algo ----
    private void ThrowBomb(Slot s)
    {
        var shot = ActiveShot.Create("Bomba", Hand, ArcTo(Hand, 22f, 9f), 22f, 2.5f, fx.FxMaterial);
        var ball = shot.AddPart(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.55f, new Color(0.12f, 0.1f, 0.1f), 0.1f);
        shot.AddPart(PrimitiveType.Cylinder, Vector3.up * 0.32f, new Vector3(0.08f, 0.12f, 0.08f), new Color(0.6f, 0.5f, 0.3f), 0.2f);
        shot.Blink = shot.AddLight(new Color(1f, 0.4f, 0.1f), 3f);
        shot.Spin = new Vector3(0f, 0f, 360f);
        shot.Exploded = at => fx.Explode(at, 3.5f, fx.PogoDamage * 1.5f, new Color(1f, 0.55f, 0.1f));
        Play(panicClip, 1.6f, 0.5f);
        Cool(s, 8f);
    }

    // ---- Matraca del Cobrador: escupitajo cargado que atraviesa y revienta al final ----
    private void ChargedSpit(Slot s)
    {
        PlayerStats stats = PlayerStats.Instance;
        float spit = stats != null ? stats.Get(PogoStat.SpitDamage) : 29f;
        EnemyTracker target = AimTarget(25f);
        Vector3 dir = target != null ? target.Center - Hand : (Camera.main != null ? Camera.main.transform.forward : fx.Player.forward);
        if (target == null) dir.y = Mathf.Max(dir.y, -0.15f);
        var shot = ActiveShot.Create("EscupitajoCargado", Hand, dir.normalized * 26f, 0f, 1.1f, fx.FxMaterial);
        shot.AddPart(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.9f, new Color(0.55f, 1f, 0.3f), 1.2f);
        shot.AddLight(new Color(0.5f, 1f, 0.3f), 4f);
        ItemFx.Trail(shot.gameObject, new Color(0.6f, 1f, 0.3f), 0.8f, 0.3f, fx.FxMaterial);
        shot.PierceDamage = spit * 1.5f;
        shot.HitRadius = 1.4f;
        shot.Exploded = at => fx.Explode(at, 3f, spit * 2.5f, new Color(0.5f, 1f, 0.3f));
        Play(panicClip, 2f, 0.5f);
        Cool(s, 9f);
    }

    // ---- Timbre de la Micro: brinca (si estás en el piso) y cae en picada explotando ----
    private void StartTimbre(Slot s)
    {
        var rb = fx.Pogo.GetComponent<Rigidbody>();
        Vector3 v = rb.linearVelocity;
        if (PogoBridge.IsGrounded(fx.Pogo))
        {
            rb.linearVelocity = new Vector3(v.x * 0.5f, 13f, v.z * 0.5f);
            timbreSlamAt = Time.time + 0.4f;   // en lo alto del brinco, se deja caer
        }
        else
            Slam();
        fx.TimbreSlam = true;
        timbreTime = Time.time;
        Cool(s, 12f);
    }

    private void Slam()
    {
        timbreSlamAt = -1f;
        var rb = fx.Pogo.GetComponent<Rigidbody>();
        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = new Vector3(v.x * 0.3f, -30f, v.z * 0.3f);
        ItemFx.Burst(fx.Player.position, 20, 0.5f, new Color(1f, 0.9f, 0.4f), fx.FxMaterial, 4f, 0.3f, 0.4f);
    }

    private void UpdateTimbre()
    {
        if (timbreSlamAt > 0f && Time.time >= timbreSlamAt && fx != null && fx.Pogo != null)
            Slam();
        if (fx != null && fx.TimbreSlam && Time.time - timbreTime > 3f)
            fx.TimbreSlam = false;   // por si algo raro pasa, que no se quede sin pisotón
    }

    public void TimbreLanded(Vector3 at)
    {
        if (timbreSlamAt > 0f) return;   // todavía va subiendo
        fx.TimbreSlam = false;
        int hits = fx.Explode(at, 4.5f, fx.PogoDamage * 2f, new Color(1f, 0.85f, 0.3f));
        if (hits > 0)
        {
            var rb = fx.Pogo.GetComponent<Rigidbody>();
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(v.x, 16f, v.z);   // rebota ~6 m
        }
    }

    // ---- Piñata del Último Rezo: la avientas y revienta en dulces al caer ----
    private void ThrowPinata(Slot s)
    {
        Vector3 from = Hand + Vector3.up * 0.4f;
        var shot = ActiveShot.Create("Pinata", from, ArcTo(from, 20f, 7f), 20f, 3f, fx.FxMaterial);
        Color[] colors = { new Color(1f, 0.2f, 0.5f), new Color(1f, 0.85f, 0.1f), new Color(0.2f, 0.8f, 1f), new Color(0.4f, 1f, 0.3f), new Color(1f, 0.5f, 0.1f) };
        shot.AddPart(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.9f, colors[0], 0.4f);
        for (int i = 0; i < 6; i++)
        {
            Vector3 d = i < 4 ? Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward : (i == 4 ? Vector3.up : Vector3.down);
            var spike = shot.AddPart(PrimitiveType.Cylinder, d * 0.6f, new Vector3(0.2f, 0.32f, 0.2f), colors[1 + i % 4], 0.4f);
            spike.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        }
        shot.Spin = new Vector3(90f, 300f, 0f);
        shot.Exploded = at =>
        {
            fx.Explode(at, 5f, fx.PogoDamage * 3f, new Color(1f, 0.4f, 0.8f));
            for (int i = 0; i < 4; i++)
                ItemFx.Burst(at + Vector3.up * 0.5f, 30, 0.5f, Color.HSVToRGB(Random.value, 0.8f, 1f), fx.FxMaterial, 10f, 0.3f, 1f);
            if (pinataHitClip != null) AudioSource.PlayClipAtPoint(pinataHitClip, at, GameSettings.SfxVolume);
        };
        Cool(s, 18f);
    }

    // ---- utilidades ----
    private void Cool(Slot s, float seconds)
    {
        s.cooldown = seconds;
        s.cooldownTotal = seconds;
    }

    private void Play(AudioClip clip, float pitch, float vol)
    {
        if (clip == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, vol * GameSettings.SfxVolume);
    }

    private void RefreshHud(float dt)
    {
        foreach (Slot s in slots)
        {
            float k = s.cooldownTotal > 0f ? s.cooldown / s.cooldownTotal : 0f;
            s.ring.fillAmount = k;
            s.timer.text = s.cooldown > 0f ? (s.cooldown >= 10f ? Mathf.CeilToInt(s.cooldown).ToString() : s.cooldown.ToString("0.0")) : "";
            s.label.text = "";
            // Destello al estar lista (verde) o al negarse (rojo).
            if (s.flash > 0f) s.flash = Mathf.MoveTowards(s.flash, 0f, dt * 2f);
            else if (s.flash < 0f) s.flash = Mathf.MoveTowards(s.flash, 0f, dt * 3f);
            Color baseC = new Color(0.08f, 0.03f, 0.06f, 0.85f);
            s.frame.color = s.flash > 0f ? Color.Lerp(baseC, new Color(0.5f, 1f, 0.5f, 0.9f), s.flash)
                : s.flash < 0f ? Color.Lerp(baseC, new Color(1f, 0.2f, 0.2f, 0.9f), -s.flash) : baseC;
            s.rect.localScale = Vector3.one * (1f + Mathf.Abs(s.flash) * 0.12f);
        }
    }
}

// Proyectil de las activas: vuela (con o sin gravedad), explota al chocar con el mapa, con un demonio
// o al acabarse su tiempo. Si tiene PierceDamage, atraviesa demonios haciéndoles daño una vez a cada uno.
public class ActiveShot : MonoBehaviour
{
    public System.Action<Vector3> Exploded;
    public float PierceDamage;
    public float HitRadius = 0.8f;
    public Vector3 Spin;
    public Light Blink;

    private Vector3 velocity;
    private float gravity, life;
    private Material fx;
    private readonly HashSet<EnemyTracker> pierced = new HashSet<EnemyTracker>();

    public static ActiveShot Create(string name, Vector3 from, Vector3 velocity, float gravity, float life, Material fx)
    {
        var go = new GameObject(name);
        go.transform.position = from;
        var s = go.AddComponent<ActiveShot>();
        s.velocity = velocity; s.gravity = gravity; s.life = life; s.fx = fx;
        return s;
    }

    public Transform AddPart(PrimitiveType type, Vector3 pos, Vector3 scale, Color color, float glow)
    {
        var p = GameObject.CreatePrimitive(type);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(transform, false);
        p.transform.localPosition = pos;
        p.transform.localScale = scale;
        ItemFx.Paint(p, color, glow);
        return p.transform;
    }

    public Light AddLight(Color c, float range)
    {
        var l = gameObject.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 2.5f; l.shadows = LightShadows.None;
        return l;
    }

    private void Update()
    {
        if (TimeStop.Active) return;
        float dt = Time.deltaTime;
        life -= dt;
        velocity += Vector3.down * gravity * dt;
        Vector3 next = transform.position + velocity * dt;

        // El mapa (sin cuerpo físico: así no choca con Pogoman ni con los demonios).
        if (Physics.Linecast(transform.position, next, out RaycastHit h, ~0, QueryTriggerInteraction.Ignore) && h.rigidbody == null)
        {
            Boom(h.point + h.normal * 0.2f);
            return;
        }
        transform.position = next;
        transform.Rotate(Spin * dt, Space.Self);
        if (Blink != null) Blink.intensity = Mathf.Sin(Time.time * 25f) > 0f ? 4f : 0.5f;
        if (Random.value < 0.5f)
            ItemFx.Burst(next, 2, 0.05f, new Color(1f, 0.8f, 0.4f), fx, 1.5f, 0.1f, 0.2f);

        float r2 = HitRadius * HitRadius;
        foreach (EnemyTracker e in new List<EnemyTracker>(EnemyTracker.All))
        {
            if (e == null || e.IsDead || !e.Targetable || (e.Center - next).sqrMagnitude > r2) continue;
            if (PierceDamage > 0f)
            {
                if (pierced.Add(e)) e.Hit(PierceDamage);
            }
            else
            {
                Boom(next);
                return;
            }
        }
        if (life <= 0f)
            Boom(next);
    }

    private void Boom(Vector3 at)
    {
        Exploded?.Invoke(at);
        Destroy(gameObject);
    }
}
