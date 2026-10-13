using System;
using UnityEngine;

namespace OblivionsEdge.DeathStorm
{
    // ─────────────────────────────────────────────────────────────────────────
    //  IDeathStormCrafting  –  Public interface consumed by UI and other systems
    //  Owner: Member 10
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Discrete stages of the DeathStorm legendary-weapon crafting ritual.
    /// </summary>
    public enum CraftingStage
    {
        /// <summary>Not yet started; waiting for the Core pickup to trigger the countdown.</summary>
        Idle,
        /// <summary>Stage 1 — four-direction Core alignment puzzle.</summary>
        CoreAlignment,
        /// <summary>Stage 2 — angular Edge synchronization puzzle.</summary>
        EdgeSync,
        /// <summary>Stage 3 — short directional energy-stabilisation sequence.</summary>
        EnergyStabilise,
        /// <summary>All three stages passed; weapon successfully forged.</summary>
        Complete,
        /// <summary>Countdown expired; player may still attempt after surviving the boss rage.</summary>
        TimedOut
    }

    /// <summary>
    /// Public surface of the DeathStorm crafting mini-game.
    /// UI systems must depend only on this interface — never on the concrete class.
    /// </summary>
    public interface IDeathStormCrafting
    {
        // ── State ──────────────────────────────────────────────────────────
        CraftingStage CurrentStage { get; }
        float CountdownRemaining { get; }
        float CountdownTotal { get; }
        bool HasDeathStorm { get; }
        bool IsCountdownRunning { get; }

        // ── Puzzle helpers ─────────────────────────────────────────────────
        /// <summary>Human-readable instruction text for the active puzzle step.</summary>
        string CurrentInstruction { get; }

        /// <summary>
        /// Submit an answer for the active stage.
        /// Accepts a cardinal direction (0=North, 1=East, 2=South, 3=West) for Stage 1,
        /// an angle bucket index for Stage 2, and a directional energy-channel index for Stage 3.
        /// </summary>
        /// <param name="answer">Zero-based integer answer selected by the player.</param>
        /// <returns>True when the answer is correct.</returns>
        bool SubmitAnswer(int answer);

        // ── Events broadcast to UI / audio / boss ──────────────────────────
        /// <summary>Fires when the countdown tick changes; payload is (remaining, total).</summary>
        event Action<float, float> OnTimerTick;

        /// <summary>Fires on stage transition; payload is the new stage.</summary>
        event Action<CraftingStage> OnStageChanged;

        /// <summary>Fires when an incorrect answer is submitted; payload is the time penalty applied.</summary>
        event Action<float> OnWrongAnswer;

        /// <summary>Fires when crafting succeeds (DeathStorm granted).</summary>
        event Action OnCraftingSuccess;

        /// <summary>
        /// Fires when the countdown expires; payload is true if this is the
        /// first expiry (triggers ENRAGE + VOID POUNCE), false for subsequent ones.
        /// </summary>
        event Action<bool> OnCountdownExpired;
    }
}
