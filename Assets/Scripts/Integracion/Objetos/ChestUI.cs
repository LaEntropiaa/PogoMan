using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Comprar y abrir cofres:
//  1) Cerca de un cofre sale el aviso; con E (o X/□ en control) lo compras por 50 monedas.
//  2) El juego se pausa y aparece el cofre: hay que pulsar 3 veces para abrirlo.
//  3) Sale un objeto al azar con su imagen, nombre y descripción, y se guarda en el inventario.
public class ChestUI : MonoBehaviour
{
    [SerializeField] private ItemDatabase database;

    [Header("Aviso")]
    [SerializeField] private CanvasGroup prompt;
    [SerializeField] private Text promptText;

    [Header("Ventana")]
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private RectTransform chestRect;
    [SerializeField] private Image chestImage;
    [SerializeField] private Sprite[] chestFrames;   // cerrado, cerrado, entreabierto, abierto
    [SerializeField] private Image rays;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Text itemName;
    [SerializeField] private Text itemDescription;
    [SerializeField] private Text itemQuote;
    [SerializeField] private Text hint;
    [SerializeField] private Text priceFx;

    [Header("Sonidos")]
    [SerializeField] private AudioClip buyClip;
    [SerializeField] private AudioClip knockClip;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip itemClip;
    [SerializeField] private AudioClip deniedClip;

    public static bool IsOpen { get; private set; }

