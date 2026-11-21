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
        StartCoroutine(PriceFx(chest.Price));

        // Tres golpes para abrir
        for (int presses = 0; presses < 3;)
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
        ItemDefinition item = database != null ? database.RollRandom(ItemEffects.CanOffer) : null;
        Play(openClip);
        itemIcon.sprite = item != null ? item.icon : null;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.6f;
            rays.color = new Color(1f, 0.85f, 0.4f, Mathf.Clamp01(k * 2f) * 0.8f);
            itemIcon.color = new Color(1, 1, 1, Mathf.Clamp01(k * 2f));
            float pop = k < 0.7f ? Mathf.Lerp(0f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
            itemIcon.rectTransform.localScale = Vector3.one * pop;
            itemIcon.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(20f, 150f, Mathf.Sqrt(k)));
            yield return null;
        }

        Play(itemClip);
        if (item != null)
        {
            itemName.text = item.displayName.ToUpperInvariant();
            itemDescription.text = item.description;
            itemQuote.text = string.IsNullOrEmpty(item.quote) ? "" : "“" + item.quote + "”";
        }
        hint.text = "PULSA E PARA CONTINUAR";

        while (!ConfirmPressed())
        {
            rays.rectTransform.Rotate(0f, 0f, -20f * Time.unscaledDeltaTime);
            yield return null;
        }

        ItemInventory.Instance?.Add(item);
        chest.MarkOpened();

        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            SetGroup(panel, 1f - t / 0.2f);
            yield return null;
        }
        SetGroup(panel, 0f);
        Time.timeScale = 1f;
        IsOpen = false;
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
