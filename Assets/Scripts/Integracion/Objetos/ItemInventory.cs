using System;
using System.Collections.Generic;
using UnityEngine;

// Objetos que lleva Pogoman en la partida (se acumulan: x2, x3...).
// Para darles efecto: ItemInventory.Instance.ItemAdded += (objeto, cantidad) => { ... };
public class ItemInventory : MonoBehaviour
{
    public static ItemInventory Instance { get; private set; }

    public event Action<ItemDefinition, int> ItemAdded;
    // Se gastó uno (p. ej. la Estampita de San Pogo); la cantidad nueva puede ser 0.
    public event Action<ItemDefinition, int> ItemRemoved;

    private readonly List<ItemDefinition> order = new List<ItemDefinition>();
    private readonly Dictionary<ItemDefinition, int> counts = new Dictionary<ItemDefinition, int>();

    public IReadOnlyList<ItemDefinition> Items => order;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int Count(ItemDefinition item) => item != null && counts.TryGetValue(item, out int n) ? n : 0;

    public int Count(string id)
    {
        foreach (ItemDefinition item in order)
            if (item.id == id)
                return counts[item];
        return 0;
    }

    public void Add(ItemDefinition item)
    {
        if (item == null)
            return;

        if (!counts.ContainsKey(item))
        {
            counts[item] = 0;
            order.Add(item);
        }

        counts[item]++;
        ItemAdded?.Invoke(item, counts[item]);
    }

    public bool Remove(ItemDefinition item)
    {
        if (item == null || !counts.TryGetValue(item, out int n) || n <= 0)
            return false;

        n--;
        if (n == 0)
        {
            counts.Remove(item);
            order.Remove(item);
        }
        else
        {
            counts[item] = n;
        }
        ItemRemoved?.Invoke(item, n);
        return true;
    }
}
