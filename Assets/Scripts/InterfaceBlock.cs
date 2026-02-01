using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public interface IMovementDriver//策略模式接口，用于局内实时切换状态同步和帧同步
{
    void Initialize(PlayerController controller);
    void OnUpdate(float deltaTime);
    void OnFixedUpdate(float deltaTime);
    void OnNetworkSpawn();
    void OnDisable();
}
public interface IPhysicsQuery//物理查询接口
{
    void Move(Vector3 motion);
    bool IsGrounded { get; }
    void SyncTransform(Vector3 position, Quaternion rotation);
    void SetRotation(Quaternion rotation);
    Vector3 Position { get; }
}

public interface INetEvent : INetworkSerializable
{
}

public interface ISkill
{
    PlayerNetworkState Execute(PlayerNetworkState currentState, float deltaTime, out bool isFinished);

    PlayerNetworkState OnEnter(PlayerNetworkState currentState);
}

public interface IDorpItem 
{
    public void PickUp(PlayerController controller);
}

public interface ITrap 
{
    public void TriggerTrap(PlayerController player);
}

public enum Masks 
{
    None,
    Panda,
    Dear,
    Monkey
}