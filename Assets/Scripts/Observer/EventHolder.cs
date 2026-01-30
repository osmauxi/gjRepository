using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

//事件管理器，满足当前观察者模式单对象多订阅无法自动清理所有订阅的情况，使用方法如下
//一个gameobject挂一个，里面的脚本通过GetComponent等方法获取到实例，并调用EventHolder实例中的AddListener<T>方法进行事件订阅
//这样当此gameobjct被删除时，自带的OnDestroy方法执行实现自动清理所有订阅
public class EventHolder : MonoBehaviour

//用法示例EventStaticHolder.AddListener(EventRegister.OnRoomEntered, OnRoomEntered,GetComponent<EventHolder>().GetEventList());
{

    private List<(string name, Delegate action, Type type)> _registeredEvents = new List<(string name, Delegate action, Type type)>();
    public List<(string, Delegate, Type)> GetEventList()
    {
        return _registeredEvents;
    }
    private void OnDestroy()
    {
        EventStaticHolder.RemoveAllListeners(_registeredEvents);
    }
}