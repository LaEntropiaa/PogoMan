using System;
using System.Collections.Generic;
using UnityEngine;

public enum PogoStat
{
    // Escupitajo
    SpitDamage,
    FireRate,
    CritChance,
    CritMultiplier,
    Range,
    ProjectileSpeed,
    SpitArea,       // radio del daño en área (m)
    SpitSize,       // tamaño del proyectil (multiplicador)
    Projectiles,    // proyectiles por disparo
    // Movimiento
    MoveSpeed,      // multiplicador de velocidad
    AirJumps,       // saltos extra en el aire
    DashDistance,   // metros
    DashCooldown,   // segundos (menos = mejor)
    DashSpeed,      // multiplicador de rapidez del dash
    // Supervivencia y otros
    PickupRadius,   // radio para recoger gemas (m)
    MaxHealth,      // vida extra sobre la base de Pogoman
    Regeneration,   // vida por segundo
    Shield,         // escudo máximo (absorbe daño y se recarga)
    XpGain,         // multiplicador de experiencia
}

// Stats de Pogoman, experiencia/niveles y estadísticas de la partida.
// Objetos y mejoras solo llaman a:
//   PlayerStats.Instance.AddFlat(PogoStat.SpitDamage, 5);      // +5 de daño
//   PlayerStats.Instance.AddPercent(PogoStat.FireRate, 0.2f);  // +20 % cadencia
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Escupitajo (base)")]
    [SerializeField] private float spitDamage = 15f;
    [SerializeField] private float fireRate = 1f;
    [SerializeField, Range(0f, 1f)] private float critChance = 0.1f;
    [SerializeField] private float critMultiplier = 2f;
    [SerializeField] private float range = 14f;
    [SerializeField] private float projectileSpeed = 22f;
    [SerializeField] private float spitArea = 1.2f;
    [SerializeField] private float spitSize = 1f;
    [SerializeField] private float projectiles = 1f;

    [Header("Movimiento (base)")]
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float airJumps = 0f;
    [SerializeField] private float dashDistance = 6f;
    [SerializeField] private float dashCooldown = 2.5f;
    [SerializeField] private float dashSpeed = 1f;

    [Header("Supervivencia (base)")]
    [SerializeField] private float pickupRadius = 3f;
    [SerializeField] private float regeneration = 0f;
    [SerializeField] private float shield = 0f;
    [SerializeField] private float xpGain = 1f;

    [Header("Experiencia")]
    [Tooltip("XP para pasar del nivel 1 al 2; cada nivel pide más.")]
    [SerializeField] private float firstLevelXp = 6f;
    [SerializeField] private float levelGrowth = 1.35f;

    private readonly Dictionary<PogoStat, float> flatBonus = new Dictionary<PogoStat, float>();
    private readonly Dictionary<PogoStat, float> percentBonus = new Dictionary<PogoStat, float>();
    // Multiplicadores de objetos (se multiplican entre sí: 0.8 × 0.8...).
    private readonly Dictionary<PogoStat, float> multiplier = new Dictionary<PogoStat, float>();

    [Tooltip("Recarga mínima del dash (s).")]
    [SerializeField] private float minDashCooldown = 0.5f;
    public float MinDashCooldown => minDashCooldown;

    public event Action Changed;

    // ---- Experiencia ----
    public int Level { get; private set; } = 1;
    public float Xp { get; private set; }
    public float XpToNext => Mathf.Round(firstLevelXp * Mathf.Pow(Level, levelGrowth));
    public int PendingLevelUps { get; private set; }
    public event Action<int> LevelUp;   // nivel nuevo

    // ---- Estadísticas de la partida ----
    public float RunTime { get; private set; }
    public int Kills { get; private set; }
    public float DamageDealt { get; private set; }
    public int CritHits { get; private set; }
    public float BiggestHit { get; private set; }
    public int Shots { get; set; }
    public int Coins { get; private set; }
    public int CoinsCollected { get; private set; }
    public event Action<int> CoinsChanged;
    public bool RunEnded { get; set; }
    // Con el tiempo detenido (Estampita de San Pogo) el reloj de la partida no avanza.
    public bool ClockStopped { get; set; }

    // ---- Contrarreloj ----
    [Header("Contrarreloj")]
    [Tooltip("Segundos para terminar la partida (15 min).")]
    [SerializeField] private float timeLimit = 900f;
    public float TimeLimit => timeLimit;
    public float TimeLeft => Mathf.Max(0f, timeLimit - RunTime);
    // Se dispara una vez cuando el tiempo llega a 0. Por ahora no hace nada.
    public event Action TimeUp;
    private bool timeUpFired;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!RunEnded && !ClockStopped)
            RunTime += Time.deltaTime;

        if (!timeUpFired && RunTime >= timeLimit)
        {
            timeUpFired = true;
            TimeUp?.Invoke();
        }
    }

    public float GetBase(PogoStat stat)
    {
        switch (stat)
        {
            case PogoStat.SpitDamage: return spitDamage;
            case PogoStat.FireRate: return fireRate;
            case PogoStat.CritChance: return critChance;
            case PogoStat.CritMultiplier: return critMultiplier;
            case PogoStat.Range: return range;
            case PogoStat.ProjectileSpeed: return projectileSpeed;
            case PogoStat.SpitArea: return spitArea;
            case PogoStat.SpitSize: return spitSize;
            case PogoStat.Projectiles: return projectiles;
            case PogoStat.MoveSpeed: return moveSpeed;
            case PogoStat.AirJumps: return airJumps;
            case PogoStat.DashDistance: return dashDistance;
            case PogoStat.DashCooldown: return dashCooldown;
            case PogoStat.DashSpeed: return dashSpeed;
            case PogoStat.PickupRadius: return pickupRadius;
            case PogoStat.Regeneration: return regeneration;
            case PogoStat.Shield: return shield;
            case PogoStat.XpGain: return xpGain;
            default: return 0f;   // MaxHealth es un bono sobre la vida de Pogoman
        }
    }

    public float Get(PogoStat stat) => Preview(stat, 0f, 0f);

    // Valor que tendría la stat si se le sumara esto (para mostrar "15 → 18" en las mejoras).
    public float Preview(PogoStat stat, float extraFlat, float extraPercent)
    {
        flatBonus.TryGetValue(stat, out float flat);
        percentBonus.TryGetValue(stat, out float percent);
        float value = (GetBase(stat) + flat + extraFlat) * (1f + percent + extraPercent);
        if (multiplier.TryGetValue(stat, out float m))
            value *= m;

        switch (stat)
        {
            case PogoStat.CritChance: return Mathf.Clamp01(value);
            case PogoStat.AirJumps: return Mathf.Clamp(Mathf.Round(value), 0f, 10f);
            case PogoStat.Projectiles: return Mathf.Clamp(Mathf.Round(value), 1f, 12f);
            case PogoStat.DashCooldown: return Mathf.Max(minDashCooldown, value);
            default: return Mathf.Max(0f, value);
        }
    }

    public void AddFlat(PogoStat stat, float amount)
    {
        flatBonus.TryGetValue(stat, out float current);
        flatBonus[stat] = current + amount;
        Changed?.Invoke();
    }

    public void Multiply(PogoStat stat, float factor)
    {
        multiplier.TryGetValue(stat, out float current);
        multiplier[stat] = (current == 0f ? 1f : current) * factor;
        Changed?.Invoke();
    }

    public void AddPercent(PogoStat stat, float amount)
    {
        percentBonus.TryGetValue(stat, out float current);
        percentBonus[stat] = current + amount;
        Changed?.Invoke();
    }

    // ---- Experiencia ----
    public void AddXp(float amount)
    {
        Xp += amount * Get(PogoStat.XpGain);
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            PendingLevelUps++;
            LevelUp?.Invoke(Level);
        }
    }

    // La pantalla de mejoras avisa cuando ya se eligió una.
    public void ConsumeLevelUp() => PendingLevelUps = Mathf.Max(0, PendingLevelUps - 1);

    public void RegisterDamage(float amount, bool crit)
    {
        DamageDealt += amount;
        BiggestHit = Mathf.Max(BiggestHit, amount);
        if (crit)
            CritHits++;
    }

    public void RegisterKill() => Kills++;

    public void AddCoins(int amount)
    {
        Coins += amount;
        if (amount > 0)
            CoinsCollected += amount;
        CoinsChanged?.Invoke(Coins);
    }

    // Para la tienda/cofres: devuelve false si no alcanza.
    public bool SpendCoins(int amount)
    {
        if (amount > Coins)
            return false;
        Coins -= amount;
        CoinsChanged?.Invoke(Coins);
        return true;
    }
}
