using OblivionsEdge.Core;

namespace OblivionsEdge.Boss
{
    /// <summary>
    /// Contract defining boss behaviors, phase progression, enrage mechanics, and arena orchestration.
    /// To be implemented by Member 3 (Boss & AI mechanics).
    /// </summary>
    public interface IBossController : IDamageable
    {
        string BossId { get; }
        string DisplayName { get; }
        BossPhase CurrentPhase { get; }
        bool IsInvulnerable { get; }
        float EnrageTimerRemaining { get; }

        void StartBossEncounter();
        void TransitionToPhase(BossPhase newPhase);
        void SetInvulnerable(bool invulnerable);
        void TriggerStun(float durationSeconds);
        void RegisterPhaseListener(IBossPhaseListener listener);
        void UnregisterPhaseListener(IBossPhaseListener listener);

        event System.Action<BossPhase> OnPhaseChanged;
        event System.Action OnBossEnraged;
        event System.Action OnBossStunned;
        event System.Action OnBossDefeated;
    }
}
