using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class EventCenter
{
    private static EventCenter instance;
    private Dictionary<string, IEventInfo> _eventDic = new Dictionary<string, IEventInfo>();
    //事件列表，存储一个事件名和IEventInfo接口便于获取，因为EventInfo有重载，不太好直接获取
    public static EventCenter Instance//没有继承monobehavior的单例模式写法
    {
        get
        {
            if (instance == null)
            {
                instance = new EventCenter();
            }
            return instance;
        }     
    }
    public void AddEventListener(string name, UnityAction action) //将事件加入监听
    {
        if (_eventDic.ContainsKey(name))
        {
            (_eventDic[name] as EventInfo).actions += action;
            //每次加入事件列表时要进行类型转换，从接口转到真正存储事件的EventInfo，as运算符本身优先级没有.运算符高，所以要用()框住
        }
        else
        {
            _eventDic.Add(name, new EventInfo(action));
        }
    }
    public void EventTrigger(string name) //事件触发器
    {
        if (_eventDic.ContainsKey(name))
        {
            if ((_eventDic[name] as EventInfo).actions != null)
            {
                (_eventDic[name] as EventInfo).actions.Invoke();
                //将值传入并响应当前对应的所有委托
            }
        }
    }
    public void RemoveEventListener(string name, UnityAction action)
    {
        if (_eventDic.ContainsKey(name))
        {
            (_eventDic[name] as EventInfo).actions -= action;
        }
    }
    public void AddEventListener<T>(string name, UnityAction<T> action) //将事件加入监听
    {
        if (_eventDic.ContainsKey(name))
        {
            (_eventDic[name] as EventInfo<T>).actions += action;
            //每次加入事件列表时要进行类型转换，从接口转到真正存储事件的EventInfo，as运算符本身优先级没有.运算符高，所以要用()框住
        }
        else
        {
            _eventDic.Add(name, new EventInfo<T>(action));
        }
    }
    public void EventTrigger<T>(string name,T info) //事件触发器
    {
        if (_eventDic.ContainsKey(name)) 
        {
            if ((_eventDic[name] as EventInfo<T>).actions != null) 
            {
                (_eventDic[name] as EventInfo<T>).actions.Invoke(info);
                //将值传入并响应当前对应的所有委托
            }
        }
    }
    public void RemoveEventListener<T>(string name,UnityAction<T> action) 
    {
        if (_eventDic.ContainsKey(name)) 
        {
            (_eventDic[name] as EventInfo<T>).actions -= action;
        }
    }
    //对于多个参数委托的重载支持
    public void AddEventListener<T1, T2>(string name, UnityAction<T1, T2> action)
    {
        if (_eventDic.ContainsKey(name))
            (_eventDic[name] as EventInfo<T1, T2>).actions += action;
        else
            _eventDic.Add(name, new EventInfo<T1, T2>(action));
    }
    public void EventTrigger<T1, T2>(string name, T1 info1, T2 info2)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo<T1, T2> eventInfo)
            eventInfo.actions?.Invoke(info1, info2);
    }
    public void RemoveEventListener<T1, T2>(string name, UnityAction<T1, T2> action)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo<T1, T2> eventInfo)
            eventInfo.actions -= action;
    }
    public void AddEventListener<T1, T2,T3>(string name, UnityAction<T1, T2, T3> action)
    {
        if (_eventDic.ContainsKey(name))
            (_eventDic[name] as EventInfo<T1, T2, T3>).actions += action;
        else
            _eventDic.Add(name, new EventInfo<T1, T2, T3>(action));
    }
    public void EventTrigger<T1, T2, T3>(string name, T1 info1, T2 info2,T3 info3)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo<T1, T2, T3> eventInfo)
            eventInfo.actions?.Invoke(info1, info2,info3);
    }
    public void RemoveEventListener<T1, T2, T3>(string name, UnityAction<T1, T2, T3> action)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo<T1, T2, T3> eventInfo)
            eventInfo.actions -= action;
    }
    public void clear() 
    {
        _eventDic.Clear();
    }
}
public interface IEventInfo 
{

}
public class EventInfo : IEventInfo 
{
    public UnityAction actions;
    public EventInfo(UnityAction action) 
    {
        actions += action;
    }
}
public class EventInfo<T> : IEventInfo
{
    public UnityAction<T> actions;
    public EventInfo(UnityAction<T> action)
    {
        actions += action;
    }
}
public class EventInfo<T1, T2> : IEventInfo
{
    public UnityAction<T1, T2> actions;
    public EventInfo(UnityAction<T1, T2> action) => actions += action;
}
public class EventInfo<T1, T2, T3> : IEventInfo
{
    public UnityAction<T1, T2, T3> actions;
    public EventInfo(UnityAction<T1, T2, T3> action) => actions += action;
}
