using UnityEngine;

// Hace que la lava "fluya": mueve la textura poco a poco y pulsa el brillo.
// Usa una copia del material para no modificar el archivo original.
[RequireComponent(typeof(Renderer))]
public class LavaFlow : MonoBehaviour
{
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.01f, 0.006f);
    [SerializeField] private float pulseAmount = 0.15f;
    [SerializeField] private float pulseSpeed = 0.8f;

    private Material material;
    private Color baseEmission;
    private Vector2 offset;

    private void Start()
    {
        material = GetComponent<Renderer>().material;
        baseEmission = material.GetColor("_EmissionColor");
    }

    private void Update()
    {
        if (material == null)
            return;

        offset += scrollSpeed * Time.deltaTime;
        material.SetTextureOffset("_BaseMap", offset);

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
        material.SetColor("_EmissionColor", baseEmission * pulse);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
