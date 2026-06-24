using System;
using UnityEngine;

namespace Enemies.Nav
{
    public class WayPoint : MonoBehaviour
    {
        public Vector3 Position => transform.position;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(Position, 0.2f);
        }
    }
}