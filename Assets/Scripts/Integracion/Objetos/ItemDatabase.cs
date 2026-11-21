using System.Collections.Generic;
using UnityEngine;

// Lista de objetos que pueden salir de un cofre.
[CreateAssetMenu(menuName = "Pogo Man/Base de objetos", fileName = "BaseDeObjetos")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> items = new List<ItemDefinition>();

    // Elige un objeto al azar respetando el "weight" de cada uno.
    // "allowed" deja fuera los que ya no sirven (no acumulables que ya tienes, o al máximo).
    public ItemDefinition RollRandom(System.Predicate<ItemDefinition> allowed = null)
    {
        var pool = new List<ItemDefinition>();
        foreach (ItemDefinition item in items)
            if (item != null && (allowed == null || allowed(item)))
                pool.Add(item);
        if (pool.Count == 0)
            pool.AddRange(items);

        float total = 0f;
        foreach (ItemDefinition item in pool)
            total += Mathf.Max(0f, item.weight);

        float roll = Random.value * total;
        foreach (ItemDefinition item in pool)
        {
            roll -= Mathf.Max(0f, item.weight);
            if (roll <= 0f)
                return item;
        }

        return pool.Count > 0 ? pool[pool.Count - 1] : null;
    }
}
