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
        // Junto al pilar, sobre el suelo.
        PogoPlayer player = FindAnyObjectByType<PogoPlayer>();
        Vector3 toPlayer = player != null ? player.transform.position - pillar : Vector3.forward;
        toPlayer.y = 0f;
        Vector3 side = toPlayer.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, toPlayer.normalized) : Vector3.right;
        Vector3 at = pillar + side * chestOffset;
        if (ChestSpawner.Spawn(at + Vector3.up * 2f, 0, false) == null)
            return;
        ItemFx.Burst(at + Vector3.up * 0.8f, 40, 0.8f, new Color(1f, 0.8f, 0.3f), Chest.SparkleMaterial, 6f, 0.3f, 0.7f);
        if (rewardClip != null)
            audioSource.PlayOneShot(rewardClip, 0.9f * GameSettings.SfxVolume);
    }
}
