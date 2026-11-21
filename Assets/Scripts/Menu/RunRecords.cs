using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Récords de todo el juego. La tabla mezcla un top "de la casa" (para irlo superando)
// con las partidas del jugador (guardadas en su compu, records.json).
// Puntos = demonios derrotados (kills).
public static class RunRecords
{
    [Serializable]
    public class Entry
    {
        public string name;
        public int score;
        public float time;
        public int level;
        public string result;
        public string date;
        public bool house;   // del top de la casa (no se guarda)
    }

    [Serializable]
    private class Data { public List<Entry> entries = new List<Entry>(); }

    public const int TableSize = 15;
    public const string Champion = "TLBZ|AlanMD";

    // Top de la casa.
    private static readonly Entry[] House =
    {
        H(Champion, 100000, 3725, 61, "INFIERNO INFINITO NV 47"),
        H("Alonso", 27450, 1980, 34, "INFIERNO INFINITO NV 18"),
        H("Ruben", 18900, 1610, 29, "VICTORIA"),
        H("PablitoGamerxX", 12340, 1322, 25, "VICTORIA"),
        H("JoSeJoSe", 9870, 1104, 22, "MURIÓ"),
        H("ElPogoDeTuPrima", 6666, 960, 19, "VICTORIA"),
        H("DonChuyRebotes", 4200, 845, 17, "MURIÓ"),
        H("xXSatanasXx", 3333, 702, 15, "MURIÓ"),
        H("LaDeLosTamales", 2150, 615, 13, "MURIÓ"),
        H("Pogolas", 1420, 488, 11, "MURIÓ"),
        H("TenderoDeLaEsquina", 980, 402, 9, "MURIÓ"),
        H("NoobDelInframundo", 640, 315, 7, "MURIÓ"),
        H("PogoPapi", 450, 260, 6, "MURIÓ"),
        H("ManchasDeSalsa", 300, 198, 5, "MURIÓ"),
    };
    private static Entry H(string n, int s, float t, int l, string r) => new Entry { name = n, score = s, time = t, level = l, result = r, date = "—", house = true };

    private static string FilePath => Path.Combine(Application.persistentDataPath, "records.json");
    private static PlayerStats savedFor;   // una sola vez por partida
    private static Entry last;

    public static int LastRank { get; private set; }
    public static bool LastIsPersonalBest { get; private set; }

    public static List<Entry> LoadPlayer()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var d = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
                if (d != null && d.entries != null) return d.entries;
            }
        }
        catch (Exception e) { Debug.LogWarning("Récords: no se pudo leer " + e.Message); }
        return new List<Entry>();
    }

    private static void SavePlayer(List<Entry> list)
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(new Data { entries = list }, true)); }
        catch (Exception e) { Debug.LogWarning("Récords: no se pudo guardar " + e.Message); }
    }

    private static int Compare(Entry a, Entry b) => a.score != b.score ? b.score.CompareTo(a.score) : b.time.CompareTo(a.time);

    // Tabla completa (casa + jugador), de mayor a menor.
    public static List<Entry> Table()
    {
        var all = new List<Entry>(House);
        all.AddRange(LoadPlayer());
        all.Sort(Compare);
        if (all.Count > TableSize) all.RemoveRange(TableSize, all.Count - TableSize);
        return all;
    }

    public static int PersonalBest
    {
        get
        {
            int best = 0;
            foreach (Entry e in LoadPlayer()) best = Mathf.Max(best, e.score);
            return best;
        }
    }

    // Guarda la partida actual. Regresa su lugar en la tabla (1 = arriba) o 0 si no entró.
    public static int RecordCurrentRun(string result)
    {
        if (PlayerStats.Instance == null || savedFor == PlayerStats.Instance)
            return LastRank;
        savedFor = PlayerStats.Instance;
        PlayerStats s = PlayerStats.Instance;
        int previousBest = PersonalBest;
        last = new Entry
        {
            name = "TÚ",
            score = s.Kills,
            time = s.RunTime,
            level = s.Level,
            result = result,
            date = DateTime.Now.ToString("dd/MM/yy"),
        };
        LastIsPersonalBest = last.score > previousBest;

        var mine = LoadPlayer();
        mine.Add(last);
        mine.Sort(Compare);
        if (mine.Count > TableSize) mine.RemoveRange(TableSize, mine.Count - TableSize);
        SavePlayer(mine);

        var table = Table();
        LastRank = table.FindIndex(e => !e.house && e.score == last.score && Mathf.Approximately(e.time, last.time) && e.date == last.date) + 1;
        return LastRank;
    }

    // El jugador escribe el nombre de quien hizo el récord.
    public static void RenameLast(string name)
    {
        if (last == null) return;
        name = string.IsNullOrWhiteSpace(name) ? "SIN NOMBRE" : name.Trim();
        if (name.Length > 18) name = name.Substring(0, 18);
        var mine = LoadPlayer();
        foreach (Entry e in mine)
            if (e.score == last.score && Mathf.Approximately(e.time, last.time) && e.date == last.date && e.name == last.name)
            {
                e.name = name;
                break;
            }
        last.name = name;
        SavePlayer(mine);
    }

    // Texto corto para las pantallas de fin de partida.
    public static string Summary(string result)
    {
        int rank = RecordCurrentRun(result);
        int score = PlayerStats.Instance != null ? PlayerStats.Instance.Kills : 0;
        string line = "PUNTOS  " + score + "     TU MEJOR  " + Mathf.Max(score, PersonalBest);
        if (rank > 0) line = "<color=#FFD24D>¡ENTRASTE AL TOP!  LUGAR #" + rank + "</color>\n" + line;
        else if (LastIsPersonalBest) line = "<color=#FFD24D>¡NUEVO RÉCORD PERSONAL!</color>\n" + line;
        return line;
    }
}
