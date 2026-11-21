using UnityEngine;

// Al derrotar al jefe de un pilar: además de la llave (la suelta BossPillar) aparece un cofre gratis.
public class KeyRewards : MonoBehaviour
{
    [Tooltip("Distancia del cofre gratis respecto al pilar.")]
    [SerializeField] private float chestOffset = 3f;
    [SerializeField] private AudioClip rewardClip;

    private AudioSource audioSource;

    private void Awake() => audioSource = UiKit.UiAudio(gameObject);
    private void OnEnable() => BossPillar.Cleared += OnPillarCleared;
    private void OnDisable() => BossPillar.Cleared -= OnPillarCleared;

    private void OnPillarCleared(Vector3 pillar)
    {
        Chest template = null;
        foreach (Chest c in FindObjectsByType<Chest>())
            if (template == null || (!c.Opened && template.Opened))
                template = c;
        if (template == null)
            return;

        // Junto al pilar, sobre el suelo, mirando hacia Pogoman.
        PogoPlayer player = FindAnyObjectByType<PogoPlayer>();
        Vector3 toPlayer = player != null ? player.transform.position - pillar : Vector3.forward;
        toPlayer.y = 0f;
        Vector3 side = toPlayer.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, toPlayer.normalized) : Vector3.right;
        Vector3 at = pillar + side * chestOffset;
        if (Physics.Raycast(at + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            at = hit.point;

        // El cofre del mapa tiene su pivote donde lo dejó Blender: copiamos su altura relativa al suelo.
        float baseOffset = template.transform.position.y - template.GetComponent<Renderer>().bounds.min.y;
        // Misma orientación que el cofre del mapa (su eje "arriba" viene de Blender), girado hacia Pogoman.
        float yaw = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer.normalized).eulerAngles.y : 0f;
        Quaternion rot = Quaternion.Euler(0f, yaw - template.transform.eulerAngles.y, 0f) * template.transform.rotation;

        GameObject copy = Instantiate(template.gameObject, at + Vector3.up * baseOffset, rot);
        copy.name = "CofreGratis";
        // Quitar lo que el cofre original creó al iniciar (luz, chispas) y su color de "abierto".
        foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
            if (child != copy.transform && (child.GetComponent<Light>() != null || child.GetComponent<ParticleSystem>() != null))
                Destroy(child.gameObject);
        foreach (Renderer r in copy.GetComponentsInChildren<Renderer>())
            r.SetPropertyBlock(null);
        copy.GetComponent<Chest>().Configure(0);

        ItemFx.Burst(at + Vector3.up * 0.8f, 40, 0.8f, new Color(1f, 0.8f, 0.3f), Chest.SparkleMaterial, 6f, 0.3f, 0.7f);
        if (rewardClip != null)
            audioSource.PlayOneShot(rewardClip, 0.9f * GameSettings.SfxVolume);
    }
}
