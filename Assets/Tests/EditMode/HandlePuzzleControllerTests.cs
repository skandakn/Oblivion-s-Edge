using NUnit.Framework;
using OblivionsEdge.Puzzles;
using UnityEngine;

namespace OblivionsEdge.Tests.EditMode
{
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
    }
}
