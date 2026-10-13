namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Lifecycle state of a puzzle or environmental challenge.
    /// </summary>
    public enum PuzzleState
    {
        Unsolved,
        InProgress,
        Solved,
        Failed,
        Disabled
    }

    /// <summary>
    /// Architectural archetype of a puzzle encounter.
    /// </summary>
    public enum PuzzleType
    {
        PowerGridReroute,
        KeycardBiometricAccess,
        SequenceMemoryTerminal,
        PressurePlateLock,
        FrequencyTuner,
        AirlockPressureDecontamination
    }
}
