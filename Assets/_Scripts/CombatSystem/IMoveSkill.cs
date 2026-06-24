using System.Numerics;

namespace _Scripts.CombatSystem
{
    public interface IMoveSkill
    {
        bool CheckCanMoveSkill();
        Vector3 MoveValue { get; }
    }
}