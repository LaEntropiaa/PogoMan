using UnityEngine;

// Prepara al iniciar el nivel lo que no está en la escena ni en los scripts originales:
// le agrega a Pogoman la vida extra/escudo y las habilidades (dash, saltos en el aire)
// y deja listos los prefabs y sonidos de las gemas y los zombies.
[DefaultExecutionOrder(-50)]
public class RunSystems : MonoBehaviour
{
    [Header("Gemas de experiencia")]
    [SerializeField] private XpGem gemPrefab;
    [SerializeField] private AudioClip gemClip;

    [Header("Habilidades")]
    [SerializeField] private AudioClip dashClip;
    [SerializeField] private AudioClip airJumpClip;
    [SerializeField] private Material fxMaterial;

    [Header("Zombies")]
    [SerializeField] private Material dirtMaterial;

    private void Awake()
    {
        XpGem.Prefab = gemPrefab;
        XpGem.PickupClip = gemClip;
        ZombieMotor.SetDirtMaterial(dirtMaterial);

        PogoPlayer player = FindAnyObjectByType<PogoPlayer>();
        if (player == null)
            return;

        if (player.GetComponent<PlayerVitals>() == null)
            player.gameObject.AddComponent<PlayerVitals>();

        PogoAbilities abilities = player.GetComponent<PogoAbilities>();
        if (abilities == null)
            abilities = player.gameObject.AddComponent<PogoAbilities>();
        abilities.Configure(dashClip, airJumpClip, fxMaterial);
    }
}
