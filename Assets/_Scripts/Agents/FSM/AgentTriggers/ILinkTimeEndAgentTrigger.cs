using System;

namespace _Scripts.Agents.FSM.AgentTriggers
{
    public interface ILinkTimeEndAgentTrigger
    {
        event Action OnLinkTimeEnd;
        void LinkTimeTrigger();
    }
}