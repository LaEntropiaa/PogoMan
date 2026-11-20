using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Opción de lista tipo "< 1920x1080 >": izquierda/derecha cambia el valor,
// clic o Enter avanza al siguiente.
[RequireComponent(typeof(Button))]
public class OptionCycler : MonoBehaviour, IMoveHandler
{
    [SerializeField] private Text valueLabel;

    public event Action<int> Changed;   // -1 o +1

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => Step(+1));
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left)
            Step(-1);
        else if (eventData.moveDir == MoveDirection.Right)
            Step(+1);
    }

    private void Step(int direction)
    {
        Changed?.Invoke(direction);
        MenuAudio.Instance?.PlayHover();
    }

    public void SetText(string text)
    {
        if (valueLabel != null)
            valueLabel.text = "<  " + text + "  >";
    }
}
