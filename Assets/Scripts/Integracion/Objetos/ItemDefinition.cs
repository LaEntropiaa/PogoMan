using UnityEngine;

public enum ItemRarity { Comun, Rara, Epica, Legendaria }

// Un objeto del juego (sale de los cofres). El efecto lo aplica ItemEffects según el "id".
[CreateAssetMenu(menuName = "Pogo Man/Objeto", fileName = "Objeto")]
public class ItemDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    [TextArea(1, 3)] public string quote;
    public Sprite icon;
    [Tooltip("Probabilidad relativa dentro de su rareza.")]
    public float weight = 1f;

    [Header("Calidad")]
    public ItemRarity rarity = ItemRarity.Comun;
    [Tooltip("Copias máximas por partida (0 = sin límite).")]
    public int maxCopies;
    [Tooltip("Habilidad activa: se usa con las teclas 1-6.")]
    public bool active;
    [Tooltip("Pacto maldito: antes de tomarlo se muestra el costo y se puede rechazar.")]
    public bool cursed;

    public static readonly Color[] RarityColors =
    {
        new Color32(0xD4, 0xD0, 0xC8, 0xFF),
        new Color32(0x58, 0xAF, 0xFF, 0xFF),
        new Color32(0xBC, 0x77, 0xFF, 0xFF),
        new Color32(0xFF, 0xC3, 0x4D, 0xFF),
    };
    public static readonly string[] RarityNames = { "COMÚN", "RARA", "ÉPICA", "LEGENDARIA" };

    public Color RarityColor => RarityColors[(int)rarity];
    public string RarityName => RarityNames[(int)rarity];
}
