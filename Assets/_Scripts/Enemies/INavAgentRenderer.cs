namespace Enemies
{
    public interface INavAgentRenderer
    {
        void BeginManualControl();
        void EndManualControl();
        void SetSyncNavPos(bool isSync);
        void UpdateNavAgentPosition();
    }
}
