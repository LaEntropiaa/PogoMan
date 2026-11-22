using System.Collections.Generic;
using UnityEngine;

// Lista de objetos que pueden salir de un cofre, con tiradas por rareza:
// primero se elige la rareza (según el minuto de partida y de dónde viene el cofre)
// y luego un objeto al azar entre los que todavía se pueden obtener.
[CreateAssetMenu(menuName = "Pogo Man/Base de objetos", fileName = "BaseDeObjetos")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> items = new List<ItemDefinition>();

    // Pesos (%) Común/Rara/Épica/Legendaria — rarezas.json
    private static readonly float[] Bought0 = { 65, 27, 7, 1 };     // minutos 0-5
    private static readonly float[] Bought5 = { 58, 30, 10, 2 };    // 5-10
    private static readonly float[] Bought10 = { 50, 33, 14, 3 };   // 10+
    private static readonly float[] Guardian = { 20, 44, 30, 6 };   // cofre gratis de guardián

    public static float[] Weights(bool guardian, float minutes) =>
        guardian ? Guardian : minutes < 5f ? Bought0 : minutes < 10f ? Bought5 : Bought10;

    public bool HasAny(System.Predicate<ItemDefinition> allowed)
    {
        foreach (ItemDefinition item in items)
            if (item != null && (allowed == null || allowed(item)))
                return true;
        return false;
    }

    public ItemDefinition RollRandom(System.Predicate<ItemDefinition> allowed = null, bool guardian = false, float minutes = 0f)
    {
        var byRarity = new List<ItemDefinition>[4];
        for (int r = 0; r < 4; r++) byRarity[r] = new List<ItemDefinition>();
        foreach (ItemDefinition item in items)
            if (item != null && (allowed == null || allowed(item)))
                byRarity[(int)item.rarity].Add(item);

        // Si una rareza no tiene nada disponible, se reparten los pesos entre las demás.
        float[] w = Weights(guardian, minutes);
        float total = 0f;
        for (int r = 0; r < 4; r++)
            if (byRarity[r].Count > 0) total += w[r];
        if (total <= 0f)
            return null;

        float roll = Random.value * total;
        for (int r = 0; r < 4; r++)
        {
            if (byRarity[r].Count == 0) continue;
            roll -= w[r];
            if (roll <= 0f)
                return Pick(byRarity[r]);
        }
        for (int r = 3; r >= 0; r--)
            if (byRarity[r].Count > 0) return Pick(byRarity[r]);
        return null;
    }

    private static ItemDefinition Pick(List<ItemDefinition> list)
    {
        float total = 0f;
        foreach (ItemDefinition i in list) total += Mathf.Max(0.01f, i.weight);
        float roll = Random.value * total;
        foreach (ItemDefinition i in list)
        {
            roll -= Mathf.Max(0.01f, i.weight);
            if (roll <= 0f) return i;
        }
        return list[list.Count - 1];
    }

    public ItemDefinition Find(string id)
    {
        foreach (ItemDefinition i in items)
            if (i != null && i.id == id) return i;
        return null;
    }
}
