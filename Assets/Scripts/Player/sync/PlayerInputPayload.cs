using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//纯数据结构封装状态 C

//客户端打包发送给服务器的输入数据
public struct PlayerInputPayload : INetworkSerializable
{
    public Vector2 MoveDirection;//基于本地摄像机的前后左右输入
    public float LookAngleY;//鼠标指向的Y轴角度
    public bool AttackPressed;
    public bool JumpPressed;
    public bool SkillPressed;

    public Vector3 CameraForward;
    public Vector3 CameraRight;

    public PlayerStateType currentState;   // 客户端当前的玩家状态

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MoveDirection);
        serializer.SerializeValue(ref LookAngleY);
        serializer.SerializeValue(ref AttackPressed);
        serializer.SerializeValue(ref JumpPressed);
        serializer.SerializeValue(ref SkillPressed);
        serializer.SerializeValue(ref CameraForward);
        serializer.SerializeValue(ref CameraRight);
    }
}

// 服务器打包发送给客户端的玩家状态数据
public struct PlayerNetworkState : INetworkSerializable
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;

    public bool IsGrounded;
    public bool IsAttacking;
    public bool IsJumping;
    public bool IsUsingSkill;
    public PlayerStateType currentState;

    public float AttackCooldownTimer;
    public float AttackDurationTimer; 
    public float JumpTimer;
    public float JumpCooldownTimer;
    public float SkillDurationTimer;

    public bool IsDead;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref Velocity);

        serializer.SerializeValue(ref IsGrounded);
        serializer.SerializeValue(ref IsAttacking);
        serializer.SerializeValue(ref IsJumping);
        serializer.SerializeValue(ref IsUsingSkill);
        serializer.SerializeValue(ref currentState);

        serializer.SerializeValue(ref AttackCooldownTimer);
        serializer.SerializeValue(ref AttackDurationTimer);
        serializer.SerializeValue(ref JumpTimer);
        serializer.SerializeValue(ref JumpCooldownTimer);
        serializer.SerializeValue(ref SkillDurationTimer);

        serializer.SerializeValue(ref IsDead);
    }
}