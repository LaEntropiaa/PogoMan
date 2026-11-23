using UnityEngine;

// Prepara al iniciar el nivel lo que no está en la escena ni en los scripts originales:
// le agrega a Pogoman la vida extra/escudo y las habilidades (dash, saltos en el aire)
// y deja listos los prefabs y sonidos de las gemas y los demonios.
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

    [Header("Escudo")]
    [SerializeField] private AudioClip shieldHitClip;
    [SerializeField] private AudioClip shieldBreakClip;

    [Header("Demonios")]
    [SerializeField] private Material dirtMaterial;

    [Header("Habilidades de los jefes (BossCaster)")]
    [SerializeField] private Material orbMaterial;
    [SerializeField] private AudioClip magicCastClip;
    [SerializeField] private AudioClip magicImpactClip;
    [SerializeField] private AudioClip fireRingClip;

    private void Awake()
    {
        XpGem.Prefab = gemPrefab;
        XpGem.PickupClip = gemClip;
        ZombieMotor.SetDirtMaterial(dirtMaterial);
        BossCaster.OrbMaterial = orbMaterial;
        BossCaster.FxMaterial = fxMaterial;
        BossCaster.CastClip = magicCastClip;
        BossCaster.ImpactClip = magicImpactClip;
        BossCaster.RingClip = fireRingClip;
        Chest.SparkleMaterial = fxMaterial;
        PlayerVitals.ShieldHitClip = shieldHitClip;
        PlayerVitals.ShieldBreakClip = shieldBreakClip;

        PogoCamera cam = FindAnyObjectByType<PogoCamera>();
        if (cam != null && cam.GetComponent<CameraSensitivity>() == null)
            cam.gameObject.AddComponent<CameraSensitivity>();

        PogoPlayer player = FindAnyObjectByType<PogoPlayer>();
        if (player == null)
            return;

        if (player.GetComponent<PlayerVitals>() == null)
            player.gameObject.AddComponent<PlayerVitals>();

        PogoAbilities abilities = player.GetComponent<PogoAbilities>();
        if (abilities == null)
            abilities = player.gameObject.AddComponent<PogoAbilities>();
        abilities.Configure(dashClip, airJumpClip, fxMaterial);

        // Onda del pogo, castigo por pararse en la cabeza de un demonio y regreso al mapa si te caes.
        if (player.GetComponent<PogoShockwave>() == null)
            player.gameObject.AddComponent<PogoShockwave>();
        if (player.GetComponent<HeadStandPunish>() == null)
            player.gameObject.AddComponent<HeadStandPunish>();
        if (player.GetComponent<PogoDeathFx>() == null)
            player.gameObject.AddComponent<PogoDeathFx>();
        if (player.GetComponent<OutOfBoundsGuard>() == null)
            player.gameObject.AddComponent<OutOfBoundsGuard>();
        if (DemoGodMode.Enabled && player.GetComponent<DemoGodMode>() == null)
            player.gameObject.AddComponent<DemoGodMode>();
    }
}
