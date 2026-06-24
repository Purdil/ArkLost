using Reflex.Core;
using UnityEngine;

namespace Enemies.Nav
{
    public class WayPointManager : MonoBehaviour, IInstaller
    {
        [SerializeField] private WayPoint[] wayPoints;
        
        public WayPoint this[int index] => wayPoints[index];

        public int GetClosestPointIndexFromPosition(Vector3 position)
        {
            float minDistance = Mathf.Infinity;
            int closestIndex = -1;
            
            for(int i = 0; i < wayPoints.Length; i++)
            {
                Vector3 wayPointPosition = wayPoints[i].Position;
                float distance = (wayPointPosition - position).sqrMagnitude;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestIndex = i;
                }
            }
            return closestIndex;
        }

        public int GetNextWayPointIndex(int currentIndex) => (currentIndex + 1) % wayPoints.Length;
        //이제 이걸 이용해서 웨이포인트를 순회하도록 코드를 작성하세요. 
        //MoveToNextPointAction을 수정하면 됩니다. 선착순 3명 
        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterValue(this);
        }
    }
}