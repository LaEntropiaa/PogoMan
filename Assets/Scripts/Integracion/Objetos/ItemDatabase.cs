using System.Collections.Generic;
using UnityEngine;

// Lista de objetos que pueden salir de un cofre.
[CreateAssetMenu(menuName = "Pogo Man/Base de objetos", fileName = "BaseDeObjetos")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> items = new List<ItemDefinition>();

    // Elige un objeto al azar respetando el "weight" de cada uno.
    public ItemDefinition RollRandom()
    {
        float total = 0f;
        foreach (ItemDefinition item in items)
            total += Mathf.Max(0f, item.weight);

        float roll = Random.value * total;
        foreach (ItemDefinition item in items)
        {
            roll -= Mathf.Max(0f, item.weight);
            if (roll <= 0f)
                return item;
        }

        return items.Count > 0 ? items[items.Count - 1] : null;
    }
}
