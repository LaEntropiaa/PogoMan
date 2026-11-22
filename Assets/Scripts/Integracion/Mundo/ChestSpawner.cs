using System.Collections.Generic;
using UnityEngine;

// Más cofres: el mapa trae muchos puntos "Cofre_Pos_" vacíos; al empezar se llenan varios con copias
// de los cofres del mapa. En el modo infinito además van saliendo cofres nuevos cada tanto.
public class ChestSpawner : MonoBehaviour
{
    public static ChestSpawner Instance { get; private set; }

    [SerializeField] private int extraChests = 28;
    [SerializeField] private float minSpacing = 14f;
    [SerializeField] private int price = 50;

    private readonly List<Vector3> spots = new List<Vector3>();

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private System.Collections.IEnumerator Start()
    {
        yield return null;   // que EstigiaGameHooks ya haya convertido los cofres del mapa
        foreach (Transform t in FindObjectsByType<Transform>())
            if (t.name.StartsWith("Cofre_Pos_"))
                spots.Add(t.position);

        var taken = new List<Vector3>();
        foreach (Chest c in FindObjectsByType<Chest>()) taken.Add(c.transform.position);

        // Barajar los puntos y llenar los que estén lejos de otros cofres.
        for (int i = spots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
        int made = 0;
        foreach (Vector3 p in spots)
        {
            if (made >= extraChests) break;
            bool near = false;
            foreach (Vector3 q in taken) if ((q - p).sqrMagnitude < minSpacing * minSpacing) { near = true; break; }
            if (near) continue;
            if (Spawn(p, price, false) != null) { taken.Add(p); made++; }
        }
        Debug.Log("ChestSpawner: " + made + " cofres extra (puntos disponibles " + spots.Count + ").");
    }

    // Copia un cofre del mapa (limpio, sin la luz/chispas del original) en ese punto.
    public static Chest Spawn(Vector3 at, int cost, bool fx)
    {
        Chest template = null;
        foreach (Chest c in FindObjectsByType<Chest>())
            if (template == null || (!c.Opened && template.Opened)) template = c;
        if (template == null) return null;
        // El piso más cercano debajo, sin contar demonios ni a Pogoman (el élite que lo suelta sigue ahí).
        RaycastHit hit = default;
        bool found = false;
        foreach (RaycastHit h in Physics.RaycastAll(at + Vector3.up * 6f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            if (h.rigidbody == null && (!found || h.distance < hit.distance)) { hit = h; found = true; }
        if (!found)
            return null;
        if (hit.collider.GetComponent<LavaHazard>() != null) return null;

        Renderer tr = template.GetComponent<Renderer>();
        float baseOffset = tr != null ? template.transform.position.y - tr.bounds.min.y : 0f;
        var holder = new GameObject("CopiaCofre");
        holder.SetActive(false);
        GameObject copy = Instantiate(template.gameObject, hit.point + Vector3.up * baseOffset,
            Quaternion.Euler(0f, Random.Range(0f, 360f) - template.transform.eulerAngles.y, 0f) * template.transform.rotation, holder.transform);
        copy.name = cost <= 0 ? "CofreGratis" : "CofreExtra";
        foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
            if (child != copy.transform && (child.GetComponent<Light>() != null || child.GetComponent<ParticleSystem>() != null))
                DestroyImmediate(child.gameObject);
        foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(null);
        Chest chest = copy.GetComponent<Chest>();
        chest.Configure(cost);
        copy.transform.SetParent(null, true);
        Destroy(holder);
        if (fx)
            ItemFx.Burst(hit.point + Vector3.up, 40, 0.8f, new Color(1f, 0.8f, 0.3f), Chest.SparkleMaterial, 6f, 0.3f, 0.7f);
        return chest;
    }

    // Modo infinito: un cofre nuevo en algún punto a 20-45 m de Pogoman.
    public Chest SpawnNear(Vector3 player)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 c = Random.insideUnitCircle.normalized * Random.Range(20f, 45f);
            Vector3 p = player + new Vector3(c.x, 10f, c.y);
            Chest chest = Spawn(p, price, true);
            if (chest != null) return chest;
        }
        return null;
    }
}
