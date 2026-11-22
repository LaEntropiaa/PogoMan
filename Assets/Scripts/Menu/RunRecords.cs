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
    public const int HudSize = 10;
    private const string NameKey = "PogoRecordName";

    // El nombre se pide una sola vez; después todas tus partidas usan ese.
    public static string SavedName => PlayerPrefs.GetString(NameKey, "");
    public static bool NeedsName => string.IsNullOrEmpty(SavedName);
    public const string Champion = "TLBZ|AlanMD";

    // Top de la casa.
    private static readonly Entry[] House =
    {
        H(Champion, 100000, 3725, 61, "INFIERNO INFINITO NV 47"),
        H("Alonso", 27450, 1980, 34, "INFIERNO INFINITO NV 18"),
        H("Ruben", 18900, 1610, 29, "VICTORIA"),
        H("PablitoGamerxX", 12340, 1322, 25, "VICTORIA"),
        H("JoSeJoSe", 9870, 1104, 22, "MURIÓ"),
        H("Gedgar", 8120, 1046, 21, "CAYÓ AL LIMBO"),
        H("Patrichocle", 7340, 1012, 20, "MURIÓ (INJUSTO)"),
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
                if (d != null && d.entries != null && d.entries.Count > 0)
                {
                    // Solo cuenta tu mejor partida (antes se guardaban varias con distinto nombre).
                    d.entries.Sort(Compare);
                    Entry best = d.entries[0];
                    if (!NeedsName) best.name = SavedName;
                    else if (!string.IsNullOrWhiteSpace(best.name) && best.name != "TÚ" && best.name != "SIN NOMBRE")
                        PlayerPrefs.SetString(NameKey, best.name);   // ya lo había escrito antes: no se vuelve a pedir
                    return new List<Entry> { best };
                }
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

    // Top para el HUD durante la partida: la casa + tu mejor + la partida actual (en vivo).
    public static List<Entry> LiveTable(int currentScore, float currentTime, out int currentRank)
    {
        var all = new List<Entry>(House);
        foreach (Entry e in LoadPlayer())
            if (e.score > currentScore) all.Add(e);
        var now = new Entry { name = NeedsName ? "TÚ" : SavedName, score = currentScore, time = currentTime, result = "AHORA" };
        all.Add(now);
        all.Sort(Compare);
        currentRank = all.IndexOf(now) + 1;
        if (all.Count > HudSize) all.RemoveRange(HudSize, all.Count - HudSize);
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
            name = NeedsName ? "TÚ" : SavedName,
            score = s.Kills,
            time = s.RunTime,
            level = s.Level,
            result = result,
            date = DateTime.Now.ToString("dd/MM/yy"),
        };
        LastIsPersonalBest = last.score > previousBest;

        // Solo se guarda si superó tu mejor partida.
        var mine = LoadPlayer();
        if (mine.Count == 0 || Compare(last, mine[0]) < 0)
            mine = new List<Entry> { last };
        SavePlayer(mine);

        var table = Table();
        LastRank = LastIsPersonalBest || mine[0] == last
            ? table.FindIndex(e => !e.house) + 1
            : 0;
        return LastRank;
    }

    // El jugador escribe el nombre de quien hizo el récord.
    public static void RenameLast(string name)
    {
        if (last == null) return;
        name = string.IsNullOrWhiteSpace(name) ? "SIN NOMBRE" : name.Trim();
        if (name.Length > 18) name = name.Substring(0, 18);
        PlayerPrefs.SetString(NameKey, name);
        PlayerPrefs.Save();
        var mine = LoadPlayer();
        foreach (Entry e in mine)
            e.name = name;
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
