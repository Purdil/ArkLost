using UnityEngine;
using UnityEngine.AI;

namespace _Scripts.Enemies
{
    public interface INavMovement
    {
        NavMeshAgent NavMeshAgent { get; }
        Vector3 Velocity { get; set; }
        float Speed { get; set; }
        bool IsStopped { get; set; }
        bool IsArrived { get;  }
        
        void SetDestination(Vector3 destination);
        void StopImmediately();
        
    }
}