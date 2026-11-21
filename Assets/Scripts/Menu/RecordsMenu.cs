using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Panel de RÉCORDS del menú principal: el top de todo el juego (la casa + tus partidas).
public class RecordsMenu : MonoBehaviour
{
    [SerializeField] private Text body;
    [SerializeField] private Color headerColor = new Color(0.6f, 0.07f, 0.03f);
    [SerializeField] private Color mineColor = new Color(0.1f, 0.35f, 0.08f);
    [SerializeField] private Color goldColor = new Color(0.78f, 0.52f, 0.02f);

    private void OnEnable() => Refresh();

    private static string Pad(string s, int w) => s.Length >= w ? s + " " : s + new string(' ', w - s.Length);

    public void Refresh()
    {
        if (body == null) return;
        var table = RunRecords.Table();
        string h = "#" + ColorUtility.ToHtmlStringRGB(headerColor);
        string g = "#" + ColorUtility.ToHtmlStringRGB(goldColor);
        string m = "#" + ColorUtility.ToHtmlStringRGB(mineColor);
        var sb = new StringBuilder();
        sb.AppendLine("<color=" + h + ">" + Pad("#", 4) + Pad("NOMBRE", 20) + Pad("PUNTOS", 9) + "TIEMPO</color>");
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            int t = Mathf.FloorToInt(e.time);
            string line = Pad((i + 1).ToString(), 4) + Pad(e.name, 20) + Pad(e.score.ToString("N0"), 9) + (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
            if (i == 0) line = "<color=" + g + "><b>" + line + "</b></color>";
            else if (!e.house) line = "<color=" + m + ">" + line + "</color>";
            sb.AppendLine(line);
        }
        int best = RunRecords.PersonalBest;
        sb.AppendLine();
        sb.AppendLine("<color=" + h + ">TU MEJOR PARTIDA:</color> " + (best > 0 ? best.ToString("N0") + " PUNTOS" : "TODAVÍA NADA"));
        sb.AppendLine("<size=" + Mathf.Max(10, body.fontSize - 6) + ">PUNTOS = DEMONIOS DERROTADOS. SUPERA A LOS DEL TOP.</size>");
        body.text = sb.ToString().TrimEnd();
    }
}
