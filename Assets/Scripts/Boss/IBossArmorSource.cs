using System;

namespace OblivionsEdge.Boss
{
    /// <summary>
    /// Published telemetry contract for boss armor status and depletion notifications.
    /// Implemented by <see cref="BossHealth"/> and observed by UI HUD systems.
    /// </summary>
    public interface IBossArmorSource
    {
        /// <summary>Current armor health remaining before weak-point exposure.</summary>
        float CurrentArmor { get; }

        /// <summary>Maximum configurable armor capacity.</summary>
        float MaxArmor { get; }

        /// <summary>Broadcasts when armor changes (current, max).</summary>
        event Action<float, float> OnArmorChanged;

        /// <summary>Broadcasts once when armor reaches zero and fractures.</summary>
        event Action OnArmorDepleted;
    }
}
