using Unity.Behavior;

namespace _Scripts.Enemies.BT
{
    [BlackboardEnum]
    public enum EnemyState
    {
        IDLE, MOVE, COMBAT, HIT, DEATH
    }
}