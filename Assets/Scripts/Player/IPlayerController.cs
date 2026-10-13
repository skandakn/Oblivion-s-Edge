using UnityEngine;

namespace OblivionsEdge.Player
{
    /// <summary>
    /// Contract defining player kinematic control, inputs, and physical posture.
    /// To be implemented by Member 2 (Player & Movement mechanics).
    /// </summary>
    public interface IPlayerController
    {
        Vector2 MoveDirection { get; }
        Vector2 AimDirection { get; }
        bool IsGrounded { get; }
        bool IsMoving { get; }
        bool IsSprinting { get; }
        bool IsCrouching { get; }

        void SetMoveInput(Vector2 direction);
        void SetAimTarget(Vector2 worldPosition);
        void SetSprint(bool isSprinting);
        void SetCrouch(bool isCrouching);
        void TriggerDash();

        void DisableControls();
        void EnableControls();

        event System.Action<Vector2> OnMoved;
        event System.Action OnDashed;
    }
}
