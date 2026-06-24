using System;
using UnityEngine;

namespace Test.RayTest
{
    public class SphereCastTester : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private LayerMask whatIsEnemy;

        private void OnDrawGizmos()
        {
            RaycastHit hit;
            bool isHit = Physics.SphereCast(transform.position,
                transform.lossyScale.x * 0.5f,
                transform.forward, 
                out hit, 
                maxDistance, 
                whatIsEnemy);

            if (isHit)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position + transform.forward * hit.distance, 
                    transform.lossyScale.x * 0.5f);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, transform.forward * maxDistance);
            }
        }
    }
}