using System.Collections.Generic;
using UnityEngine;

public enum UpgradeRarity { Comun, Rara, Epica, Legendaria }

// Una mejora que se puede elegir al subir de nivel.
public class UpgradeDef
{
    public string Name;
    public string Stat;         // nombre que se muestra ("Daño", "Cadencia"...)
    public PogoStat Target;
    public bool Percent;        // true = suma porcentaje, false = suma plano
    public float Amount;        // cantidad en rareza común
    public bool WholeNumber;    // saltos y proyectiles: +1 / +2
    public float Cap = float.MaxValue;   // si la stat ya llegó aquí, ya no sale
    public string Format = "0.#";        // cómo se muestra el valor
    public float Display = 1f;           // multiplicador para mostrar (0..1 → %)
    public string Unit = "";
}

// Lista de mejoras estilo Megabonk y tirada de rarezas.
public static class Upgrades
{
    public static readonly float[] RarityMultiplier = { 1f, 1.5f, 2.2f, 3.2f };
    public static readonly string[] RarityName = { "COMÚN", "RARA", "ÉPICA", "LEGENDARIA" };
    public static readonly Color[] RarityColor =
    {
        new Color(0.85f, 0.85f, 0.85f),
        new Color(0.35f, 0.65f, 1f),
        new Color(0.75f, 0.35f, 1f),
        new Color(1f, 0.72f, 0.15f),
    };

    public static readonly List<UpgradeDef> All = new List<UpgradeDef>
    {
        // ---- Escupitajo ----
        new UpgradeDef { Name = "Saliva de Chile Habanero", Stat = "Daño", Target = PogoStat.SpitDamage, Amount = 3f },
        new UpgradeDef { Name = "Metralleta de Gargajos", Stat = "Disparos por segundo", Target = PogoStat.FireRate, Percent = true, Amount = 0.12f, Format = "0.00" },
        new UpgradeDef { Name = "Ojo de Halcón Bizco", Stat = "Prob. de crítico", Target = PogoStat.CritChance, Amount = 0.04f, Cap = 1f, Format = "0", Display = 100f, Unit = "%" },
        new UpgradeDef { Name = "Escupitajo Bendito", Stat = "Daño crítico", Target = PogoStat.CritMultiplier, Amount = 0.25f, Format = "0.00", Unit = "x" },
        new UpgradeDef { Name = "Pulmones de Fumador", Stat = "Alcance", Target = PogoStat.Range, Percent = true, Amount = 0.1f, Unit = " m" },
        new UpgradeDef { Name = "Gargajo Supersónico", Stat = "Velocidad del proyectil", Target = PogoStat.ProjectileSpeed, Percent = true, Amount = 0.15f },
        new UpgradeDef { Name = "Salpicadura de Agua Puerca", Stat = "Daño en área", Target = PogoStat.SpitArea, Amount = 0.35f, Format = "0.0", Unit = " m" },
        new UpgradeDef { Name = "Flema Gigante", Stat = "Tamaño", Target = PogoStat.SpitSize, Percent = true, Amount = 0.15f, Format = "0", Display = 100f, Unit = "%" },
        new UpgradeDef { Name = "Escupitajo Gemelo", Stat = "Proyectiles", Target = PogoStat.Projectiles, Amount = 1f, WholeNumber = true, Cap = 12f, Format = "0" },
        // ---- Movimiento ----
        new UpgradeDef { Name = "Resorte de Colchón", Stat = "Saltos en el aire", Target = PogoStat.AirJumps, Amount = 1f, WholeNumber = true, Cap = 10f, Format = "0" },
        new UpgradeDef { Name = "Tenis Piratas", Stat = "Velocidad", Target = PogoStat.MoveSpeed, Percent = true, Amount = 0.07f, Format = "0", Display = 100f, Unit = "%" },
        new UpgradeDef { Name = "Patada de Mula", Stat = "Distancia del dash", Target = PogoStat.DashDistance, Amount = 1.2f, Unit = " m" },
        new UpgradeDef { Name = "Café de Olla", Stat = "Recarga del dash", Target = PogoStat.DashCooldown, Percent = true, Amount = -0.1f, Format = "0.0#", Unit = " s" },
        new UpgradeDef { Name = "Escape de Moto", Stat = "Rapidez del dash", Target = PogoStat.DashSpeed, Percent = true, Amount = 0.15f, Format = "0", Display = 100f, Unit = "%" },
        // ---- Supervivencia ----
        new UpgradeDef { Name = "Imán de Refri", Stat = "Radio de recogida", Target = PogoStat.PickupRadius, Amount = 0.8f, Unit = " m" },
        new UpgradeDef { Name = "Corazón de Pollo", Stat = "Vida máxima", Target = PogoStat.MaxHealth, Amount = 15f, Format = "0" },
        new UpgradeDef { Name = "Caldo de Gallina", Stat = "Regeneración", Target = PogoStat.Regeneration, Amount = 0.5f, Format = "0.0", Unit = "/s" },
        new UpgradeDef { Name = "Tapa de Olla", Stat = "Escudo", Target = PogoStat.Shield, Amount = 12f, Format = "0" },
        new UpgradeDef { Name = "Lotería Mañanera", Stat = "Experiencia", Target = PogoStat.XpGain, Percent = true, Amount = 0.1f, Format = "0", Display = 100f, Unit = "%" },
    };

