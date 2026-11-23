using UnityEngine;

// Temblor de la cámara (encima de lo que haga PogoCamera ese frame).
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;
    private float time, strength;
    private Vector3 applied;

    public static void Add(float seconds, float amount)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (instance == null) instance = cam.gameObject.AddComponent<CameraShake>();
        instance.time = Mathf.Max(instance.time, seconds);
        instance.strength = Mathf.Max(instance.strength, amount);
    }

    // Antes de que PogoCamera calcule (Update va antes que cualquier LateUpdate) se quita el temblor
    // del frame anterior: así la cámara nunca "hereda" el desplazamiento y no se va acumulando.
    private void Update()
    {
        transform.position -= applied;
        applied = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (time <= 0f) return;
        time -= Time.deltaTime;
        float k = Mathf.Clamp01(time / 0.6f) * strength;
        applied = new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f, 0f) * 0.9f * k;
        applied = transform.rotation * applied;
        transform.position += applied;
        if (time <= 0f) strength = 0f;
    }
}
