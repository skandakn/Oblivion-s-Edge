using OblivionsEdge.Core;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Contract for an individual interactive component within a puzzle (switches, fuses, dials).
    /// </summary>
    public interface IPuzzleElement : IInteractable
    {
        string ElementId { get; }
        bool IsActive { get; }
        int Value { get; }

        void SetActive(bool active);
        void SetValue(int value);
        void ResetElement();

        event System.Action<IPuzzleElement> OnElementStateChanged;
    }

    /// <summary>
    /// Contract defining puzzle controller orchestration, validation logic, and reward trigger.
    /// To be implemented by Member 4 (Puzzles & Environmental Systems).
    /// </summary>
    public interface IPuzzleController
    {
        string PuzzleId { get; }
        PuzzleType Type { get; }
        PuzzleState State { get; }
        bool IsSolved { get; }

        bool EvaluateCondition();
        void ForceSolve();
        void ResetPuzzle();

        event System.Action<PuzzleState> OnPuzzleStateChanged;
        event System.Action OnPuzzleSolved;
        event System.Action OnPuzzleFailed;
    }
}
