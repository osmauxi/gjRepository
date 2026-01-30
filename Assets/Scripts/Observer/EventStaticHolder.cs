                                             using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

//做静态类供调用，实现与EventHolder的职责分离
public static class EventStaticHolder
{
    #region AddListener
    public static void AddListener(string name, UnityAction action,List<(string, Delegate, Type)> _registeredEvents)
    {
        EventCenter.Instance.AddEventListener(name, action);
        _registeredEvents.Add((name, action, typeof(UnityAction)));
    }
    public static void AddListener<T>(string name, UnityAction<T> action, List<(string, Delegate, Type)> _registeredEvents)
    {
        EventCenter.Instance.AddEventListener<T>(name, action);
        _registeredEvents.Add((name, action, typeof(UnityAction<T>)));
    }
    public static void AddListener<T1, T2>(string name, UnityAction<T1, T2> action,List<(string, Delegate, Type)> _registeredEvents)
    {
        EventCenter.Instance.AddEventListener<T1, T2>(name, action);
        _registeredEvents.Add((name, action, typeof(UnityAction<T1, T2>)));
    }
    public static void AddListener<T1, T2, T3>(string name, UnityAction<T1, T2, T3> action,List<(string, Delegate, Type)> _registeredEvents)
    {
        EventCenter.Instance.AddEventListener<T1, T2, T3>(name, action);
        _registeredEvents.Add((name, action, typeof(UnityAction<T1, T2, T3>)));
    }
    #endregion
    #region RemoveListener
    public static void RemoveListener(string name, UnityAction action,List<(string, Delegate, Type)> _registeredEvents)
    {
        EventCenter.Instance.RemoveEventListener(name, action);
        _registeredEvents.RemoveAll(item => item.Item1 == name && Delegate.Equals(item.Item2, action) && item.Item3 == typeof(UnityAction));
    }
    public static void RemoveListener<T>(string name, UnityAction<T> action, List<(string, Delegate, Type)> _registeredEvents)
    {
        EventCenter.Instance.RemoveEventListener(name, action);
        _registeredEvents.RemoveAll(item =>item.Item1 == name && Delegate.Equals(item.Item2, action)&& item.Item3 == typeof(UnityAction<T>) );
    }
    public static void RemoveListener<T1,T2>(string name, UnityAction<T1, T2> action, List<(string, Delegate, Type)> _registeredEvents)
    {
        EventCenter.Instance.RemoveEventListener(name, action);
        _registeredEvents.RemoveAll(item => item.Item1 == name && Delegate.Equals(item.Item2, action) && item.Item3 == typeof(UnityAction<T1, T2>));
    }
    public static void RemoveListener<T1, T2,T3>(string name, UnityAction<T1, T2,T3> action, List<(string, Delegate, Type)> _registeredEvents)
    {
        EventCenter.Instance.RemoveEventListener(name, action);
        _registeredEvents.RemoveAll(item => item.Item1 == name && Delegate.Equals(item.Item2, action) && item.Item3 == typeof(UnityAction<T1, T2, T3>));
    }
    #endregion
    #region TriggerEvent
    public static void EventTrigger(string name) 
    {
        EventCenter.Instance.EventTrigger(name);
    }

    public static void EventTrigger<T>(string name, T info) 
    {
        EventCenter.Instance.EventTrigger<T>(name,info);
    }

    public static void EventTrigger<T1, T2>(string name, T1 info1, T2 info2) 
    {
        EventCenter.Instance.EventTrigger<T1,T2>(name, info1,info2);
    }

    public static void EventTrigger<T1, T2, T3>(string name, T1 info1, T2 info2, T3 info3) 
    {
        EventCenter.Instance.EventTrigger<T1,T2,T3>(name, info1,info2,info3);
    }

    #endregion
    #region RemoveAllListener
    public static void RemoveAllListeners(List<(string, Delegate, Type)> _registeredEvents)
    {
        foreach (var (name, action, type) in _registeredEvents)
        {
            if (string.IsNullOrEmpty(name) || action == null) continue;

            try
            {
                MethodInfo removeMethod = null;
                Type eventCenterType = typeof(EventCenter);

                // 非泛型委托
                if (type == typeof(UnityAction))
                {
                    removeMethod = eventCenterType.GetMethod("RemoveEventListener",new Type[] { typeof(string), typeof(UnityAction) }
                    );
                }
                // 单参数泛型委托
                else if (type.IsGenericType &&type.GetGenericTypeDefinition() == typeof(UnityAction<>) &&type.GetGenericArguments().Length == 1)
                {
                    Type genericArg = type.GetGenericArguments()[0];
                    var methods = eventCenterType.GetMethods().Where(m => m.Name == "RemoveEventListener" && m.IsGenericMethod).ToList();

                    // 尝试找到匹配的泛型方法
                    foreach (var method in methods)
                    {
                        if (method.GetGenericArguments().Length == 1)
                        {
                            removeMethod = method.MakeGenericMethod(genericArg);
                            break;
                        }
                    }
                }

                if (removeMethod != null)
                {
                    removeMethod.Invoke(EventCenter.Instance, new object[] { name, action });
                }
                else
                {
                    Debug.LogWarning($"无法为类型 {type} 找到 RemoveEventListener 方法");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"移除事件监听器失败: {name}, {type}. 错误: {ex.Message}");
            }
        }

        _registeredEvents.Clear();
    }
    #endregion
}
