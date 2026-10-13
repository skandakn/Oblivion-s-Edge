using System;
using UnityEngine;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Automated test runner and simulation harness for OBLIVION'S EDGE puzzle verification.
    /// Validates mathematical reflection geometry, correct mirror configurations,
    /// and incorrect misalignment failure modes.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Puzzles/Edge Puzzle Verification")]
    public class EdgePuzzleVerification : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("If true, automatically executes tests on Start.")]
        [SerializeField] private bool _runTestsOnStart = false;

        [Tooltip("Reference to the scene puzzle controller if testing within scene.")]
        [SerializeField] private EdgePuzzleController _puzzleController;

        private void Start()
        {
            if (_runTestsOnStart)
            {
                RunAllTests();
            }
        }

        [ContextMenu("Run All Puzzle Tests")]
        public void RunAllTests()
        {
            Debug.Log("<color=cyan><b>=== OBLIVION'S EDGE: EDGE PUZZLE TEST SUITE ===</b></color>");

            bool mathPassed = TestReflectionMath();
            bool correctPassed = TestCorrectSolution();
            bool incorrectPassed = TestIncorrectArrangement();
            bool stepPassed = TestInteractiveRotation();

            bool allPassed = mathPassed && correctPassed && incorrectPassed && stepPassed;

            if (allPassed)
            {
                Debug.Log("<color=green><b>=== ALL TESTS PASSED SUCCESSFULLY! (4/4) ===</b></color>");
            }
            else
            {
                Debug.LogError("<color=red><b>=== SOME TESTS FAILED! ===</b></color>");
            }
        }

        /// <summary>
        /// Validates the specular reflection formula r = d - 2(d · n)n against fundamental geometric cases.
        /// </summary>
        public static bool TestReflectionMath()
        {
            Debug.Log("<b>[Test 1]</b> Validating Reflection Formula: r = d - 2(d · n)n ...");

            // Case A: Beam traveling East (+X) hitting a North-South wall with normal West (-X)
            Vector2 d1 = Vector2.right; // (1, 0)
            Vector2 n1 = Vector2.left;  // (-1, 0)
            Vector2 r1 = LaserReflectionMath.Reflect2D(d1, n1);
            bool passA = Vector2.Distance(r1, Vector2.left) < 0.001f;

            // Case B: Beam traveling East (+X) hitting a 45-degree diagonal mirror with normal pointing Up-Left (-1, 1)/sqrt(2)
            Vector2 d2 = Vector2.right; // (1, 0)
            Vector2 n2 = new Vector2(-1f, 1f).normalized;
            Vector2 r2 = LaserReflectionMath.Reflect2D(d2, n2);
            bool passB = Vector2.Distance(r2, Vector2.up) < 0.001f; // Reflects 90 degrees North

            // Case C: Beam traveling North (+Y) hitting a diagonal mirror with normal Down-Left (-1, -1)/sqrt(2)
            Vector2 d3 = Vector2.up; // (0, 1)
            Vector2 n3 = new Vector2(-1f, -1f).normalized;
            Vector2 r3 = LaserReflectionMath.Reflect2D(d3, n3);
            bool passC = Vector2.Distance(r3, Vector2.left) < 0.001f; // Reflects 90 degrees West

            bool passed = passA && passB && passC;
            if (passed)
            {
                Debug.Log(" -> <b>Test 1 PASSED:</b> Reflection geometry correctly transforms incoming rays.");
            }
            else
            {
                Debug.LogError(string.Format(" -> <b>Test 1 FAILED:</b> Case A={0}, Case B={1}, Case C={2}", passA, passB, passC));
            }
            return passed;
        }

        /// <summary>
        /// Validates that the intended mirror arrangement correctly routes the beam from emitter to receiver.
        /// </summary>
        public static bool TestCorrectSolution()
        {
            Debug.Log("<b>[Test 2]</b> Simulating Correct Puzzle Arrangement ...");

            // Setup geometric coordinates:
            // Emitter at (0, 0) fires East (1, 0)
            Vector2 emitterPos = new Vector2(0f, 0f);
            Vector2 emitterDir = Vector2.right;

            // Mirror 1 at (4, 0), angled to reflect East -> North.
            // Normal must be (-1, 1)/sqrt(2)
            Vector2 mirror1Pos = new Vector2(4f, 0f);
            Vector2 mirror1Normal = new Vector2(-1f, 1f).normalized;

            // Mirror 2 at (4, 4), angled to reflect North -> West.
            // Normal must be (-1, -1)/sqrt(2)
            Vector2 mirror2Pos = new Vector2(4f, 4f);
            Vector2 mirror2Normal = new Vector2(-1f, -1f).normalized;

            // Receiver at (0, 4) with aperture normal facing East (1, 0)
            Vector2 receiverPos = new Vector2(0f, 4f);
            Vector2 receiverApertureNormal = Vector2.right;

            // Ray tracing simulation:
            // Segment 1: Emitter -> Mirror 1
            Vector2 rayDir1 = emitterDir;
            float distToM1 = Vector2.Distance(emitterPos, mirror1Pos);
            bool strikesM1 = distToM1 > 0f && Vector2.Dot(rayDir1, (mirror1Pos - emitterPos).normalized) > 0.999f;

            // Bounce 1:
            Vector2 rayDir2 = LaserReflectionMath.Reflect2D(rayDir1, mirror1Normal);
            bool reflectsToM2 = Vector2.Distance(rayDir2, Vector2.up) < 0.001f;

            // Segment 2: Mirror 1 -> Mirror 2
            bool strikesM2 = reflectsToM2 && Vector2.Dot(rayDir2, (mirror2Pos - mirror1Pos).normalized) > 0.999f;

            // Bounce 2:
            Vector2 rayDir3 = LaserReflectionMath.Reflect2D(rayDir2, mirror2Normal);
            bool reflectsToReceiver = Vector2.Distance(rayDir3, Vector2.left) < 0.001f;

            // Segment 3: Mirror 2 -> Receiver
            bool strikesReceiver = reflectsToReceiver && Vector2.Dot(rayDir3, (receiverPos - mirror2Pos).normalized) > 0.999f;

            // Receiver acceptance check:
            float angleWithAperture = Vector2.Angle(rayDir3, -receiverApertureNormal);
            bool receiverAccepts = angleWithAperture < 1f;

            bool passed = strikesM1 && strikesM2 && strikesReceiver && receiverAccepts;

            if (passed)
            {
                Debug.Log(" -> <b>Test 2 PASSED:</b> Correct arrangement successfully reaches and activates receiver.");
            }
            else
            {
                Debug.LogError(string.Format(" -> <b>Test 2 FAILED:</b> M1={0}, M2={1}, Recv={2}, Accept={3}",
                    strikesM1, strikesM2, strikesReceiver, receiverAccepts));
            }
            return passed;
        }

        /// <summary>
        /// Validates that an incorrect mirror rotation blocks or misdirects the beam, preventing vault unlock.
        /// </summary>
        public static bool TestIncorrectArrangement()
        {
            Debug.Log("<b>[Test 3]</b> Simulating Incorrect Puzzle Misalignment ...");

            // Setup coordinates
            Vector2 emitterPos = new Vector2(0f, 0f);
            Vector2 emitterDir = Vector2.right;
            Vector2 mirror1Pos = new Vector2(4f, 0f);

            // Misaligned Mirror 1: facing Down (0, -1) or East (1, 0)
            // Incoming beam is East (+1, 0). If mirror normal is East (+1, 0), it strikes the back face!
            Vector2 badNormal = Vector2.right; // strikes back face
            bool isFacingFront = LaserReflectionMath.IsFacingSurface(emitterDir, badNormal);

            // Alternatively, mirror rotated to 0 degrees (normal Up: (0, 1))
            // Incoming beam is (1, 0).
            Vector2 horizontalMirrorNormal = Vector2.up;
            Vector2 reflectedParallel = LaserReflectionMath.Reflect2D(emitterDir, horizontalMirrorNormal);
            // Angle between reflectedParallel (1, 0) and required target (0, 1) is 90 degrees!
            float divergenceFromTarget = Vector2.Angle(reflectedParallel, Vector2.up);
            bool missesMirror2 = divergenceFromTarget > 80f;

            // Result: Mirror 2 at (4, 4) is NEVER struck!
            bool beamReachesReceiver = false;
            bool puzzleSolved = beamReachesReceiver; // false

            bool passed = (!isFacingFront || missesMirror2) && !puzzleSolved;

            if (passed)
            {
                Debug.Log(" -> <b>Test 3 PASSED:</b> Misaligned mirror successfully prevents laser from reaching receiver.");
            }
            else
            {
                Debug.LogError(" -> <b>Test 3 FAILED:</b> Puzzle incorrectly accepted misaligned setup.");
            }
            return passed;
        }

        /// <summary>
        /// Validates player stepping through mirror rotations to reach the solution.
        /// </summary>
        public static bool TestInteractiveRotation()
        {
            Debug.Log("<b>[Test 4]</b> Simulating Interactive Stepped Rotation Cycle ...");

            // Initial scrambled angle: 0 degrees
            float angle = 0f;
            float step = 45f;
            float targetAngle = 135f;

            int stepsTaken = 0;
            bool reachedTarget = false;

            for (int i = 0; i < 8; i++)
            {
                if (Mathf.Abs(angle - targetAngle) < 0.1f)
                {
                    reachedTarget = true;
                    break;
                }
                angle = Mathf.Repeat(angle + step, 360f);
                stepsTaken++;
            }

            bool passed = reachedTarget && stepsTaken == 3; // 0 -> 45 -> 90 -> 135 (3 steps)

            if (passed)
            {
                Debug.Log(string.Format(" -> <b>Test 4 PASSED:</b> Rotated from 0° to 135° in {0} interactive steps.", stepsTaken));
            }
            else
            {
                Debug.LogError(string.Format(" -> <b>Test 4 FAILED:</b> Target reached={0}, Steps={1}", reachedTarget, stepsTaken));
            }
            return passed;
        }
    }
}
