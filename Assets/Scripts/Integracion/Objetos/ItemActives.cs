using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Habilidades activas de los objetos: se asignan a las teclas 1-6 en el orden en que las consigues.
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
    private readonly List<DetonatorCharge> charges = new List<DetonatorCharge>();
    private Slot matracaSlot;
    private float matracaHold;
    private float timbreTime;
    private Pinata pinata;

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
                if (k.wasReleasedThisFrame) Release(s);
            }
        }

        UpdateMatraca(dt);
        UpdateTimbre();
        charges.RemoveAll(c => c == null);
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
            case ItemEffects.Detonador: Detonate(s); break;
            case ItemEffects.Matraca: StartMatraca(s); break;
            case ItemEffects.Timbre: StartTimbre(s); break;
            case ItemEffects.Pinata: SpawnPinata(s); break;
        }
    }

    private void Release(Slot s)
    {
        if (s == matracaSlot) ReleaseMatraca();
    }

    // ---- Botón de Pánico del Oxxo: empuja y daña alrededor ----
    private void Panic(Slot s)
    {
        Vector3 c = fx.Player.position;
        foreach (EnemyTracker e in new List<EnemyTracker>(EnemyTracker.All))
        {
            if (e == null || e.IsDead) continue;
            Vector3 d = e.Center - c; d.y = 0f;
            if (d.magnitude > 4f) continue;
            e.Hit(25f);
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
        Cool(s, 16f);
    }

    // ---- Detonador del Tianguis ----
    public void PlantCharge(EnemyTracker e)
    {
        if (e == null) return;
        charges.RemoveAll(c => c == null);
        if (charges.Count >= 3) return;
        foreach (DetonatorCharge c in charges) if (c.Target == e) return;
        charges.Add(DetonatorCharge.Plant(e, fx.FxMaterial));
    }

    private void Detonate(Slot s)
    {
        charges.RemoveAll(c => c == null);
        if (charges.Count == 0) { Deny(s); return; }
        var hitCount = new Dictionary<EnemyTracker, int>();
        float dmg = fx.PogoDamage * 1.2f;
        foreach (DetonatorCharge c in charges)
        {
            Vector3 at = c.transform.position;
            foreach (EnemyTracker e in new List<EnemyTracker>(EnemyTracker.All))
            {
                if (e == null || e.IsDead || (e.Center - at).sqrMagnitude > 9f) continue;
                hitCount.TryGetValue(e, out int n);
                e.Hit(n == 0 ? dmg : dmg * 0.35f);   // explosiones extra en el mismo blanco al 35 %
                hitCount[e] = n + 1;
            }
            ItemFx.Burst(at, 40, 1.2f, new Color(1f, 0.6f, 0.1f), fx.FxMaterial, 9f, 0.5f, 0.6f);
            Destroy(c.gameObject);
        }
        charges.Clear();
        fx.Play(fx.ExplodeClip, 0.9f, 1f);
        Cool(s, 6f);
    }

    // ---- Matraca del Cobrador: guarda salvas y las suelta juntas ----
    private void StartMatraca(Slot s)
    {
        if (SpitAttack.Instance == null) { Deny(s); return; }
        matracaSlot = s;
        matracaHold = 3f;
        s.activeTotal = 3f;
        s.activeLeft = 3f;
        SpitAttack.Instance.StoredVolleys = 0;
        SpitAttack.Instance.Holding = true;
    }

    private void UpdateMatraca(float dt)
    {
        if (matracaSlot == null) return;
        matracaHold -= dt;
        if (matracaHold <= 0f) ReleaseMatraca();
    }

    private void ReleaseMatraca()
    {
        if (matracaSlot == null || SpitAttack.Instance == null) return;
        Slot s = matracaSlot;
        matracaSlot = null;
        SpitAttack spit = SpitAttack.Instance;
        int volleys = spit.StoredVolleys;
        spit.Holding = false;
        spit.StoredVolleys = 0;
        s.activeLeft = 0f;
        Camera cam = Camera.main;
        if (volleys > 0)
            spit.Burst(volleys, cam != null ? cam.transform.forward : fx.Player.forward);
        Cool(s, 10f);
    }

    // ---- Timbre de la Micro: caída en picada que explota ----
    private void StartTimbre(Slot s)
    {
        if (PogoBridge.IsGrounded(fx.Pogo)) { Deny(s); return; }   // solo en el aire
        var rb = fx.Pogo.GetComponent<Rigidbody>();
        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = new Vector3(v.x * 0.3f, -30f, v.z * 0.3f);
        fx.TimbreSlam = true;
        timbreTime = Time.time;
        ItemFx.Burst(fx.Player.position, 20, 0.5f, new Color(1f, 0.9f, 0.4f), fx.FxMaterial, 4f, 0.3f, 0.4f);
        Cool(s, 14f);
    }

    private void UpdateTimbre()
    {
        if (fx != null && fx.TimbreSlam && Time.time - timbreTime > 3f)
            fx.TimbreSlam = false;   // por si algo raro pasa, que no se quede sin pisotón
    }

    public void TimbreLanded(Vector3 at)
    {
        fx.TimbreSlam = false;
        int hits = fx.Explode(at, 4f, fx.PogoDamage * 2f, new Color(1f, 0.85f, 0.3f));
        if (hits > 0)
        {
            var rb = fx.Pogo.GetComponent<Rigidbody>();
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(v.x, 16f, v.z);   // rebota ~6 m
        }
    }

    // ---- Piñata del Último Rezo ----
    private void SpawnPinata(Slot s)
    {
        Vector3 fwd = Camera.main != null ? Camera.main.transform.forward : fx.Player.forward;
        fwd.y = 0f;
        Vector3 at = ItemEffects.GroundAt(fx.Player.position + fwd.normalized * 3f) + Vector3.up * 2f;
        if (pinata != null) Destroy(pinata.gameObject);
        pinata = Pinata.Spawn(at, fx, pinataHitClip);
        s.activeTotal = 6f;
        s.activeLeft = 6f;
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
            string label = "";
            if (s.activeLeft > 0f) label = s.activeLeft.ToString("0.0") + " s";
            if (s.item.id == ItemEffects.Detonador) label = charges.Count + "/3";
            if (s.item.id == ItemEffects.Matraca && s == matracaSlot && SpitAttack.Instance != null) label = "x" + SpitAttack.Instance.StoredVolleys;
            if (s.item.id == ItemEffects.Pinata && pinata != null) label = pinata.Hits + "/3";
            s.label.text = label;
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

// Carga del Detonador: sigue al demonio marcado; si muere, se queda en el suelo. Dura 8 s.
public class DetonatorCharge : MonoBehaviour
{
    public EnemyTracker Target { get; private set; }
    private float life = 8f;

    public static DetonatorCharge Plant(EnemyTracker e, Material fx)
    {
        var go = new GameObject("CargaDetonador");
        var c = go.AddComponent<DetonatorCharge>();
        c.Target = e;
        go.transform.position = e.Center;
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.35f, 0.25f, 0.25f);
        ItemFx.Paint(body, new Color(0.9f, 0.2f, 0.1f), 1.2f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = Color.red; l.range = 2.5f; l.intensity = 2f; l.shadows = LightShadows.None;
        ItemFx.Burst(go.transform.position, 8, 0.2f, new Color(1f, 0.3f, 0.1f), fx, 2f, 0.2f, 0.3f);
        return c;
    }

    private void Update()
    {
        if (TimeStop.Active) return;
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        if (Target != null && !Target.IsDead)
            transform.position = Target.Top + Vector3.up * 0.2f;
        else if (Target != null)
        {
            transform.position = ItemEffects.GroundAt(transform.position) + Vector3.up * 0.15f;
            Target = null;
        }
        var l = GetComponent<Light>();
        l.intensity = (Mathf.Sin(Time.time * (life < 2f ? 20f : 8f)) > 0f) ? 3f : 0.5f;
    }
}

// Piñata del Último Rezo: písala tres veces en 6 s para que estalle.
public class Pinata : MonoBehaviour
{
    public int Hits { get; private set; }
    private float life = 6f;
    private ItemEffects fx;
    private AudioClip hitClip;
    private Collider col;
    private bool wasGrounded = true;
    private Transform body;

    public static Pinata Spawn(Vector3 at, ItemEffects fx, AudioClip hitClip)
    {
        var go = new GameObject("Pinata");
        go.transform.position = at;
        var p = go.AddComponent<Pinata>();
        p.fx = fx;
        p.hitClip = hitClip;
        p.body = new GameObject("Cuerpo").transform;
        p.body.SetParent(go.transform, false);
        Color[] colors = { new Color(1f, 0.2f, 0.5f), new Color(1f, 0.85f, 0.1f), new Color(0.2f, 0.8f, 1f), new Color(0.4f, 1f, 0.3f), new Color(1f, 0.5f, 0.1f) };
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(p.body, false);
        core.transform.localScale = Vector3.one * 1.2f;
        ItemFx.Paint(core, colors[0], 0.4f);
        for (int i = 0; i < 6; i++)
        {
            var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(spike.GetComponent<Collider>());
            spike.transform.SetParent(p.body, false);
            Vector3 dir = i < 4 ? Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward : (i == 4 ? Vector3.up : Vector3.down);
            spike.transform.localPosition = dir * 0.8f;
            spike.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            spike.transform.localScale = new Vector3(0.25f, 0.45f, 0.25f);
            ItemFx.Paint(spike, colors[1 + i % 4], 0.4f);
        }
        var box = go.AddComponent<BoxCollider>();   // sin Rigidbody: Pogoman puede rebotar encima
        box.size = new Vector3(1.8f, 1.4f, 1.8f);
        p.col = box;
        ItemFx.Burst(at, 30, 0.6f, new Color(1f, 0.6f, 0.9f), fx.FxMaterial, 4f, 0.3f, 0.6f);
        return p;
    }

    private void Update()
    {
        if (TimeStop.Active) return;
        life -= Time.deltaTime;
        body.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 3f) * 10f, Time.time * 40f, 0f);
        if (life <= 0f)
        {
            ItemFx.Burst(transform.position, 20, 0.5f, new Color(0.6f, 0.5f, 0.5f), fx.FxMaterial, 3f, 0.3f, 0.6f);   // se rompe sin explotar
            Destroy(gameObject);
            return;
        }

        // ¿Pogoman acaba de caer encima?
        PogoPlayer p = fx.Pogo;
        bool grounded = p != null && PogoBridge.IsGrounded(p);
        if (grounded && !wasGrounded)
        {
            Vector3 feet = p.transform.position;
            if (Physics.Raycast(feet, Vector3.down, out RaycastHit hit, 2.2f, ~0, QueryTriggerInteraction.Ignore) && hit.collider == col)
            {
                Hits++;
                body.localScale = Vector3.one * (1f + Hits * 0.15f);
                ItemFx.Burst(transform.position + Vector3.up * 0.6f, 25, 0.5f, new Color(1f, 0.8f, 0.2f), fx.FxMaterial, 5f, 0.3f, 0.5f);
                if (hitClip != null) AudioSource.PlayClipAtPoint(hitClip, transform.position, GameSettings.SfxVolume);
                if (Hits >= 3)
                {
                    fx.Explode(transform.position, 5f, fx.PogoDamage * 3f, new Color(1f, 0.4f, 0.8f));
                    for (int i = 0; i < 4; i++)
                        ItemFx.Burst(transform.position, 30, 0.5f, Color.HSVToRGB(Random.value, 0.8f, 1f), fx.FxMaterial, 10f, 0.3f, 1f);
                    Destroy(gameObject);
                }
            }
        }
        wasGrounded = grounded;
    }
}
