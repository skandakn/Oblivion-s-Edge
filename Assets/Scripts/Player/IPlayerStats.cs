using OblivionsEdge.Core;

namespace OblivionsEdge.Player
{
    /// <summary>
    /// Contract defining player survival vital statistics (Stamina, Sanity, Oxygen).
    /// Health is governed by OblivionsEdge.Core.IDamageable.
    /// </summary>
    public interface IPlayerStats : IDamageable
    {
        float CurrentStamina { get; }
        float MaxStamina { get; }

        float CurrentSanity { get; }
        float MaxSanity { get; }

        float CurrentOxygen { get; }
        float MaxOxygen { get; }

        bool ConsumeStamina(float amount);
        void RestoreStamina(float amount);

        void ModifySanity(float delta);
        void ModifyOxygen(float delta);

        event System.Action<float, float> OnStaminaChanged; // (current, max)
        event System.Action<float, float> OnSanityChanged;  // (current, max)
        event System.Action<float, float> OnOxygenChanged;  // (current, max)
        event System.Action OnSanityDepleted;
        event System.Action OnOxygenDepleted;
    }
}
