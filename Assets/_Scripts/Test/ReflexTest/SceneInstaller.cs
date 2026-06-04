using Reflex.Core;
using UnityEngine;

namespace Test.ReflexTest
{
    public class SceneInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterValue("World");
            containerBuilder.RegisterValue(this);
        }

        public void TestConsoleScene(string msg) => Debug.Log($"Scene installer : {msg}");
    }
}