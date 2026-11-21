using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Lee datos que los scripts de juego guardan como privados (vida de PogoPlayer
// y Zombie, llaves requeridas de KeyManager...) SIN modificar esos scripts.
//
// Si algún día esos scripts exponen propiedades públicas (por ejemplo
// "public float CurrentHealth"), basta con cambiar este archivo.
public static class PogoBridge
{
    private const BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<(Type, string), FieldInfo> cache = new Dictionary<(Type, string), FieldInfo>();

    private static FieldInfo Field(Type type, string name)
    {
        var key = (type, name);
        if (cache.TryGetValue(key, out FieldInfo cached))
            return cached;

        FieldInfo found = null;
        for (Type t = type; t != null && found == null; t = t.BaseType)
            found = t.GetField(name, Flags);

        cache[key] = found;
        return found;
    }

    public static float GetFloat(object target, string field, float fallback = 0f)
    {
        if (target == null || (target is UnityEngine.Object o && o == null))
            return fallback;

        FieldInfo f = Field(target.GetType(), field);
        return f != null && f.FieldType == typeof(float) ? (float)f.GetValue(target) : fallback;
    }

    public static int GetInt(object target, string field, int fallback = 0)
    {
        if (target == null || (target is UnityEngine.Object o && o == null))
            return fallback;

        FieldInfo f = Field(target.GetType(), field);
        return f != null && f.FieldType == typeof(int) ? (int)f.GetValue(target) : fallback;
    }

    public static bool GetBool(object target, string field, bool fallback = false)
    {
        if (target == null || (target is UnityEngine.Object o && o == null))
            return fallback;

        FieldInfo f = Field(target.GetType(), field);
        return f != null && f.FieldType == typeof(bool) ? (bool)f.GetValue(target) : fallback;
    }

    // Cualquier otro tipo de campo (Vector2, Transform...).
    public static T Get<T>(object target, string field, T fallback = default)
    {
        if (target == null || (target is UnityEngine.Object o && o == null))
            return fallback;

        FieldInfo f = Field(target.GetType(), field);
        return f != null && typeof(T).IsAssignableFrom(f.FieldType) ? (T)f.GetValue(target) : fallback;
    }

    public static void SetFloat(object target, string field, float value)
    {
        if (target == null || (target is UnityEngine.Object o && o == null))
            return;

        FieldInfo f = Field(target.GetType(), field);
        if (f != null && f.FieldType == typeof(float))
            f.SetValue(target, value);
    }

    public static void Set<T>(object target, string field, T value)
    {
        if (target == null || (target is UnityEngine.Object o && o == null))
            return;

        FieldInfo f = Field(target.GetType(), field);
        if (f != null && f.FieldType.IsAssignableFrom(typeof(T)))
            f.SetValue(target, value);
    }

    // ---- Atajos para personajes (PogoPlayer, Zombie y cualquier Enemy con los mismos campos) ----
    public static float Health(Component c) => GetFloat(c, "currentHealth");
    public static float MaxHealth(Component c) => Mathf.Max(1f, GetFloat(c, "maxHealth", 100f));
    public static bool IsDead(Component c) => GetBool(c, "isDead");

    // ---- Estado de PogoPlayer ----
    public static bool IsGrounded(Component player) => GetBool(player, "grounded");
    public static bool IsCtrlHeld(Component player) => GetBool(player, "ctrlHeld");
}
