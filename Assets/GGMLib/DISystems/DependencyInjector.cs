using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GGMLib.DISystems
{
    [DefaultExecutionOrder(-10)] //가장 먼저 실행될 수 있도록 한다.
    public class DependencyInjector : MonoBehaviour
    {
        private const BindingFlags _bindingFlags 
            = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private readonly Dictionary<Type, object> _registry = new Dictionary<Type, object>(); //의존성 객체를 넣을 딕셔너리

        private void Awake()
        {
            IEnumerable<IDependencyProvider> providers = FindMonoBehaviours().OfType<IDependencyProvider>();
            foreach (IDependencyProvider provider in providers)
            {
                RegisterProvider(provider);
            }

            IEnumerable<MonoBehaviour> injectables = FindMonoBehaviours().Where(IsInjectable);

            foreach (MonoBehaviour injectable in injectables)
            {
                Inject(injectable);
            }
        }

        private void Inject(MonoBehaviour injectable)
        {
            Type type = injectable.GetType();
            IEnumerable<FieldInfo> fields = type.GetFields(_bindingFlags)
                .Where(f => Attribute.IsDefined(f, typeof(InjectAttribute)));

            foreach (FieldInfo field in fields)
            {
                Type fieldType = field.FieldType;
                object injectInstance = Resolve(fieldType); //해당 타입의 의존성 주입 객체를 찾아.
                Debug.Assert(injectInstance != null, $"주입할 객체가 없습니다. :{field.Name}");
                
                field.SetValue(injectable, injectInstance);
            }
            
            IEnumerable<MethodInfo> methods = type.GetMethods(_bindingFlags)
                .Where(method => Attribute.IsDefined(method, typeof(InjectAttribute)));

            foreach (MethodInfo method in methods)
            {
                Type[] requireParamTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                //GameManager와 EnemyManager
                object[] requiredInstances = requireParamTypes.Select(Resolve).ToArray();
                method.Invoke(injectable, requiredInstances);
            }
        }

        private bool IsInjectable(MonoBehaviour mono)
        {
            MemberInfo[] members = mono.GetType().GetMembers(_bindingFlags);
            return members.Any(member => Attribute.IsDefined(member, typeof(InjectAttribute)));
        }

        private object Resolve(Type type)
        {
            _registry.TryGetValue(type, out object result);
            return result;
        }

        private void RegisterProvider(IDependencyProvider provider)
        {
            if (Attribute.IsDefined(provider.GetType(), typeof(ProvideAttribute)))
            {
                _registry.Add(provider.GetType(), provider);
                return;
            }
            
            //그렇지 않다면 Provide 어트리뷰트가 있는 매서드를 찾아서 레지스트리에 넣어준다.
            MethodInfo[] methods = provider.GetType().GetMethods(_bindingFlags);
            foreach (MethodInfo method in methods)
            {
                if (!Attribute.IsDefined(method, typeof(ProvideAttribute))) continue;

                Type type = method.ReturnType; //해당 메서드가 리턴하는 타입
                object provideInstance = method.Invoke(provider, null); //매서드를 실행한 결과를 받는다.
                Debug.Assert(provideInstance != null, $"오브젝트가 널입니다. : {method.Name}");
                
                _registry.Add(type, provideInstance);
            }
        }

        private MonoBehaviour[] FindMonoBehaviours()
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        }
    }
}