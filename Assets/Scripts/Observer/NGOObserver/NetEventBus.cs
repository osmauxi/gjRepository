using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

//使用结构体作为事件数据载体，因为结构体是值类型，分配在栈上，用完直接删，不会有GC开销
public class NetEventBus : NetworkBehaviour
{
    public static NetEventBus Instance;

    private void Awake()
    {
        if (Instance == null) 
            Instance = this;
        else 
            Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        //网络底层方法，RegisterNamedMessageHandler注册NetEvent消息处理器，
        //收到消息时直接传输原始的二进制数据流(FastBufferReader)，并调用HandleIncomingPacket处理
        //使用这个因为
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("NetEvent", HandleIncomingPacket);       
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("NetEvent");
    }

    //Type存结构体的类型，object存对应的委托(Action<T>)，因为结构体可以存多个值，所以只需要单泛型方法
    //不同委托因为委托名不同，获取出来是没法放一起的，所以用object存储，所有物体都是object的子类，在后续需要使用的时候再拆箱，强制类型转换进行使用
    private Dictionary<Type, object> _handlers = new Dictionary<Type, object>();
    //Type，object作为标准的泛型事件总线的存储结构，装箱拆箱的性能开销并不大，高级C#这一块

    public void Subscribe<T>(Action<T> handler) where T : struct, INetEvent
    {
        //获取类型
        Type type = typeof(T);

        if (!_handlers.ContainsKey(type))
        {
            _handlers[type] = null;
        }
        //_handlers[type]取出所有委托，强制类型转换Action<T>，然后加上新的handler委托
        _handlers[type] = (Action<T>)_handlers[type] + handler;
    }
    //为什么(Action<T>)_handlers[type]取出委托之后可以进行加减法？这里是加减法的重载。
    //因为委托本质上是一个多播委托，也就是一个列表，可以通过+和-操作符来添加或移除方法引用，被触发时会依次调用列表中的所有方法

    //struct约束T必须是值类型，INetEvent约束T必须实现INetEvent接口
    public void Unsubscribe<T>(Action<T> handler) where T : struct, INetEvent
    {
        Type type = typeof(T);
        if (_handlers.ContainsKey(type))
        {
            _handlers[type] = (Action<T>)_handlers[type] - handler;
        }
    }

    public void Send<T>(T data) where T : struct, INetEvent
    {
        //如果是 Server：直接广播给所有 Client，并执行本地逻辑
        if (IsServer)
        {
            SendToAllClients(data);
            InvokeLocal(data);
        }
        //如果是 Client：发给 Server 请求转发
        else if (IsClient)
        {
            SendToServer(data);
            //客户端发送后，通常不立即执行本地，而是等服务器广播回来（保证时序一致）
            //或者如果你需要预测表现，可以在这里 InvokeLocal(data)
        }
    }

    #region 网络底层处理
    private void SendToServer<T>(T data) where T : struct, INetEvent
    {
        //创建一个“快速写入器”
        //1024:初始容量，申请一块 1024 字节的内存条来写数据。
        //Allocator.Temp:告诉 Unity 这块内存是“临时的”，用完这一帧立马销毁，0GC。
        var writer = new FastBufferWriter(1024, Allocator.Temp);

        //写入“信封标签”
        //typeof(T).FullName输出此事件数据结构体的类型信息，也就是所在命名空间 + 结构体名
        //WriteValueSafe: 把字符串转换成二进制写入内存条。
        //也就是告诉接收方，这包数据是什么类型的事件，之后收到二进制数据流之后就按照这个类型来解析。
        writer.WriteValueSafe(typeof(T).FullName);

        //写入“信件内容”
        //因为结构体都实现了INetworkSerializable，直接调用NGO的扩展方法WriteNetworkSerializable就行了
        //这里会调用结构体T里的NetworkSerialize方法把实际数据转为二进制流。
        writer.WriteNetworkSerializable(data);

        //发送数据包
        //NetEvent:频道名，NetEventBus已经在OnNetworkSpawn注册了这个频道的处理器
        //NetworkManager.ServerClientId获取服务器ID，这个方法是发给服务器的。
        //writer:把写好的内存条交出去。
        //所以这里服务器会收到这包数据，然后调用HandleIncomingPacket处理。
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("NetEvent", NetworkManager.ServerClientId, writer);
    }

    private void SendToAllClients<T>(T data) where T : struct, INetEvent
    {
        var writer = new FastBufferWriter(1024, Allocator.Temp);
        writer.WriteValueSafe(typeof(T).FullName);
        writer.WriteNetworkSerializable(data);

        //SendNamedMessageToAll: 这是一个群发指令。
        //NGO会把这份二进制数据复制N份，发给所有连接的客户端。
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("NetEvent", writer);
    }

    //处理收到的包
    private void HandleIncomingPacket(ulong senderId, FastBufferReader reader)
    {
        //拆“信封标签”
        //第一个写入的内容是typeName，所以第一个读出来的也是typeName
        //读完后，reader 的指针会向后移动，指向剩下的数据（结构体内容）
        reader.ReadValueSafe(out string typeName);

        //反射获取Type
        Type eventType = Type.GetType(typeName);
        if (eventType == null) 
            return;

        //typeof(NetEventBus).GetMethod:去NetEventBus类里找一个名字叫"ReceiveInternal"的方法。
        //BindingFlags:告诉它去哪里找（NonPublic = 私有方法也找，Instance = 实例方法）。
        //也就是拿到了ReceiveInternal方法，但是现在还没填泛型参数 T，是空的容器。
        var method = typeof(NetEventBus).GetMethod("ReceiveInternal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        //MakeGenericMethod:用反射给泛型方法补全类型参，让它从抽象的泛型模板变成可直接调用的具体方法
        //填入泛型参数，因为上方已经获取了T的实际类型。
        //等同于：private void ReceiveInternal<PlayerFireEvent>(...)
        var genericMethod = method.MakeGenericMethod(eventType);
        //调用方法，传入参数
        genericMethod.Invoke(this, new object[] { senderId, reader });
    }

    //泛型解析漏斗
    private void ReceiveInternal<T>(ulong senderId, FastBufferReader reader) where T : struct, INetEvent
    {
        T data = new T();
        //拆包
        //reader此时的指针正好指在结构体数据的开头（因为刚才FullName已经在HandleIncomingPacket中被读走了）。
        //调用结构体的NetworkSerialize方法进行反序列化，读出值写入data。
        reader.ReadNetworkSerializable(out data);

        //如果我是服务器，收到了客户端的消息 -> 我需要广播给其他人
        if (IsServer)
        {
            // 可以在这里加权限验证：这个 senderId 有资格发这个事件吗？
            // if (CheckPermission(senderId, data)) ...

            //广播
            SendToAllClients(data);

            //执行服务器本地逻辑
            InvokeLocal(data);
        }
        // 如果我是客户端，收到了服务器的消息 -> 执行本地逻辑
        else
        {
            InvokeLocal(data);
        }
    }

    private void InvokeLocal<T>(T data) where T : struct, INetEvent
    {
        Type type = typeof(T);
        if (_handlers.TryGetValue(type, out var handlerObj) && handlerObj != null)
        {
            //拆箱并调用
            ((Action<T>)handlerObj).Invoke(data);
        }
    }
    #endregion
}