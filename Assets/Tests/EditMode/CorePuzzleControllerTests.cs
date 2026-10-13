using NUnit.Framework;
using OblivionsEdge.Puzzles;
using UnityEngine;

namespace OblivionsEdge.Tests.EditMode
{
    /// <summary>
    /// Unit and EditMode verification suite for Member 8's <see cref="CorePuzzleController"/>.
    /// Tests all four <see cref="Vector2"/> gravity directions, 90-degree rotations,
    /// correct alignment, non-soft-locking incorrect feedback, and Boss locked-state gating.
    /// </summary>
    public sealed class CorePuzzleControllerTests
    {
        private GameObject _rootObject;
        private GameObject _forceArrowObject;
        private GameObject _socketObject;
        private CorePuzzleController _puzzle;

        [SetUp]
        public void SetUp()
        {
            _rootObject = new GameObject("CorePuzzleTest");
            _forceArrowObject = new GameObject("ForceArrow");
            _forceArrowObject.transform.SetParent(_rootObject.transform, false);
            _socketObject = new GameObject("SocketIndicator");
            _socketObject.transform.SetParent(_rootObject.transform, false);

            _puzzle = _rootObject.AddComponent<CorePuzzleController>();
            _puzzle.SetVisualTransforms(_forceArrowObject.transform, _socketObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void AllFourDirections_Rotate90DegreesClockwiseAndCounterClockwiseCorrectly()
        {
            _puzzle.SetInitialGravityDirection(Vector2.up);
            _puzzle.SetRequiredSocketDirection(Vector2.down);
            _puzzle.ResetPuzzle();

            // Starting at Up (0, 1) -> 0 degrees
            Assert.That(_puzzle.CurrentGravityDirection, Is.EqualTo(Vector2.up));
            Assert.That(_puzzle.GetForceArrowAngleDegrees(), Is.EqualTo(0f).Within(0.01f));

            // 90° clockwise -> Right (1, 0) -> 270 degrees
            _puzzle.RotateClockwise();
            Assert.That(_puzzle.CurrentGravityDirection, Is.EqualTo(Vector2.right));
            Assert.That(_puzzle.GetForceArrowAngleDegrees(), Is.EqualTo(270f).Within(0.01f));

            // 90° clockwise -> Down (0, -1) -> 180 degrees
            _puzzle.RotateClockwise();
            Assert.That(_puzzle.CurrentGravityDirection, Is.EqualTo(Vector2.down));
            Assert.That(_puzzle.GetForceArrowAngleDegrees(), Is.EqualTo(180f).Within(0.01f));

            // 90° clockwise -> Left (-1, 0) -> 90 degrees
            _puzzle.RotateClockwise();
            Assert.That(_puzzle.CurrentGravityDirection, Is.EqualTo(Vector2.left));
            Assert.That(_puzzle.GetForceArrowAngleDegrees(), Is.EqualTo(90f).Within(0.01f));

            // 90° clockwise -> wraps back to Up (0, 1) -> 0 degrees
            _puzzle.RotateClockwise();
            Assert.That(_puzzle.CurrentGravityDirection, Is.EqualTo(Vector2.up));
            Assert.That(_puzzle.GetForceArrowAngleDegrees(), Is.EqualTo(0f).Within(0.01f));

            // 90° counter-clockwise -> Left (-1, 0) -> 90 degrees
            _puzzle.RotateCounterClockwise();
            Assert.That(_puzzle.CurrentGravityDirection, Is.EqualTo(Vector2.left));
            Assert.That(_puzzle.GetForceArrowAngleDegrees(), Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void SelectDirection_SupportsAllFourCardinalVector2ValuesAndTargetSockets()
        {
            _puzzle.SetBossConditionMet(true);

            foreach (Vector2 requiredSocket in CorePuzzleController.CardinalDirections)
            {
                // Pick a starting direction different from the required socket direction
                Vector2 initialDirection = CorePuzzleController.RotateVector90Clockwise(requiredSocket, 1);
                _puzzle.SetInitialGravityDirection(initialDirection);
                _puzzle.SetRequiredSocketDirection(requiredSocket);
                _puzzle.ResetPuzzle();
                _puzzle.SetBossConditionMet(true);

                Assert.That(_puzzle.IsSolved, Is.False);
                Assert.That(_puzzle.CoreExtractionUnlocked, Is.False);

                bool selected = _puzzle.SelectDirection(requiredSocket);

                Assert.That(selected, Is.True);
                Assert.That(_puzzle.CurrentGravityDirection, Is.EqualTo(requiredSocket));
                Assert.That(_puzzle.GetAlignmentDot(), Is.EqualTo(1f).Within(0.001f));
                Assert.That(_puzzle.GetAlignmentAngleDegrees(), Is.EqualTo(0f).Within(0.01f));
                Assert.That(_puzzle.IsDirectionAligned, Is.True);
                Assert.That(_puzzle.IsSolved, Is.True);
                Assert.That(_puzzle.CoreExtractionUnlocked, Is.True);
            }
        }

        [Test]
        public void LockedState_PreventsCoreExtractionBeforeBossConditionMet()
        {
            int bossBlockedCount = 0;
            int unlockedCount = 0;
            _puzzle.OnExtractionBlockedByBoss += () => bossBlockedCount++;
            _puzzle.OnCoreExtractionUnlocked += () => unlockedCount++;

            _puzzle.SetInitialGravityDirection(Vector2.up);
            _puzzle.SetRequiredSocketDirection(Vector2.down);
            _puzzle.ResetPuzzle();
            _puzzle.SetBossConditionMet(false);

            // Selecting the matching socket direction while boss condition is false must NOT unlock extraction
            _puzzle.SelectDirection(Vector2.down);

            Assert.That(_puzzle.IsDirectionAligned, Is.True);
            Assert.That(_puzzle.IsBossConditionMet, Is.False);
            Assert.That(_puzzle.IsSolved, Is.False);
            Assert.That(_puzzle.CoreExtractionUnlocked, Is.False);
            Assert.That(_puzzle.CanExtractCore(), Is.False);
            Assert.That(_puzzle.TryExtractCore(), Is.False);
            Assert.That(bossBlockedCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(unlockedCount, Is.EqualTo(0));

            // Once Member 5's boss condition is met, evaluating/aligning unlocks Core extraction
            _puzzle.NotifyArmorExposed();
            bool solved = _puzzle.EvaluateCondition();

            Assert.That(solved, Is.True);
            Assert.That(_puzzle.IsBossConditionMet, Is.True);
            Assert.That(_puzzle.IsSolved, Is.True);
            Assert.That(_puzzle.CoreExtractionUnlocked, Is.True);
            Assert.That(_puzzle.CanExtractCore(), Is.True);
            Assert.That(unlockedCount, Is.EqualTo(1));
        }

        [Test]
        public void IncorrectAlignment_ShowsFeedbackWithoutSoftLockingAndAllowsSubsequentSolve()
        {
            int incorrectFeedbackCount = 0;
            int solvedCount = 0;
            GameObject extractedBy = null;

            _puzzle.OnIncorrectAlignment += () => incorrectFeedbackCount++;
            _puzzle.OnCoreExtractionUnlocked += () => solvedCount++;
            _puzzle.OnCoreExtracted += interactor => extractedBy = interactor;

            _puzzle.SetInitialGravityDirection(Vector2.up);
            _puzzle.SetRequiredSocketDirection(Vector2.left);
            _puzzle.ResetPuzzle();
            _puzzle.SetBossConditionMet(true);

            // Try two wrong directions: Right (1, 0) and Down (0, -1)
            _puzzle.SelectDirection(Vector2.right);
            Assert.That(_puzzle.IsSolved, Is.False);
            Assert.That(_puzzle.State, Is.EqualTo(PuzzleState.InProgress));
            Assert.That(_puzzle.CoreExtractionUnlocked, Is.False);
            Assert.That(incorrectFeedbackCount, Is.EqualTo(1));
            Assert.That(_puzzle.LastFeedbackMessage, Does.Contain("MISALIGNED"));

            _puzzle.SelectDirection(Vector2.down);
            Assert.That(_puzzle.IsSolved, Is.False);
            Assert.That(_puzzle.State, Is.EqualTo(PuzzleState.InProgress));
            Assert.That(_puzzle.CoreExtractionUnlocked, Is.False);
            Assert.That(incorrectFeedbackCount, Is.EqualTo(2));

            // Puzzle is not soft-locked: selecting Left (-1, 0) solves and unlocks extraction
            _puzzle.SelectDirection(Vector2.left);
            Assert.That(_puzzle.IsSolved, Is.True);
            Assert.That(_puzzle.State, Is.EqualTo(PuzzleState.Solved));
            Assert.That(_puzzle.CoreExtractionUnlocked, Is.True);
            Assert.That(solvedCount, Is.EqualTo(1));

            // Member 9 extraction hook grants Core once and rejects duplicate extraction
            Assert.That(_puzzle.TryExtractCore(_rootObject), Is.True);
            Assert.That(_puzzle.IsCoreExtracted, Is.True);
            Assert.That(extractedBy, Is.EqualTo(_rootObject));
            Assert.That(_puzzle.TryExtractCore(_rootObject), Is.False);
        }
    }
}
