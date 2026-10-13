using NUnit.Framework;
using OblivionsEdge.Puzzles;
using UnityEngine;

namespace OblivionsEdge.Tests.EditMode
{
    [TestFixture]
    public sealed class HandlePuzzleControllerTests
    {
        private GameObject _gameObject;
        private HandlePuzzleController _puzzle;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("HandlePuzzleTest");
            _puzzle = _gameObject.AddComponent<HandlePuzzleController>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        [Test]
        public void CorrectPattern_UnlocksHandleAndRaisesCompletionOnce()
        {
            int completionCount = 0;
            _puzzle.OnHandleUnlocked += () => completionCount++;

            _puzzle.RotateRing(1);
            _puzzle.RotateRing(2, 2);

            Assert.That(_puzzle.IsSolved, Is.True);
            Assert.That(_puzzle.HandleUnlocked, Is.True);
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(_puzzle.RotateRing(0), Is.False, "Solved puzzles ignore repeated input.");
            Assert.That(completionCount, Is.EqualTo(1));
        }

        [Test]
        public void IncorrectPattern_RemainsInProgressAndUsesModularRotation()
        {
            _puzzle.RotateRing(0, -1);

            Assert.That(_puzzle.IsSolved, Is.False);
            Assert.That(_puzzle.State, Is.EqualTo(PuzzleState.InProgress));
            Assert.That(_puzzle.GetCurrentRotationDegrees(0), Is.EqualTo(270f));
        }

        [Test]
        public void Reset_RestoresInitialPatternAndRelocksHandle()
        {
            _puzzle.ForceSolve();
            _puzzle.ResetPuzzle();

            Assert.That(_puzzle.IsSolved, Is.False);
            Assert.That(_puzzle.HandleUnlocked, Is.False);
            Assert.That(_puzzle.State, Is.EqualTo(PuzzleState.Unsolved));
            Assert.That(_puzzle.GetCurrentRotationDegrees(0), Is.EqualTo(0f));
            Assert.That(_puzzle.GetCurrentRotationDegrees(1), Is.EqualTo(0f));
            Assert.That(_puzzle.GetCurrentRotationDegrees(2), Is.EqualTo(0f));
        }

        [Test]
        public void Solved_EmitsOnPuzzleCompleted_AndUnlocksDeathstormHandleArtifact()
        {
            bool puzzleCompletedFired = false;
            string unlockedArtifact = null;

            _puzzle.OnPuzzleCompleted += () => puzzleCompletedFired = true;
            _puzzle.OnArtifactUnlocked += (id) => unlockedArtifact = id;

            _puzzle.ForceSolve();

            Assert.That(puzzleCompletedFired, Is.True, "OnPuzzleCompleted must be emitted upon puzzle completion.");
            Assert.That(unlockedArtifact, Is.EqualTo(HandlePuzzleController.HandleArtifactId));
            Assert.That(_puzzle.UnlockedArtifactId, Is.EqualTo("deathstorm_handle"));
            Assert.That(_puzzle.IsHandleUnlocked, Is.True);
        }

        [Test]
        public void DiscreteStatePersistence_GetAndSetDiscreteState_RestoresPatternExactly()
        {
            // Scramble rings
            _puzzle.RotateRing(0, 3); // 270° (step 3)
            _puzzle.RotateRing(1, 2); // 180° (step 2)
            _puzzle.RotateRing(2, 1); // 90° (step 1)

            int[] saved = _puzzle.GetDiscreteState();
            Assert.That(saved, Is.EqualTo(new[] { 3, 2, 1 }));

            // Reset puzzle
            _puzzle.ResetPuzzle();
            Assert.That(_puzzle.GetDiscreteState(), Is.EqualTo(new[] { 0, 0, 0 }));

            // Restore discrete state
            _puzzle.SetDiscreteState(saved);
            Assert.That(_puzzle.GetDiscreteState(), Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(_puzzle.GetCurrentRotationDegrees(0), Is.EqualTo(270f));
            Assert.That(_puzzle.GetCurrentRotationDegrees(1), Is.EqualTo(180f));
            Assert.That(_puzzle.GetCurrentRotationDegrees(2), Is.EqualTo(90f));
        }

        [Test]
        public void FortyFiveDegreeMode_OperatesWithEightDiscreteSteps()
        {
            _puzzle.SetStepsPerRevolution(8);

            Assert.That(_puzzle.StepsPerRevolution, Is.EqualTo(8));
            Assert.That(_puzzle.StepAngle, Is.EqualTo(45f));

            _puzzle.RotateRing(0, 1);
            Assert.That(_puzzle.GetCurrentRotationDegrees(0), Is.EqualTo(45f));

            _puzzle.RotateRing(0, 3);
            Assert.That(_puzzle.GetCurrentRotationDegrees(0), Is.EqualTo(180f));

            _puzzle.RotateRing(0, -1);
            Assert.That(_puzzle.GetCurrentRotationDegrees(0), Is.EqualTo(135f));
        }

        [Test]
        public void CipherKey_MatchesShipLogConfiguration()
        {
            Assert.That(_puzzle.CipherKeyDescription, Does.Contain("Relay Ship Log Cipher"));
            Assert.That(_puzzle.GetTargetRotationDegrees(0), Is.EqualTo(0f), "Outer ring targets 0° (North)");
            Assert.That(_puzzle.GetTargetRotationDegrees(1), Is.EqualTo(90f), "Middle ring targets 90° (East)");
            Assert.That(_puzzle.GetTargetRotationDegrees(2), Is.EqualTo(180f), "Inner ring targets 180° (South)");
        }

        [Test]
        public void ResetRing_ResetsSingleRingBackToInitialStep()
        {
            _puzzle.RotateRing(1, 2); // 180°
            Assert.That(_puzzle.GetCurrentRotationDegrees(1), Is.EqualTo(180f));

            _puzzle.ResetRing(1);
            Assert.That(_puzzle.GetCurrentRotationDegrees(1), Is.EqualTo(0f));
        }
    }
}
