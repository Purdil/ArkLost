using System;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace _Scripts.Boss
{
    public class DetectGroundSensor : MonoBehaviour, IModule
    {
        [SerializeField] private LayerMask whatIsGround;
        [SerializeField] private float rayDistance;
        [SerializeField] private Vector3 rayOffset;
        
        public void Initialize(ModuleOwner owner)
        {
            
        }

        public bool AgentIsGround()
        {
            if (Physics.Raycast(transform.position + rayOffset, Vector3.down, rayDistance, whatIsGround))
            {
                   return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + rayOffset, Vector3.down * rayDistance);
        }
    }
}