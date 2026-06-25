using System;

namespace _Scripts.Agents.AgentTriggers
{
    public interface ILinkTimeEndAgentTrigger
    {
        event Action OnLinkTimeEnd;
        void LinkTimeTrigger();
    }
}