namespace Agents
{
    public interface IRenderer
    {
        void PlayClip(int clipHash, float normalizedTime, float crossFadeDuration, int layerIndex = 0);
    }
}