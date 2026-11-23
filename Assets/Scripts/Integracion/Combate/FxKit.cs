using System.Collections;
using UnityEngine;

// Efectos "sólidos" para los ataques de jefes (no solo luces): mallas que crecen y se deshacen,
// rocas que saltan del suelo y ondas en el piso. Pocas partículas: casi todo son mallas simples.
public class FxKit : MonoBehaviour
{
    private static FxKit runner;
    private static FxKit Runner
    {
        get
        {
            if (runner == null) runner = new GameObject("FxKit").AddComponent<FxKit>();
            return runner;
        }
    }

    private static GameObject Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Color c, float glow)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetPositionAndRotation(pos, rot);
        g.transform.localScale = scale;
        ItemFx.Paint(g, c, glow);
        return g;
    }

    // Golpe al piso: polvo, chispas, marca y unas rocas que saltan.
    public static void Crack(Vector3 ground, float radius, Color glow)
    {
        AttackFx.GroundSlam(ground, radius, new Color(0.45f, 0.32f, 0.28f), glow);
        Runner.StartCoroutine(Rocks(ground, radius));
    }

    private static IEnumerator Rocks(Vector3 ground, float radius)
    {
        var rocks = new GameObject[4];
        var vel = new Vector3[4];
        for (int i = 0; i < rocks.Length; i++)
        {
            float s = Random.Range(0.15f, 0.35f) * Mathf.Clamp(radius / 2f, 0.6f, 1.6f);
            rocks[i] = Prim(PrimitiveType.Cube, ground + Random.insideUnitSphere * radius * 0.4f + Vector3.up * 0.2f,
                Vector3.one * s, Random.rotation, new Color(0.2f, 0.13f, 0.12f), 0.05f);
            vel[i] = new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 2.4f), Random.Range(-1f, 1f)) * 3.5f;
        }
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            for (int i = 0; i < rocks.Length; i++)
            {
                if (rocks[i] == null) continue;
                vel[i] += Physics.gravity * Time.deltaTime;
                rocks[i].transform.position += vel[i] * Time.deltaTime;
                rocks[i].transform.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0f);
                if (t > 0.6f) rocks[i].transform.localScale *= 0.9f;
            }
            yield return null;
        }
        foreach (GameObject g in rocks) if (g != null) Destroy(g);
    }

    // Onda en el piso (solo visual).
    public static void Ring(Vector3 ground, float radius, Color inner, Color outer) =>
        ShockRing.SpawnVisual(ground + Vector3.up * 0.1f, radius, 0.4f, inner, outer);
}
