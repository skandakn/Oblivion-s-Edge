using UnityEngine;

namespace OblivionsEdge.Core
{
    /// <summary>
    /// Supported damage types for combat interactions, elemental resistances, and status effects.
    /// </summary>
    public enum DamageType
    {
        Physical,
        Energy,
        Thermal,
        Toxic,
        EMP
    }

    /// <summary>
    /// Payload structure containing all contextual data for a damage event.
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public GameObject Source;
        public Vector2 HitPoint;
        public Vector2 HitDirection;
        public float KnockbackForce;
        public bool IsCritical;

        public DamageInfo(float amount, DamageType type, GameObject source, Vector2 hitPoint, Vector2 hitDirection, float knockbackForce = 0f, bool isCritical = false)
        {
            Amount = amount;
            Type = type;
            Source = source;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            KnockbackForce = knockbackForce;
            IsCritical = isCritical;
        }
    }

    /// <summary>
    /// Core interface for any entity capable of taking damage (Player, Enemies, Bosses, Barrels, Turrets).
    /// </summary>
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }

        void TakeDamage(DamageInfo damageInfo);
        void Heal(float amount);

        event System.Action<DamageInfo> OnDamageTaken;
        event System.Action<float, float> OnHealthChanged; // (current, max)
        event System.Action<DamageInfo> OnDied;
    }
}
