using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

// Centralized event registry + Netcode sync.
// AddListener 的重载允许传入 optional EventNetHolder owner 用于自动记录（owner 只保存元信息用于 OnDestroy 清理）。
public class StaticEventNetHolder : NetworkBehaviour
{
    public static StaticEventNetHolder Instance { get; private set; }

    private void Awake()
    {
        if(Instance == null) 
            Instance = this;
        else
            Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    private Dictionary<string, IEventInfo> _eventDic = new Dictionary<string, IEventInfo>();

    #region EventInfo definitions
    private interface IEventInfo
    //UnityAction 是纯本地委托, 不支持网络序列化和远程调用.所以使用Delegate作为参数类型(可以放进Rpc当参数)
    // Delegate 是 “强类型泛型”，Invoke 时无装箱拆箱；
    //IEventInfo 接口是纯抽象层，无额外内存开销。
    {
        Type[] ParamTypes { get; }
        void AddDelegate(Delegate del);
        void RemoveDelegate(Delegate del);
        void InvokeLocal(object[] args);
    }

    private class EventInfo0 : IEventInfo
    {
        private UnityAction actions;
        public Type[] ParamTypes => Type.EmptyTypes;
        public EventInfo0(UnityAction action) 
        {
            actions += action;
        }
        public void AddDelegate(Delegate del) 
        {
            actions += del as UnityAction;
        }
        public void RemoveDelegate(Delegate del) 
        {
            actions -= del as UnityAction;
        }
        public void InvokeLocal(object[] args) 
        {
            if (actions == null)
                return;
            actions?.Invoke();
        } 
    }

    private class EventInfo1<T> : IEventInfo
    {
        private UnityAction<T> actions;
        public Type[] ParamTypes => new[] { typeof(T) };
        public EventInfo1(UnityAction<T> action) 
        {
            actions += action;
        }
        public void AddDelegate(Delegate del) 
        {
            actions += del as UnityAction<T>;
        }
        public void RemoveDelegate(Delegate del) 
        {
            actions -= del as UnityAction<T>;
        } 
        public void InvokeLocal(object[] args)
        {
            if (actions == null) 
                return;
            var a = (T)args[0];
            actions.Invoke(a);
        }
    }

    private class EventInfo2<T1, T2> : IEventInfo
    {
        private UnityAction<T1, T2> actions;
        public Type[] ParamTypes => new[] { typeof(T1), typeof(T2) };
        public EventInfo2(UnityAction<T1, T2> action)
        {
            actions += action;
        }
        public void AddDelegate(Delegate del)
        {
            actions += del as UnityAction<T1, T2>;
        }
        public void RemoveDelegate(Delegate del)
        {
            actions -= del as UnityAction<T1, T2>;
        }
        public void InvokeLocal(object[] args)
        {
            if (actions == null) 
                return;
            var a1 = (T1)args[0];
            var a2 = (T2)args[1];
            actions.Invoke(a1, a2);
        }
    }

    private class EventInfo3<T1, T2, T3> : IEventInfo
    {
        private UnityAction<T1, T2, T3> actions;
        public Type[] ParamTypes => new[] { typeof(T1), typeof(T2), typeof(T3) };
        public EventInfo3(UnityAction<T1, T2, T3> action) 
        {
            actions += action;
        } 
        public void AddDelegate(Delegate del) 
        {
            actions += del as UnityAction<T1, T2, T3>;
        } 
        public void RemoveDelegate(Delegate del)
        {
            actions -= del as UnityAction<T1, T2, T3>;
        }
        public void InvokeLocal(object[] args)
        //args是参数数组，按顺序存储了所有参数，按类型转换后传递给委托
        {
            if (actions == null) return;
            var a1 = (T1)args[0];
            var a2 = (T2)args[1];
            var a3 = (T3)args[2];
            actions.Invoke(a1, a2, a3);
        }
    }
    #endregion

    #region AddListener
    public void AddListener(string name, UnityAction action, EventNetHolder owner = null)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo0 e)
            //类型检测防止铸币忘记事件参数把双参数加到单参数事件里
            e.AddDelegate(action);
        else
            _eventDic[name] = new EventInfo0(action);