    // Veces que se eligió cada mejora (para mostrar "Nv. 3").
    public static readonly Dictionary<UpgradeDef, int> Taken = new Dictionary<UpgradeDef, int>();

    public static void ResetRun() => Taken.Clear();

    public struct Offer
    {
        public UpgradeDef Def;
        public UpgradeRarity Rarity;
        public float Flat;
        public float Percent;
    }

    public static UpgradeRarity RollRarity(int level)
    {
        // Entre más nivel, un poquito más de suerte.
        float luck = Mathf.Min(level * 0.004f, 0.12f);
        float r = Random.value;
        if (r < 0.02f + luck * 0.4f) return UpgradeRarity.Legendaria;
        if (r < 0.11f + luck) return UpgradeRarity.Epica;
        if (r < 0.36f + luck * 1.5f) return UpgradeRarity.Rara;
        return UpgradeRarity.Comun;
    }

    public static Offer MakeOffer(UpgradeDef def, UpgradeRarity rarity)
    {
        float amount = def.WholeNumber
            ? (rarity >= UpgradeRarity.Epica ? 2f : 1f)
            : def.Amount * RarityMultiplier[(int)rarity];

        return new Offer
        {
            Def = def,
            Rarity = rarity,
            Flat = def.Percent ? 0f : amount,
            Percent = def.Percent ? amount : 0f,
        };
    }

    // Tres mejoras distintas al azar (sin las que ya están al máximo).
    public static List<Offer> Roll(PlayerStats stats, int count)
    {
        var pool = new List<UpgradeDef>();
        foreach (UpgradeDef d in All)
            if (stats.Get(d.Target) < d.Cap - 0.001f)
                pool.Add(d);

        var offers = new List<Offer>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            offers.Add(MakeOffer(pool[index], RollRarity(stats.Level)));
            pool.RemoveAt(index);
        }
        return offers;
    }

    public static void Apply(PlayerStats stats, Offer offer)
    {
        if (offer.Flat != 0f) stats.AddFlat(offer.Def.Target, offer.Flat);
        if (offer.Percent != 0f) stats.AddPercent(offer.Def.Target, offer.Percent);
        Taken.TryGetValue(offer.Def, out int n);
        Taken[offer.Def] = n + 1;
    }

    public static string FormatValue(UpgradeDef def, float value) =>
        (value * def.Display).ToString(def.Format) + def.Unit;
}
