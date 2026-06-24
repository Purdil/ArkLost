using GGMLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Scripts.Enemies
{
    public class NavMovement : MonoBehaviour, IModule, INavMovement
    {
        public NavMeshAgent NavMeshAgent { get; private set; }
        public Vector3 Velocity
        {
            get => NavMeshAgent != null ? NavMeshAgent.velocity : Vector3.zero;
            set
            {
                if(NavMeshAgent != null)
                    NavMeshAgent.velocity = value;
            }
        }

        public float Speed
        {
            get => NavMeshAgent != null ? NavMeshAgent.speed : 0f;
            set
            {
                if(NavMeshAgent != null)
                    NavMeshAgent.speed = value;
            }
        }

        public bool IsStopped
        {
            get => NavMeshAgent != null && NavMeshAgent.isStopped;
            set
            {
                if(NavMeshAgent != null)
                    NavMeshAgent.isStopped = value;
            }
        }

        public bool IsArrived
        {
            get
            {
                if(NavMeshAgent == null || NavMeshAgent.pathPending) return false;

                return NavMeshAgent.remainingDistance <= NavMeshAgent.stoppingDistance * 0.5f
                 && (!NavMeshAgent.hasPath || NavMeshAgent.velocity.sqrMagnitude <= 0.01f);      
            }
        }

        public void SetDestination(Vector3 destination)
        {
            NavMeshAgent.SetDestination(destination);
        }

        public void StopImmediately()
        {
            NavMeshAgent.velocity = Vector3.zero;
            if (NavMeshAgent.enabled)
            {
                NavMeshAgent.ResetPath();
                
            }
        }

        public void Initialize(ModuleOwner owner)
        {
            NavMeshAgent = owner.GetComponent<NavMeshAgent>(); 
            Debug.Assert(NavMeshAgent != null, "NavMeshAgent Owner should have a NavMeshAgent");
            NavMeshAgent.SetDestination(owner.transform.position);
            NavMeshAgent.ResetPath();
        }
        
    }
}
