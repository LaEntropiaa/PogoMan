using UnityEngine;

// Cielo del mapa: usa un skybox panorámico (la niebla no lo tapa, a diferencia del domo del mapa,
// que se oculta) y lo gira muy despacio para que las nubes se muevan.
public class SkyDrift : MonoBehaviour
{
    [SerializeField] private Material skybox;
    [Tooltip("Parte del nombre del material del domo viejo (se oculta).")]
    [SerializeField] private string domeMaterialName = "Cielo";
    [Tooltip("Grados por segundo.")]
    [SerializeField] private float speed = 0.35f;

    private Material sky;
    private float rotation;

    private void Start()
    {
        if (skybox == null)
            return;

        foreach (Renderer r in FindObjectsByType<Renderer>())
        {
            Material m = r.sharedMaterial;
            if (m != null && m.name.Contains(domeMaterialName) && r.bounds.size.x > 200f)
                r.enabled = false;
        }

        sky = new Material(skybox);   // copia: girarlo no toca el asset
        RenderSettings.skybox = sky;
        foreach (Camera c in FindObjectsByType<Camera>())
            if (c.CompareTag("MainCamera"))
                c.clearFlags = CameraClearFlags.Skybox;
        rotation = sky.HasProperty("_Rotation") ? sky.GetFloat("_Rotation") : 0f;
    }

    private void Update()
    {
        if (sky == null || !sky.HasProperty("_Rotation"))
            return;
        rotation = (rotation + speed * Time.deltaTime) % 360f;
        sky.SetFloat("_Rotation", rotation);
    }
}
