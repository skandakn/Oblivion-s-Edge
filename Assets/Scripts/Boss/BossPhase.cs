namespace OblivionsEdge.Boss
{
    /// <summary>
    /// Discrete combat states and stages for boss encounters.
    /// </summary>
    public enum BossPhase
    {
        Dormant,
        Intro,
        Phase1,
        Phase2,
        Phase3,
        VulnerableStun,
        Enraged,
        Defeated
    }

    /// <summary>
    /// Listener contract for arena hazards, music transitions, and environmental locks reacting to boss phase shifts.
    /// </summary>
    public interface IBossPhaseListener
    {
        void OnBossPhaseEntered(BossPhase phase);
    }
}
