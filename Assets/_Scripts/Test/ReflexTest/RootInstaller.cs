using Reflex.Core;
using UnityEngine;

namespace Test.ReflexTest
{
    public class RootInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterValue("Hello");
            containerBuilder.RegisterValue(this);
        }

        public static void TestConsole(string msg)
        {
            Debug.Log($"Root installer test : {msg}");
        }
    }
}