using System;
using System.Collections.Generic;
using UnityEngine;

// EventNetHolder 仅记录订阅元信息，用于 OnDestroy 自动解绑。
// 不再实现 AddListener/RemoveListener 的实际逻辑（调用方应调用 StaticEventNetHolder.AddListener(...) 并传入 owner）。
public class EventNetHolder : MonoBehaviour
{
    // 存储 (事件名, 委托)。EventNetHolder 不关心 delegate 的签名（只用于移除）。
    private List<(string name, Delegate action)> _registeredEvents = new List<(string name, Delegate action)>();

    // 供 StaticEventNetHolder.AddListener(owner) 调用以记录注册项
    public void RecordRegistration(string eventName, Delegate action)
    {
        if (string.IsNullOrEmpty(eventName) || action == null) return;
        _registeredEvents.Add((eventName, action));
    }

    public List<(string, Delegate)> GetEventList() => _registeredEvents;

    private void OnDestroy()
    {
        if (StaticEventNetHolder.Instance != null)
        {
            StaticEventNetHolder.Instance.RemoveAllListeners(_registeredEvents);
        }
        _registeredEvents.Clear();
    }
}