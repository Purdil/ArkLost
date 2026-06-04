using System;
using UnityEngine;

namespace Test.AnimTest
{
    [RequireComponent(typeof(Animator))]
    public class TestRootMotion : MonoBehaviour
    {
        [SerializeField] private Transform parentTrm;
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            Debug.Log($"root position : {_animator.rootPosition}, " +
                      $"delta position : {_animator.deltaPosition}," +
                      $" delta rotation : {_animator.deltaRotation}");
            if (parentTrm != null)
            {
                parentTrm.Translate(_animator.deltaPosition);
                parentTrm.rotation *= _animator.deltaRotation;
            }
        }
    }
}