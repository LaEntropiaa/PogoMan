using PogoMan.Estigia;
using UnityEngine;

// Conecta el mapa "El Estigia" con el juego al iniciar, buscando por nombre,
// sin modificar el prefab generado (si se regenera el mapa, todo sigue funcionando):
//  - los cofres de muestra se pueden comprar,
//  - el río y las lagunas del Estigia hacen daño,
//  - los géiseres lanzan más alto y avisan a PogoJump,
//  - se ocultan los pilares de muestra (los pilares reales los pone el juego),
//  - los pilares se enderezan al colocarse (los puntos del mapa vienen acostados de Blender).
public class EstigiaGameHooks : MonoBehaviour
{
    [Header("Cofres")]
    [SerializeField] private int chestPrice = 50;

    [Header("Agua del Estigia (hace daño)")]
    [Tooltip("Inicio del nombre del objeto del río (en el prefab los nombres llevan un número al final).")]
    [SerializeField] private string hazardName = "El_Estigia_rio";
    [SerializeField] private AudioClip hazardSound;
    [SerializeField] private Material hazardSparks;

    [Header("Géiseres")]
    [Tooltip("Altura extra sobre la isla de llegada (más = lanza más alto).")]
    [SerializeField] private float geyserClearance = 4.5f;

    private void Awake()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;

            // En el prefab los nombres van "limpios" y con número: Cofre_01_1023, Pilar_Preview_1_0460...
            if (n.StartsWith("Cofre_") && !n.StartsWith("Cofre_Pos") && t.GetComponent<MeshRenderer>() != null)
                t.gameObject.AddComponent<Chest>().Configure(chestPrice);
            else if (n.StartsWith(hazardName))
                MakeHazard(t.gameObject);
            else if (n.StartsWith("Pilar_Preview"))
                t.gameObject.SetActive(false);
        }

        foreach (EstigiaGeyser geyser in GetComponentsInChildren<EstigiaGeyser>(true))
        {
            geyser.clearance = geyserClearance;
            geyser.onLaunch.AddListener(v => PogoJump.Instance?.OnExternalLaunch(v));
        }
    }

    // Los puntos de los pilares (los pentagramas ZonaPilar) vienen de Blender con -90° en X y
    // BossPillar copia esa rotación, así que el pilar salía tirado. Los pentagramas se dejan como
    // están y solo se endereza el pilar, un frame después de que BossPillar lo coloca.
    private System.Collections.IEnumerator Start()
    {
        yield return null;
        foreach (BossPillar pillar in FindObjectsByType<BossPillar>())
            StandUp(pillar.transform);
    }

    private static void StandUp(Transform t)
    {
        Vector3 facing = t.rotation * Vector3.up;   // el "frente" del empty de Blender
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.001f)
            facing = Vector3.forward;
        t.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
    }

    private void MakeHazard(GameObject go)
    {
        if (go.GetComponent<Collider>() == null)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter != null)
                go.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
        go.AddComponent<LavaHazard>().Configure(hazardSound, hazardSparks);
    }
}
