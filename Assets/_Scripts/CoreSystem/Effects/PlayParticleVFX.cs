using System.Collections;
using CoreSystem;
using UnityEngine;

namespace _Scripts.CoreSystem.Effects
{
    public class PlayParticleVFX : MonoBehaviour, IPlayableVFX
    {
        [field: SerializeField] public AssetNameSO VfxName { get; private set; }
        [field: SerializeField] public float VfxDuration { get; private set; }
        [SerializeField] private ParticleSystem[] particles;
        [SerializeField] private bool detachToWorldOnPlay;

        private static Transform _worldRoot;
        private Transform _originalParent;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;
        private Vector3 _originalLocalScale;
        private Coroutine _restoreCoroutine;
        private bool _isDetached;

        private void Awake()
        {
            CacheOriginalTransform();
        }

        public void PlayVFX(Vector3 position, Quaternion rotation)
        {
            if (_isDetached)
                RestoreOriginalTransform();

            transform.SetPositionAndRotation(position, rotation);
            PlayVFX();
        }

        public void PlayVFX()
        {
            if (_restoreCoroutine != null)
            {
                StopCoroutine(_restoreCoroutine);
                _restoreCoroutine = null;
            }

            if (_isDetached)
            {
                StopParticles(ParticleSystemStopBehavior.StopEmittingAndClear);
                RestoreOriginalTransform();
            }

            Vector3 playPosition = transform.position;
            Quaternion playRotation = transform.rotation;

            if (detachToWorldOnPlay)
            {
                transform.SetParent(GetWorldRoot(), true);
                transform.SetPositionAndRotation(playPosition, playRotation);
                _isDetached = true;
            }

            foreach (ParticleSystem particle in particles)
            {
                particle.Play();
            }

            if (detachToWorldOnPlay && !HasLoopingParticle())
                _restoreCoroutine = StartCoroutine(RestoreWhenFinished());
        }

        public void StopVFX()
        {
            if (_restoreCoroutine != null)
            {
                StopCoroutine(_restoreCoroutine);
                _restoreCoroutine = null;
            }

            ParticleSystemStopBehavior stopBehavior = _isDetached
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            StopParticles(stopBehavior);

            if (_isDetached)
                RestoreOriginalTransform();
        }

        private void CacheOriginalTransform()
        {
            _originalParent = transform.parent;
            _originalLocalPosition = transform.localPosition;
            _originalLocalRotation = transform.localRotation;
            _originalLocalScale = transform.localScale;
        }

        private IEnumerator RestoreWhenFinished()
        {
            yield return null;

            while (IsAnyParticleAlive())
                yield return null;

            _restoreCoroutine = null;
            RestoreOriginalTransform();
        }

        private bool IsAnyParticleAlive()
        {
            foreach (ParticleSystem particle in particles)
            {
                if (particle != null && particle.IsAlive(true))
                    return true;
            }

            return false;
        }

        private bool HasLoopingParticle()
        {
            foreach (ParticleSystem particle in particles)
            {
                if (particle != null && particle.main.loop)
                    return true;
            }

            return false;
        }

        private void StopParticles(ParticleSystemStopBehavior stopBehavior)
        {
            foreach (ParticleSystem particle in particles)
            {
                if (particle != null)
                    particle.Stop(true, stopBehavior);
            }
        }

        private void RestoreOriginalTransform()
        {
            transform.SetParent(_originalParent, false);
            transform.localPosition = _originalLocalPosition;
            transform.localRotation = _originalLocalRotation;
            transform.localScale = _originalLocalScale;
            _isDetached = false;
        }

        private static Transform GetWorldRoot()
        {
            if (_worldRoot == null)
            {
                GameObject root = new GameObject("VFX World Root");
                _worldRoot = root.transform;
            }

            return _worldRoot;
        }
    }
}
