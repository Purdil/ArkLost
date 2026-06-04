using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GGMLib.ModuleSystem
{
    public abstract class ModuleOwner : MonoBehaviour
    {
        protected Dictionary<Type, IModule> _moducleDict;

        protected virtual void Awake()
        {
            _moducleDict = GetComponentsInChildren<IModule>()
                .ToDictionary(module => module.GetType());

            InitializeComponents();
            AfterInitComponents();
        }

        protected virtual void InitializeComponents()
        {
            foreach (IModule module in _moducleDict.Values)
            {
                module.Initialize(this); //오너를 자기로 셋팅하여 넣어준다.
            }
        }

        protected virtual void AfterInitComponents()
        {
            foreach (IAfterInitModule module in _moducleDict.Values.OfType<IAfterInitModule>())
            {
                module.AfterInit();
            }
        }

        public T GetModule<T>()
        {
            if(_moducleDict.TryGetValue(typeof(T), out IModule module))
                return (T) module;
            
            IModule findModule = _moducleDict.Values.FirstOrDefault(moduleType => moduleType is T);

            if (findModule is T castedModule)
                return castedModule;

            return default;
        }
    }
}