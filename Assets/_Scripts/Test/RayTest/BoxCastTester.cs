using System;
using UnityEngine;

namespace Test.RayTest
{
    public class BoxCastTester : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private LayerMask whatIsEnemy;

        private void OnDrawGizmos()
        {
            RaycastHit hit;
            bool isHit = Physics.BoxCast(transform.position,
                transform.lossyScale * 0.5f,
                transform.forward, 
                out hit, 
                transform.rotation, 
                maxDistance, 
                whatIsEnemy);

            if (isHit)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(Vector3.forward * hit.distance, transform.lossyScale);
                
                Gizmos.matrix = oldMatrix;
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, transform.forward * maxDistance);
            }
        }
    }
}