    private AudioSource audioSource;
    private float denyShake;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.ignoreListenerPause = true;
        SetGroup(panel, 0f);
        SetGroup(prompt, 0f);
    }

    private static void SetGroup(CanvasGroup g, float a)
    {
        if (g == null) return;
        g.alpha = a;
        g.blocksRaycasts = a > 0.5f;
    }

    private void Play(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, GameSettings.SfxVolume);
    }

    private static bool InteractPressed() =>
        (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
        (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);

    private static bool ConfirmPressed() =>
        InteractPressed() ||
        (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) ||
        (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
        (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

    private void Update()
    {
        if (IsOpen)
            return;

        Chest chest = Chest.Nearest;
        bool show = chest != null && Time.timeScale > 0f;
        int coins = PlayerStats.Instance != null ? PlayerStats.Instance.Coins : 0;

        if (prompt != null)
        {
            prompt.alpha = Mathf.MoveTowards(prompt.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            denyShake = Mathf.MoveTowards(denyShake, 0f, Time.unscaledDeltaTime * 3f);
            prompt.transform.localPosition = new Vector3(Mathf.Sin(Time.unscaledTime * 60f) * 10f * denyShake, prompt.transform.localPosition.y, 0f);
        }

        if (!show)
            return;

        if (promptText != null)
            promptText.text = chest.Price <= 0
                ? "E  ABRIR COFRE  GRATIS"
                : coins >= chest.Price
                ? "E  ABRIR COFRE  " + chest.Price + " MONEDAS"
                : "TE FALTAN " + (chest.Price - coins) + " MONEDAS";

        if (!InteractPressed())
            return;

        // Si ya no queda ningún objeto que pueda salir, no se cobra.
        if (database != null && !database.HasAny(ItemEffects.CanOffer))
        {
            denyShake = 1f;
            Play(deniedClip);
            if (promptText != null) promptText.text = "YA NO HAY OBJETOS PARA TI";
            return;
        }
        if (PlayerStats.Instance != null && PlayerStats.Instance.SpendCoins(chest.Price))
            StartCoroutine(OpenRoutine(chest));
        else
        {
            denyShake = 1f;
            Play(deniedClip);
        }
    }

    private IEnumerator OpenRoutine(Chest chest)
    {
        IsOpen = true;
        Time.timeScale = 0f;
        SetGroup(prompt, 0f);

        // El objeto se decide al empezar: la luz que se asoma por la rendija ya trae el color de su rareza.
        float minutes = PlayerStats.Instance != null ? PlayerStats.Instance.RunTime / 60f : 0f;
        ItemDefinition item = forcedItem != null ? forcedItem
            : database != null ? database.RollRandom(ItemEffects.CanOffer, chest != null && chest.Free, minutes) : null;
        forcedItem = null;
        Color rc = item != null ? item.RarityColor : new Color(1f, 0.85f, 0.4f);
        int tier = item != null ? (int)item.rarity : 0;

        EnsureRarityUi();
        EnsureChestParts();
        bool gift = chest == null;
        ResetChestParts(gift, rc);
        rays.color = new Color(1f, 0.85f, 0.4f, 0f);
        rays.rectTransform.localScale = Vector3.one;
        rarityText.text = "";
        glow.color = Color.clear;
        itemIcon.color = new Color(1, 1, 1, 0);
        itemIcon.rectTransform.localScale = Vector3.zero;
        itemName.text = itemDescription.text = itemQuote.text = "";
        hint.text = gift ? "" : "PULSA E 3 VECES PARA ABRIR";

        // Entrada: el cofre cae desde arriba y rebota (polvo al tocar el piso).
        Play(buyClip);
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.5f;
            SetGroup(panel, Mathf.Clamp01(k * 3f));
            chestRect.anchoredPosition = chestHome + Vector2.up * 520f * (1f - EaseOutBounce(k));
            chestRect.localScale = Vector3.one;
            yield return null;
        }
        SetGroup(panel, 1f);
        chestRect.anchoredPosition = chestHome;
        StartCoroutine(Dust(10));
        StartCoroutine(Squash(0.22f));
        if (chest != null)
            StartCoroutine(PriceFx(chest.Price));

        // Tres golpes: brinca, la tapa se levanta un poco más cada vez y se asoma más luz.
        for (int presses = gift ? 3 : 0; presses < 3;)
        {
            Idle(presses);
            if (ConfirmPressed())
            {
                presses++;
                Play(knockClip, 0.9f + presses * 0.12f);
                hint.text = presses < 3 ? "PULSA E " + (3 - presses) + (3 - presses == 1 ? " VEZ MÁS" : " VECES MÁS") : "";
                StartCoroutine(Knock(presses, rc));
            }
            yield return null;
        }

        if (!gift)
        {
            // Anticipación: tiembla cada vez más fuerte y la rendija brilla.
            yield return new WaitForSecondsRealtime(0.18f);
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.45f;
                chestRect.anchoredPosition = chestHome + new Vector2(Mathf.Sin(t * 90f) * 9f * k, 0f);
                chestRect.localScale = new Vector3(1f + 0.08f * k, 1f - 0.12f * k, 1f);
                crack.color = new Color(rc.r, rc.g, rc.b, 0.75f + 0.25f * Mathf.Sin(t * 50f));
                lid.rectTransform.anchoredPosition = lidHome + Vector2.up * (6f + Mathf.Abs(Mathf.Sin(t * 70f)) * 8f * k);
                yield return null;
            }
            chestRect.anchoredPosition = chestHome;
            chestRect.localScale = Vector3.one;
        }

        // ¡Se abre! La tapa sale volando, destello y onda del color de la rareza.
        rays.rectTransform.localScale = Vector3.one * (1f + tier * 0.18f);
        Play(openClip);
        if (!gift)
        {
            StartCoroutine(LidFly());
            baseClosed.enabled = false;
            baseOpen.enabled = true;
            crack.color = Color.clear;
            StartCoroutine(Flash(rc, 0.35f + tier * 0.1f));
            StartCoroutine(Shockwave(rc, tier));
            StartCoroutine(Squash(0.3f));
        }
        itemIcon.sprite = item != null ? item.icon : null;
        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.7f;
            rays.color = new Color(rc.r, rc.g, rc.b, Mathf.Clamp01(k * 2f) * (0.55f + tier * 0.15f));
            glow.color = new Color(rc.r, rc.g, rc.b, Mathf.Clamp01(k * 2f) * (0.25f + tier * 0.12f));
            itemIcon.color = new Color(1, 1, 1, Mathf.Clamp01(k * 3f));
            float pop = k < 0.65f ? Mathf.Lerp(0.2f, 1.25f, k / 0.65f) : Mathf.Lerp(1.25f, 1f, (k - 0.65f) / 0.35f);
            itemIcon.rectTransform.localScale = Vector3.one * pop;
            // Sale de dentro del cofre: sube rápido y se asienta con un pequeño vaivén.
            float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.8f), 3f);
            itemIcon.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(k * 9f) * 14f * (1f - k), Mathf.Lerp(-10f, 150f, rise));
            itemIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * 12f) * 18f * (1f - k));
            baseOpen.color = Color.Lerp(Color.white, new Color(1f, 0.95f, 0.8f), Mathf.PingPong(t * 4f, 1f));
            yield return null;
        }
        itemIcon.rectTransform.localRotation = Quaternion.identity;
        baseOpen.color = Color.white;

        Play(itemClip, 1f - tier * 0.05f);
        if (tier >= 2) Play(rarityClip, tier == 3 ? 0.85f : 1.1f);
        StartCoroutine(Sparkles(rc, 10 + tier * 14));
        if (item != null)
        {
            itemName.text = item.displayName.ToUpperInvariant();
            itemDescription.text = item.description;
            itemQuote.text = string.IsNullOrEmpty(item.quote) ? "" : "“" + item.quote + "”";
            rarityText.text = item.RarityName + (item.cursed ? "  ·  PACTO MALDITO" : item.active ? "  ·  ACTIVA (TECLA " + NextActiveKey() + ")" : "");
            rarityText.color = item.cursed ? new Color(1f, 0.35f, 0.3f) : rc;
        }
        bool cursed = item != null && item.cursed;
        hint.text = cursed ? "E  ACEPTAR EL PACTO        Q  RECHAZARLO" : "PULSA E PARA CONTINUAR";

        bool accepted = true;
        while (true)
        {
            float spin = -20f - tier * 12f;
            rays.rectTransform.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (3f + tier * 2f));
            glow.rectTransform.localScale = Vector3.one * (1f + pulse * 0.08f * (tier + 1));
            itemIcon.rectTransform.anchoredPosition = new Vector2(0f, 150f + Mathf.Sin(Time.unscaledTime * 2.5f) * 6f);
            if (cursed && Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) { accepted = false; break; }
            if (cursed ? InteractPressed() : ConfirmPressed()) break;
            yield return null;
        }

        // Pase lo que pase al entregar el objeto, la ventana se cierra (si no, ya no se podrían abrir más cofres).
        try
        {
            if (accepted)
                ItemInventory.Instance?.Add(item);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
        try
        {
            if (chest != null)
            {
                chest.MarkOpened();
                Chest.RaiseOpened(chest);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }

        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            SetGroup(panel, 1f - t / 0.2f);
            yield return null;
        }
        SetGroup(panel, 0f);
        Time.timeScale = 1f;
        IsOpen = false;
    }

    // ---- Rareza: brillo de fondo, texto y chispas del color de la rareza ----
    [SerializeField] private AudioClip rarityClip;
    private Text rarityText;
    private Image glow;
    private ItemDefinition forcedItem;

    // Para regalos (p. ej. el Diablo): abre la ventana con este objeto sin cofre.
    public void ShowGift(ItemDefinition item, System.Action done)
    {
        forcedItem = item;
        StartCoroutine(GiftRoutine(done));
    }

    private IEnumerator GiftRoutine(System.Action done)
    {
        yield return OpenRoutine(null);
        done?.Invoke();
    }

    private void EnsureRarityUi()
    {
        if (rarityText != null) return;
        var parent = (RectTransform)itemName.transform.parent;
        var rt = UiKit.Rect("Rareza", parent, itemName.rectTransform.anchorMin, itemName.rectTransform.anchorMax, itemName.rectTransform.pivot,
            itemName.rectTransform.anchoredPosition + new Vector2(0f, 46f), new Vector2(1000f, 40f));
        rarityText = UiKit.Text(rt, itemName.font, 30, Color.white, TextAnchor.MiddleCenter);
        rarityText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);

        var g = new GameObject("BrilloRareza", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(rays.transform.parent, false);
        g.transform.SetSiblingIndex(rays.transform.GetSiblingIndex());
        glow = g.GetComponent<Image>();
        glow.raycastTarget = false;
        glow.sprite = MakeGlowSprite();
        var grt = (RectTransform)g.transform;
        grt.anchorMin = grt.anchorMax = rays.rectTransform.anchorMin;
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = rays.rectTransform.anchoredPosition;
        grt.sizeDelta = rays.rectTransform.sizeDelta * 1.1f;
    }

    private static Sprite MakeGlowSprite()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    private IEnumerator Sparkles(Color c, int count)
    {
        var parent = (RectTransform)rays.transform.parent;
        var list = new System.Collections.Generic.List<(RectTransform rt, Image im, Vector2 v)>();
        for (int i = 0; i < count; i++)
        {
            var rt = UiKit.Rect("Chispa", parent, rays.rectTransform.anchorMin, rays.rectTransform.anchorMax, new Vector2(0.5f, 0.5f),
                rays.rectTransform.anchoredPosition + new Vector2(0f, 130f), Vector2.one * Random.Range(8f, 18f));
            var im = UiKit.Image(rt, c);
            float a = Random.value * Mathf.PI * 2f;
            list.Add((rt, im, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(250f, 650f)));
        }
        for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
        {
            foreach (var p in list)
            {
                p.rt.anchoredPosition += p.v * Time.unscaledDeltaTime * (1f - t / 1.2f);
                p.im.color = new Color(c.r, c.g, c.b, 1f - t / 1.2f);
            }
            yield return null;
        }
        foreach (var p in list) Destroy(p.rt.gameObject);
    }

    private static int NextActiveKey() => ItemActives.Instance != null ? Mathf.Min(6, CountActives() + 1) : 1;
    private static int CountActives()
    {
        int n = 0;
        if (ItemInventory.Instance != null)
            foreach (ItemDefinition i in ItemInventory.Instance.Items) if (i.active) n++;
        return n;
    }

    private IEnumerator Punch(RectTransform target, float strength)
    {
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / 0.25f;
            target.localScale = Vector3.one * (1f + strength * k * Mathf.Sin(t * 60f));
            target.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 70f) * 8f * k);
            yield return null;
        }
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;
    }

    // ---- Cofre animado: tapa y base por separado (recortadas de los mismos sprites) ----
    private Image lid, baseClosed, baseOpen, crack, flash;
    private Vector2 chestHome, lidHome;
    private float px;

    private void EnsureChestParts()
    {
        if (lid != null) return;
        chestHome = chestRect.anchoredPosition;
        px = chestRect.rect.width / 32f;   // los sprites son de 32×32
        Texture2D closedTex = chestFrames[0].texture;
        Texture2D openTex = chestFrames[chestFrames.Length - 1].texture;
        // En píxeles de textura (origen abajo): tapa = filas 8-15, base = 16-28, base abierta con el brillo = 11-28.
        baseClosed = Part("Base", closedTex, new Rect(4, 3, 24, 13));
        crack = Part("Rendija", null, new Rect(5, 15.5f, 22, 1));
        baseOpen = Part("BaseAbierta", openTex, new Rect(4, 3, 24, 18));
        lid = Part("Tapa", closedTex, new Rect(4, 16, 24, 8));
        lidHome = lid.rectTransform.anchoredPosition;
        chestImage.enabled = false;

        var f = new GameObject("Destello", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(panel.transform, false);
        f.transform.SetAsLastSibling();
        var frt = (RectTransform)f.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        flash = f.GetComponent<Image>();
        flash.raycastTarget = false;
        flash.color = Color.clear;
    }

    private Image Part(string name, Texture2D tex, Rect r)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(chestRect, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(r.width, r.height) * px;
        rt.anchoredPosition = new Vector2(r.center.x - 16f, r.center.y - 16f) * px;
        var im = go.GetComponent<Image>();
        im.raycastTarget = false;
        if (tex != null)
            im.sprite = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return im;
    }

    private void ResetChestParts(bool open, Color rc)
    {
        lid.enabled = !open;
        lid.color = Color.white;
        lid.rectTransform.anchoredPosition = lidHome;
        lid.rectTransform.localRotation = Quaternion.identity;
        baseClosed.enabled = !open;
        baseOpen.enabled = open;
        crack.color = new Color(rc.r, rc.g, rc.b, 0f);
        chestRect.localRotation = Quaternion.identity;
        chestRect.localScale = Vector3.one;
    }

    // Mientras esperas el golpe: la tapa traquetea (más mientras más golpes lleva).
    private void Idle(int presses)
    {
        if (lid == null || !lid.enabled || knocking) return;
        float t = Time.unscaledTime;
        float rattle = presses == 0 ? 0f : Mathf.Max(0f, Mathf.Sin(t * 13f)) * (1.5f + presses * 1.5f);
        lid.rectTransform.anchoredPosition = lidHome + Vector2.up * rattle;
        chestRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 1.5f);
    }

    private bool knocking;
    private IEnumerator Knock(int presses, Color rc)
    {
        knocking = true;
        float hop = 26f + presses * 14f, lift = 8f + presses * 8f;
        float tilt = (presses % 2 == 0 ? -1f : 1f) * (5f + presses * 2f);
        StartCoroutine(CrackSparks(rc, 4 + presses * 4));
        for (float t = 0f; t < 0.32f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.32f;
            float arc = Mathf.Sin(k * Mathf.PI);
            chestRect.anchoredPosition = chestHome + Vector2.up * hop * arc;
            float sq = k < 0.15f ? Mathf.Lerp(1f, 0.82f, k / 0.15f) : k > 0.85f ? Mathf.Lerp(1.12f, 0.9f, (k - 0.85f) / 0.15f) : Mathf.Lerp(0.82f, 1.12f, (k - 0.15f) / 0.7f);
            chestRect.localScale = new Vector3(2f - sq, sq, 1f);
            chestRect.localRotation = Quaternion.Euler(0f, 0f, tilt * arc);
            lid.rectTransform.anchoredPosition = lidHome + Vector2.up * lift * Mathf.Sin(Mathf.Min(1f, k * 1.4f) * Mathf.PI);
            lid.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -tilt * 0.6f * arc);
            crack.color = new Color(rc.r, rc.g, rc.b, Mathf.Max(presses / 3f * 0.8f, arc));
            yield return null;
        }
        chestRect.anchoredPosition = chestHome;
        chestRect.localRotation = Quaternion.identity;
        lid.rectTransform.localRotation = Quaternion.identity;
        crack.color = new Color(rc.r, rc.g, rc.b, presses / 3f * 0.8f);
        StartCoroutine(Squash(0.15f + presses * 0.04f));
        StartCoroutine(Dust(3 + presses * 2));
        knocking = false;
    }

    private IEnumerator Squash(float amount)
    {
        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.22f;
            float s = 1f - amount * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
            chestRect.localScale = new Vector3(2f - s, s, 1f);
            yield return null;
        }
        chestRect.localScale = Vector3.one;
    }

    private IEnumerator LidFly()
    {
        RectTransform rt = lid.rectTransform;
        Vector2 v = new Vector2(Random.Range(-260f, 260f), 1500f);
        float spin = Random.Range(500f, 800f) * (v.x < 0f ? 1f : -1f);
        for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            v.y -= 2600f * dt;
            rt.anchoredPosition += v * dt;
            rt.localRotation *= Quaternion.Euler(0f, 0f, spin * dt);
            lid.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1.6f - t * 1.6f));
            yield return null;
        }
        lid.enabled = false;
    }

    private IEnumerator Flash(Color c, float strength)
    {
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / 0.4f;
            flash.color = Color.Lerp(c, Color.white, 0.6f) * new Color(1f, 1f, 1f, strength * k * k);
            yield return null;
        }
        flash.color = Color.clear;
    }

    private IEnumerator Shockwave(Color c, int tier)
    {
        var rt = UiKit.Rect("Onda", (RectTransform)rays.transform.parent, rays.rectTransform.anchorMin, rays.rectTransform.anchorMax, new Vector2(0.5f, 0.5f),
            chestHome + Vector2.up * 20f, Vector2.one * 120f);
        var im = UiKit.Image(rt, c);
        im.sprite = MakeRingSprite();
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.6f;
            rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 6f + tier * 1.5f, 1f - (1f - k) * (1f - k));
            im.color = new Color(c.r, c.g, c.b, 0.9f * (1f - k));
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    private IEnumerator CrackSparks(Color c, int count)
    {
        var list = new System.Collections.Generic.List<(RectTransform rt, Image im, Vector2 v)>();
        for (int i = 0; i < count; i++)
        {
            var rt = UiKit.Rect("ChispaRendija", chestRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(Random.Range(-10f, 10f) * px, -0.5f * px), Vector2.one * Random.Range(6f, 12f));
            var im = UiKit.Image(rt, c);
            list.Add((rt, im, new Vector2(Random.Range(-1f, 1f) * 320f, Random.Range(120f, 420f))));
        }
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                p.v.y -= 1400f * dt;
                list[i] = p;
                p.rt.anchoredPosition += p.v * dt;
                p.im.color = new Color(c.r, c.g, c.b, 1f - t / 0.6f);
            }
            yield return null;
        }
        foreach (var p in list) Destroy(p.rt.gameObject);
    }

    private IEnumerator Dust(int count)
    {
        var parent = (RectTransform)chestRect.parent;
        float half = chestRect.rect.width * 0.4f;
        var list = new System.Collections.Generic.List<(RectTransform rt, Image im, Vector2 v)>();
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            var rt = UiKit.Rect("Polvo", parent, chestRect.anchorMin, chestRect.anchorMax, new Vector2(0.5f, 0.5f),
                chestHome + new Vector2(side * Random.Range(0.5f, 1f) * half, -chestRect.rect.height * 0.38f), Vector2.one * Random.Range(10f, 22f));
            var im = UiKit.Image(rt, new Color(0.75f, 0.65f, 0.55f, 0.8f));
            list.Add((rt, im, new Vector2(side * Random.Range(120f, 320f), Random.Range(20f, 110f))));
        }
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            foreach (var p in list)
            {
                p.rt.anchoredPosition += p.v * Time.unscaledDeltaTime * (1f - t / 0.5f);
                p.rt.localScale = Vector3.one * (1f + t * 1.5f);
                p.im.color = new Color(0.75f, 0.65f, 0.55f, 0.8f * (1f - t / 0.5f));
            }
            yield return null;
        }
        foreach (var p in list) Destroy(p.rt.gameObject);
    }

    private static float EaseOutBounce(float x)
    {
        const float n = 7.5625f, d = 2.75f;
        if (x < 1f / d) return n * x * x;
        if (x < 2f / d) { x -= 1.5f / d; return n * x * x + 0.75f; }
        if (x < 2.5f / d) { x -= 2.25f / d; return n * x * x + 0.9375f; }
        x -= 2.625f / d; return n * x * x + 0.984375f;
    }

    private static Sprite ringSprite;
    private static Sprite MakeRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 9f)));
            }
        t.Apply();
        ringSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        return ringSprite;
    }

    private IEnumerator PriceFx(int price)
    {
        if (priceFx == null || price <= 0) yield break;
        priceFx.text = "-" + price;
        var rect = priceFx.rectTransform;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-200f, -120f, t));
            priceFx.color = new Color(1f, 0.82f, 0.3f, 1f - t);
            yield return null;
        }
        priceFx.color = Color.clear;
    }
}
