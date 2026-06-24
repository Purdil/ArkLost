using System;
using GGMLib.ModuleSystem;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.CombatSystem
{
    public class ToughnessModule : MonoBehaviour, IModule
    {
        [Serializable]
        public sealed class ToughnessValueChangedEvent : UnityEvent<float, float>
        {
        }

        [SerializeField] private float maxToughness;
        [SerializeField] private float currentToughness;
        [SerializeField] private ToughnessValueChangedEvent onToughnessChangedUnity = new ToughnessValueChangedEvent();

        public UnityEvent OnLessToughness;
        public UnityEvent OnRecuveryToughness;

        public event Action<float, float> OnToughnessChanged;

        public float MaxToughness => maxToughness;
        public float CurrentToughness => currentToughness;
        public float NormalizedToughness => maxToughness > 0f ? Mathf.Clamp01(currentToughness / maxToughness) : 0f;
        public ToughnessValueChangedEvent OnToughnessChangedUnity => onToughnessChangedUnity;

        public void Initialize(ModuleOwner owner)
        {
            currentToughness = Mathf.Clamp(currentToughness, 0f, Mathf.Max(0f, maxToughness));
            NotifyToughnessChanged();
        }

        public void DecreaseToughness(float amount)
        {
            if (amount <= 0f)
            {
                RecoverToughness(-amount);
                return;
            }

            currentToughness = Mathf.Clamp(currentToughness - amount, 0f, Mathf.Max(0f, maxToughness));
            NotifyToughnessChanged();

            if (currentToughness <= 0f)
                OnLessToughness?.Invoke();
        }

        public void RecoverToughness(float amount)
        {
            if (amount <= 0f)
                return;

            var wasEmpty = currentToughness <= 0f;
            currentToughness = Mathf.Clamp(currentToughness + amount, 0f, Mathf.Max(0f, maxToughness));
            NotifyToughnessChanged();

            if (wasEmpty && currentToughness > 0f)
                OnRecuveryToughness?.Invoke();
        }

        public void SetToughness(float value)
        {
            currentToughness = Mathf.Clamp(value, 0f, Mathf.Max(0f, maxToughness));
            NotifyToughnessChanged();
        }

        private void NotifyToughnessChanged()
        {
            OnToughnessChanged?.Invoke(currentToughness, maxToughness);
            onToughnessChangedUnity?.Invoke(currentToughness, maxToughness);
        }
    }
}
