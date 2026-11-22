using UnityEngine;

// Aplica la sensibilidad de Opciones a la cámara (PogoCamera) sin modificar su script:
// multiplica su sensibilidad original por la elegida (0.2x a 3x).
public class CameraSensitivity : MonoBehaviour
{
    private PogoCamera cam;
    private float baseSensitivity = 3f;

    private void Start()
    {
        cam = GetComponent<PogoCamera>();
        baseSensitivity = PogoBridge.GetFloat(cam, "mouseSensitivity", 3f);
    }

    private void Update()
    {
        if (cam != null)
            PogoBridge.SetFloat(cam, "mouseSensitivity", baseSensitivity * GameSettings.MouseSensitivity);
    }
}
