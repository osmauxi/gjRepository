using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public interface IMovementDriver//策略模式接口，用于局内实时切换状态同步和帧同步
{
    // 初始化：把 Controller 传进去，方便 Driver 访问 RPC 和 组件
    void Initialize(PlayerController controller);

    // 对应 Unity 的 Update (处理输入、表现层插值)
    void OnUpdate(float deltaTime);

    // 对应 Unity 的 FixedUpdate (处理核心物理模拟)
    void OnFixedUpdate(float deltaTime);

    // 对应 OnNetworkSpawn (初始化状态)
    void OnNetworkSpawn();

    void OnDisable();
}
public interface IPhysicsQuery//物理查询接口，用于检测地面和碰撞
 //帧同步极其严格，要求所有方法必须具有确定性，也就是所有设备计算结果必须一致，physics这些方法是具有不确定性的，需要自己写实现
{
    bool CheckGround(Vector3 position,Vector3 offset, float radius, LayerMask layer,out float groundHeight);
    bool CheckCollision(Vector3 start, Vector3 end, float radius, LayerMask layer);
    bool CheckObstacle(Vector3 start, Vector3 end, float radius, LayerMask layer);
}

public interface INetEvent : INetworkSerializable
{
}