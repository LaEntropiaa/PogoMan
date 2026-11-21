using System;
using System.Collections.Generic;
using UnityEngine;

public enum PogoStat
{
    SpitDamage,
    FireRate,
    CritChance,
    CritMultiplier,
    Range,
    ProjectileSpeed,
}

// Stats del escupitajo y estadísticas de la partida.
// Pensado para el sistema de objetos: un objeto solo tiene que llamar a
//   PlayerStats.Instance.AddFlat(PogoStat.SpitDamage, 5);      // +5 de daño
//   PlayerStats.Instance.AddPercent(PogoStat.FireRate, 0.2f);  // +20% cadencia
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Escupitajo (valores base)")]
    [SerializeField] private float spitDamage = 15f;
    [Tooltip("Disparos por segundo.")]
    [SerializeField] private float fireRate = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float critChance = 0.1f;
    [SerializeField] private float critMultiplier = 2f;
    [SerializeField] private float range = 14f;
    [SerializeField] private float projectileSpeed = 22f;

    private readonly Dictionary<PogoStat, float> flatBonus = new Dictionary<PogoStat, float>();
    private readonly Dictionary<PogoStat, float> percentBonus = new Dictionary<PogoStat, float>();

    public event Action Changed;

    // ---- Estadísticas de la partida ----
    public float RunTime { get; private set; }
    public int Kills { get; private set; }
    public float DamageDealt { get; private set; }
    public int CritHits { get; private set; }
    public float BiggestHit { get; private set; }
    public int Shots { get; set; }
    public bool RunEnded { get; set; }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!RunEnded)
            RunTime += Time.deltaTime;
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
            default: return 0f;
        }
    }

    public float Get(PogoStat stat)
    {
        flatBonus.TryGetValue(stat, out float flat);
        percentBonus.TryGetValue(stat, out float percent);
        float value = (GetBase(stat) + flat) * (1f + percent);

        if (stat == PogoStat.CritChance)
            value = Mathf.Clamp01(value);

        return Mathf.Max(0f, value);
    }

    public void AddFlat(PogoStat stat, float amount)
    {
        flatBonus.TryGetValue(stat, out float current);
        flatBonus[stat] = current + amount;
        Changed?.Invoke();
    }

    public void AddPercent(PogoStat stat, float amount)
    {
        percentBonus.TryGetValue(stat, out float current);
        percentBonus[stat] = current + amount;
        Changed?.Invoke();
    }

    public void RegisterDamage(float amount, bool crit)
    {
        DamageDealt += amount;
        BiggestHit = Mathf.Max(BiggestHit, amount);
        if (crit)
            CritHits++;
    }

    public void RegisterKill() => Kills++;
}
