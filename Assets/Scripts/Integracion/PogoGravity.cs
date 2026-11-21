using UnityEngine;

// Gravedad extra solo para Pogoman: con más gravedad y un poco más de fuerza
// de salto, los rebotes son más bajos pero más rápidos.
//   altura ≈ fuerzaSalto² / (2 · gravedadTotal)
//   tiempo en el aire ≈ 2 · fuerzaSalto / gravedadTotal
[RequireComponent(typeof(Rigidbody))]
public class PogoGravity : MonoBehaviour
{
    [Tooltip("Aceleración hacia abajo que se suma a la gravedad normal (9.81).")]
    [SerializeField] private float extraGravity = 12f;

    private Rigidbody rb;
    private PogoPlayer player;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PogoPlayer>();
    }

    private void FixedUpdate()
    {
        if (player != null && PogoBridge.IsDead(player))
            return;

        rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
    }
}
