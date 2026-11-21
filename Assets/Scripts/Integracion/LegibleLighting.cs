using UnityEngine;

// Iluminación más neutra para que se vean los colores (el mapa sigue rojo por sus materiales,
// pero la luz ya no tiñe todo de rojo). Ajusta al iniciar las luces del prefab del mapa,
// sin modificarlo (si se regenera el mapa, esto se sigue aplicando).
public class LegibleLighting : MonoBehaviour
{
    [Header("Sol (la luz direccional con sombras)")]
    [SerializeField] private Color sunColor = new Color(1f, 0.95f, 0.88f);
    [SerializeField] private float sunIntensity = 1.35f;

    [Header("Otras luces direccionales (relleno)")]
    [Tooltip("0 = gris, 1 = color original.")]
    [SerializeField, Range(0f, 1f)] private float fillSaturation = 0.3f;
    [SerializeField] private float fillIntensity = 0.45f;

    [Header("Luces puntuales (lava, antorchas...)")]
    [SerializeField, Range(0f, 1f)] private float pointSaturation = 0.6f;
    [SerializeField] private float pointIntensityMultiplier = 0.7f;

    [Header("Ambiente")]
    [SerializeField] private Color ambientSky = new Color(0.52f, 0.5f, 0.58f);
    [SerializeField] private Color ambientEquator = new Color(0.42f, 0.38f, 0.38f);
    [SerializeField] private Color ambientGround = new Color(0.2f, 0.17f, 0.17f);

    [Header("Niebla")]
    [SerializeField] private Color fogColor = new Color(0.22f, 0.14f, 0.16f);
    [SerializeField] private float fogDensity = 0.007f;

    private void Start()
    {
        Light sun = null;
        Light[] lights = FindObjectsByType<Light>();

        // El sol es la direccional que tiene sombras (o la más fuerte).
        foreach (Light l in lights)
            if (l.type == LightType.Directional &&
                (sun == null || (l.shadows != LightShadows.None && sun.shadows == LightShadows.None) || l.intensity > sun.intensity))
                sun = l;

        foreach (Light l in lights)
        {
            if (l == sun)
            {
                l.color = sunColor;
                l.intensity = sunIntensity;
            }
            else if (l.type == LightType.Directional)
            {
                l.color = Desaturate(l.color, fillSaturation);
                l.intensity = fillIntensity;
            }
            else
            {
                l.color = Desaturate(l.color, pointSaturation);
                l.intensity *= pointIntensityMultiplier;
            }
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    private static Color Desaturate(Color c, float keep)
    {
        float gray = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        return Color.Lerp(new Color(gray, gray, gray), c, keep);
    }
}