        owner?.RecordRegistration(name, action);
        //事件注册时，如果传入了owner，就记录这次注册，方便后续自动解绑
    }

    public void AddListener<T>(string name, UnityAction<T> action, EventNetHolder owner = null)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo1<T> e)
            e.AddDelegate(action);
        else
            _eventDic[name] = new EventInfo1<T>(action);

        owner?.RecordRegistration(name, action);
    }

    public void AddListener<T1, T2>(string name, UnityAction<T1, T2> action, EventNetHolder owner = null)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo2<T1, T2> e)
            e.AddDelegate(action);
        else
            _eventDic[name] = new EventInfo2<T1, T2>(action);

        owner?.RecordRegistration(name, action);
    }

    public void AddListener<T1, T2, T3>(string name, UnityAction<T1, T2, T3> action, EventNetHolder owner = null)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo3<T1, T2, T3> e)
            e.AddDelegate(action);
        else
            _eventDic[name] = new EventInfo3<T1, T2, T3>(action);

        owner?.RecordRegistration(name, action);
    }
    #endregion

    #region RemoveListener
    public void RemoveListener(string name, UnityAction action)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo0 e)
            e.RemoveDelegate(action);
    }

    public void RemoveListener<T>(string name, UnityAction<T> action)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo1<T> e)
            e.RemoveDelegate(action);
    }

    public void RemoveListener<T1, T2>(string name, UnityAction<T1, T2> action)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo2<T1, T2> e)
            e.RemoveDelegate(action);
    }

    public void RemoveListener<T1, T2, T3>(string name, UnityAction<T1, T2, T3> action)
    {
        if (_eventDic.TryGetValue(name, out var info) && info is EventInfo3<T1, T2, T3> e)
            e.RemoveDelegate(action);
    }

    //不需要带参的通用方法，它通过委托名找到EventInfo类，然后调用RemoveDelegate方法，因为通过接口调用，所以不需要知道具体类型
    public void RemoveListener(string name, Delegate del)
    {
        if (!_eventDic.TryGetValue(name, out var info)) return;
        info.RemoveDelegate(del);
    }
    #endregion

    #region RemoveAllListeners
    public void RemoveAllListeners(List<(string name, Delegate action)> regs)
    {
        if (regs == null) return;
        foreach (var (name, action) in regs) 
        {
            RemoveListener(name, action);
        }
    }
    #endregion

    #region Trigger + Network RPC
    public void EventTrigger(string eventName, ulong[] targetClientIds = null)
    {
        if (!IsServer) return;
        //广播到客户端，没有指定客户端(targetClientIds为空)则发给所有客户端
        SyncEventClientRpc(eventName, SerializeArgs(null), BuildClientRpcParams(targetClientIds));
    }

    public void EventTrigger<T>(string eventName, T arg1, ulong[] targetClientIds = null)
    {
        if (!IsServer) return;
        SyncEventClientRpc(eventName, SerializeArgs(new object[] { arg1 }), BuildClientRpcParams(targetClientIds));
    }

    public void EventTrigger<T1, T2>(string eventName, T1 arg1, T2 arg2, ulong[] targetClientIds = null)
    {
        if (!IsServer) return;
        SyncEventClientRpc(eventName, SerializeArgs(new object[] { arg1, arg2 }), BuildClientRpcParams(targetClientIds));
    }

    public void EventTrigger<T1, T2, T3>(string eventName, T1 arg1, T2 arg2, T3 arg3, ulong[] targetClientIds = null)
    {
        if (!IsServer) return;
        SyncEventClientRpc(eventName, SerializeArgs(new object[] { arg1, arg2, arg3 }), BuildClientRpcParams(targetClientIds));
    }
    #endregion
    /// <summary>
    /// argsJson的传值，声明object[] args = new object[] {这里面传参数类型即可};
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestEventTriggerServerRpc(string eventName, string argsJson, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        //if (!IsValidRequest(eventName)) return;

        _eventDic.TryGetValue(eventName, out var info);//取出事件信息
        var args = DeserializeArgs(argsJson, info?.ParamTypes);//反序列化客户端发来的JSON参数
        if (info != null)
            info.InvokeLocal(args ?? Array.Empty<object>());

        SyncEventClientRpc(eventName, argsJson);//同步到所有客户端
    }
    [ClientRpc]
    private void SyncEventClientRpc(string eventName, string argsJson, ClientRpcParams clientRpcParams = default)
    {
        if (IsServer && !IsClient) 
            return; 
        if (!_eventDic.TryGetValue(eventName, out var info)) 
            return;
        var args = DeserializeArgs(argsJson, info.ParamTypes);
        Debug.Log("ClientRpc received event: " + eventName); 
        info.InvokeLocal(args ?? Array.Empty<object>());
    }

    #region 自定义参数的 JSON 序列化 / 反序列化
    //json序列化，相比二进制序列化更通用，但性能较低
    [Serializable]
    private class ArgsWrapper //JSON序列化的包装壳，因为 Unity 的JsonUtility无法直接序列化 / 反序列化string[]（需包装在可序列化类中）；
    {
        public string[] argsJson;
    }

    private string SerializeArgs(object[] args)
    {
        if (args == null || args.Length == 0) //空参数就返回空数组包装壳
            return JsonUtility.ToJson(new ArgsWrapper { argsJson = new string[0] });
        var arr = new string[args.Length];
        for (int i = 0; i < args.Length; i++) //遍历参数数组，把每个参数转成独立的JSON字符串
        {
            arr[i] = JsonUtility.ToJson(args[i]);
        }
        return JsonUtility.ToJson(new ArgsWrapper { argsJson = arr });
        //把字符串数组包装进ArgsWrapper，再转成最终的JSON字符串
    }

    private object[] DeserializeArgs(string wrapperJson, Type[] paramTypes)
    {
        if (string.IsNullOrEmpty(wrapperJson)) 
            return Array.Empty<object>();
        //把JSON字符串还原为ArgsWrapper容器
        var wrapper = JsonUtility.FromJson<ArgsWrapper>(wrapperJson);

        if (wrapper?.argsJson == null || wrapper.argsJson.Length == 0)
            return Array.Empty<object>();
        if (paramTypes == null || paramTypes.Length == 0) 
            return Array.Empty<object>();

        //遍历还原每个参数：按类型把JSON字符串转回原对象
        var results = new object[paramTypes.Length];
        for (int i = 0; i < paramTypes.Length && i < wrapper.argsJson.Length; i++)
            results[i] = JsonUtility.FromJson(wrapper.argsJson[i], paramTypes[i]);
        return results;
    }
    #endregion

    private ClientRpcParams BuildClientRpcParams(ulong[] targetClientIds)
    {
        if (targetClientIds == null || targetClientIds.Length == 0) return default;
        return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = targetClientIds } };
    }

    private bool IsValidRequest(string eventName)
    {
        // 示例：禁止客户端触发某些 server-only 事件。按需扩展为白名单/权限检查。
        return eventName != EventRegister.OnPlayerEnterRoom && eventName != EventRegister.OnGamePlayStart;
    }

    public override void OnDestroy()
    {
        _eventDic.Clear();
    }
}



