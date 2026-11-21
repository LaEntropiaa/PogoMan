using UnityEngine;

// Cofre del mapa que se compra con monedas. ChestUI se encarga de la animación.
public class Chest : MonoBehaviour
{
    // El cofre cerrado más cercano que está al alcance de Pogoman.
    public static Chest Nearest { get; private set; }

    [SerializeField] private int price = 50;
    [SerializeField] private float interactDistance = 2.6f;

    public int Price => price;
    public bool Opened { get; private set; }

    private static Transform player;
    private Renderer[] renderers;

    public void Configure(int newPrice) => price = newPrice;

    private void Awake() => renderers = GetComponentsInChildren<Renderer>();

    private Vector3 Center => renderers.Length > 0 ? renderers[0].bounds.center : transform.position;

    private void Update()
    {
        if (Opened)
            return;

        if (player == null)
        {
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p == null)
                return;
            player = p.transform;
        }

        float distance = Vector3.Distance(Center, player.position);
        bool inRange = distance < interactDistance;

        if (inRange && (Nearest == null || Nearest.Opened ||
                        Vector3.Distance(Nearest.Center, player.position) > distance))
            Nearest = this;
        else if (!inRange && Nearest == this)
            Nearest = null;
    }

    public void MarkOpened()
    {
        Opened = true;
        if (Nearest == this)
            Nearest = null;

        // Un cofre abierto se ve más apagado.
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(0.45f, 0.4f, 0.4f));
            r.SetPropertyBlock(block);
        }
    }

    private void OnDisable()
    {
        if (Nearest == this)
            Nearest = null;
    }
}
