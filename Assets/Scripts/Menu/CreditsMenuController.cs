using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Créditos editables desde el Inspector: agrega una entrada por integrante.
public class CreditsMenuController : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string role = "ROL";
        public string name = "NOMBRE";
    }

    [SerializeField] private Text body;
    [SerializeField] private List<Entry> team = new List<Entry>();
    [TextArea(2, 6)]
    [SerializeField] private string notes = "";

    private void Awake() => Refresh();

    // Se actualiza en el editor mientras editas el Inspector.
    private void OnValidate() => Refresh();

    public void Refresh()
    {
        if (body == null)
            return;

        var sb = new StringBuilder();

        foreach (Entry e in team)
        {
            sb.AppendLine(e.role.ToUpperInvariant());
            sb.AppendLine(e.name);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(notes))
            sb.AppendLine(notes);

        body.text = sb.ToString().TrimEnd();
    }
}
