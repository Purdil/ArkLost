using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGMLib.EventChannelSystem
{
    [CreateAssetMenu(fileName = "Event Channel", menuName = "Lib/EventChannel", order = 0)]
    public class GameEventChannelSO : ScriptableObject
    {
        private Dictionary<Type, Action<GameEvent>> _events = new();
        private Dictionary<Delegate, Action<GameEvent>> _lookUp = new(); //중복구독 방지를 위한 테이블

        public void AddListener<T>(Action<T> handler) where T : GameEvent
        {
            if (_lookUp.ContainsKey(handler)) return;
            
            Action<GameEvent> castHandler = (evt) => handler(evt as T); //타입캐스트 콜 핸들러
            _lookUp[handler] =  castHandler;
            Type eventType = typeof(T);
            if (_events.ContainsKey(eventType))
            {
                _events[eventType] += castHandler;
            }
            else
            {
                _events[eventType] = castHandler;
            }
        }

       

        public void RemoveListener<T>(Action<T> handler) where T : GameEvent
        {
           Type eventType = typeof(T);
           if (_lookUp.TryGetValue(handler, out Action<GameEvent> action))
           {
               if (_events.TryGetValue(eventType, out Action<GameEvent> internalAction))
               {
                   internalAction -=  action;
                   if (internalAction == null) //구독을 해제하고 없다면 딕셔너리에서 action제거
                   {
                       _events.Remove(eventType);
                   }
                   else //남아있다면 다시 넣어줌.
                   {
                       _events[eventType] = internalAction;
                   }
               }
           }
           _lookUp.Remove(handler);
            
        }
        
        public void RaiseEvent(GameEvent evt)
        {
            if (_events.TryGetValue(evt.GetType(), out Action<GameEvent> action))
            {
                action?.Invoke(evt);
            }
        }

        public void Clear()
        {
            _events.Clear();
            _lookUp.Clear();
        }
    }
}