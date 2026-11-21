using UnityEngine;

// Un objeto del juego (sale de los cofres). El efecto lo aplica ItemEffects según el "id".
[CreateAssetMenu(menuName = "Pogo Man/Objeto", fileName = "Objeto")]
public class ItemDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    [TextArea(1, 3)] public string quote;
    public Sprite icon;
    [Tooltip("Probabilidad relativa de salir de un cofre.")]
    public float weight = 1f;
}
