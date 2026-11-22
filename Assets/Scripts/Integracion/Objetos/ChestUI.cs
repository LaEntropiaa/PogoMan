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

        chestImage.sprite = chestFrames[0];
        rays.color = new Color(1f, 0.85f, 0.4f, 0f);
        rays.rectTransform.localScale = Vector3.one;
        EnsureRarityUi();
        rarityText.text = "";
        glow.color = Color.clear;
        itemIcon.color = new Color(1, 1, 1, 0);
        itemIcon.rectTransform.localScale = Vector3.zero;
        itemName.text = itemDescription.text = itemQuote.text = "";
        hint.text = "PULSA E 3 VECES PARA ABRIR";

        // Entrada de la ventana + "-50"
        Play(buyClip);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            SetGroup(panel, t / 0.25f);
            chestRect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t / 0.25f);
            yield return null;
        }
        SetGroup(panel, 1f);
        chestRect.localScale = Vector3.one;
        if (chest != null)
            StartCoroutine(PriceFx(chest.Price));

        // Tres golpes para abrir (un regalo ya viene abierto)
        if (chest == null)
            chestImage.sprite = chestFrames[chestFrames.Length - 1];
        for (int presses = chest == null ? 3 : 0; presses < 3;)
        {
            if (ConfirmPressed())
            {
                presses++;
                Play(knockClip, 0.9f + presses * 0.12f);
                chestImage.sprite = chestFrames[Mathf.Min(presses, chestFrames.Length - 1)];
                hint.text = presses < 3 ? "PULSA E " + (3 - presses) + (3 - presses == 1 ? " VEZ MÁS" : " VECES MÁS") : "";
                StartCoroutine(Punch(chestRect, 0.18f + presses * 0.06f));
            }
            yield return null;
        }

        // ¡Se abre!
        float minutes = PlayerStats.Instance != null ? PlayerStats.Instance.RunTime / 60f : 0f;
        ItemDefinition item = forcedItem != null ? forcedItem
            : database != null ? database.RollRandom(ItemEffects.CanOffer, chest != null && chest.Free, minutes) : null;
        forcedItem = null;
        Color rc = item != null ? item.RarityColor : new Color(1f, 0.85f, 0.4f);
        int tier = item != null ? (int)item.rarity : 0;
        rays.rectTransform.localScale = Vector3.one * (1f + tier * 0.18f);
        Play(openClip);
        itemIcon.sprite = item != null ? item.icon : null;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.6f;
            rays.color = new Color(rc.r, rc.g, rc.b, Mathf.Clamp01(k * 2f) * (0.55f + tier * 0.15f));
            glow.color = new Color(rc.r, rc.g, rc.b, Mathf.Clamp01(k * 2f) * (0.25f + tier * 0.12f));
            itemIcon.color = new Color(1, 1, 1, Mathf.Clamp01(k * 2f));
            float pop = k < 0.7f ? Mathf.Lerp(0f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
            itemIcon.rectTransform.localScale = Vector3.one * pop;
            itemIcon.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(20f, 150f, Mathf.Sqrt(k)));
            yield return null;
        }

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
