using UnityEngine;

// Moneda que suelta un enemigo: salta un poquito, gira y vuela sola hacia
// Pogoman (imán). Al tocarlo suma una moneda.
public class Coin : MonoBehaviour
{
    [SerializeField] private float popTime = 0.35f;
    [SerializeField] private float magnetAcceleration = 60f;
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private float collectDistance = 0.7f;
    [SerializeField] private float spinSpeed = 540f;
    [SerializeField] private AudioClip collectClip;
    [SerializeField] private int value = 1;

    private static Transform player;

    private Vector3 velocity;
    private float age;

    private void Start()
    {
        velocity = new Vector3(Random.Range(-3f, 3f), Random.Range(4f, 7f), Random.Range(-3f, 3f));

        if (player == null)
        {
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p != null)
                player = p.transform;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        transform.Rotate(0f, spinSpeed * dt, 0f, Space.World);

        if (age < popTime || player == null)
        {
            velocity += Physics.gravity * dt;   // pequeño brinco al salir
        }
        else
        {
            Vector3 target = player.position + Vector3.up * 0.5f;
            Vector3 toPlayer = target - transform.position;

            if (toPlayer.magnitude < collectDistance)
            {
                Collect();
                return;
            }

            velocity = Vector3.MoveTowards(velocity, toPlayer.normalized * maxSpeed, magnetAcceleration * dt);
        }

        transform.position += velocity * dt;

        if (age > 6f)
            Collect();   // por si se atora en algo
    }

    private void Collect()
    {
        PlayerStats.Instance?.AddCoins(value);


        if (collectClip != null)
        {
            var go = new GameObject("SonidoMoneda");
            var src = go.AddComponent<AudioSource>();
            src.clip = collectClip;
            src.pitch = Random.Range(0.94f, 1.08f);
            src.volume = 0.55f * GameSettings.SfxVolume;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(go, collectClip.length / src.pitch + 0.1f);
        }

        Destroy(gameObject);
    }
}
