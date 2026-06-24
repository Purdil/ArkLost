using System;
using System.Collections.Generic;
using Reflex.Attributes;
using UnityEngine;

namespace Test.ReflexTest
{
    public class InjectDummy : MonoBehaviour
    {
        [Inject] [SerializeField] private RootInstaller rootInstaller;
        [Inject] [SerializeField] private SceneInstaller sceneInstaller;
        [Inject] private IEnumerable<string> _injectString;

        private void Start()
        {
            Debug.Log(string.Join(" ", _injectString));
            sceneInstaller.TestConsoleScene("TTT");
        }
    }
}