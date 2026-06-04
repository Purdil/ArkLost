using UnityEngine;

namespace Players
{
    public interface IControlMovement
    {
        bool CanManualMove { get; set; }
        void SetMovementDirection(Vector2 inputDirection);
        void SetMovementVelocity(Vector3 velocity); //수동으로 속도 조절기능
        void RotateTo(Vector3 direction);
    }
}