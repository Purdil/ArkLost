using System;

namespace _Scripts.Agents.FSM.AgentTriggers
{
    public interface IDamageCastAgentTrigger
    {
        event Action OnDamageCast;
        void DamageCastTrigger();
        //public void DamageCastTrigger() => OnDamageCast?.Invoke();
    }
}