using System;
using UnityEngine;

namespace Test.RayTest
{
    public class RayCastTester : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private LayerMask whatIsEnemy;
        private void OnDrawGizmos()
        {
            RaycastHit hit;
            
            bool isHit = Physics.Raycast(transform.position, 
                transform.forward, out hit, maxDistance, whatIsEnemy);

            if (isHit)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, transform.forward * hit.distance);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, transform.forward * maxDistance);
            }
        }
    }
}