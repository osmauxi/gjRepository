using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//纯数据结构封装状态 C

// 客户端打包发送给服务器的输入数据
public struct PlayerInputPayload : INetworkSerializable
{
    public Vector2 MoveDirection;          // 基于本地摄像机的前后左右输入
    public float LookAngleY;               // 鼠标指向的Y轴角度
    public bool DashPressed;
    public float Timestamp;

    public Vector3 CameraForward;          // 客户端的摄像机前方向（世界坐标）
    public Vector3 CameraRight;           // 客户端的摄像机右方向（世界坐标）

    public PlayerStateType currentState;   // 客户端当前的玩家状态

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MoveDirection);
        serializer.SerializeValue(ref LookAngleY);
        serializer.SerializeValue(ref DashPressed);
        serializer.SerializeValue(ref Timestamp);
        serializer.SerializeValue(ref CameraForward);
        serializer.SerializeValue(ref CameraRight);
        serializer.SerializeValue(ref currentState);
    }
}

// 服务器打包发送给客户端的玩家状态数据
public struct PlayerNetworkState : INetworkSerializable
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;

    public bool IsGrounded;
    public bool IsDashing;
    public PlayerStateType currentState;

    public float DashCooldownTimer; // 冲刺冷却倒计时 (0表示冷却完毕)
    public float DashDurationTimer;

    public Vector3 DashStartPosition;
    public Vector3 DashDirection;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref Velocity);

        serializer.SerializeValue(ref IsGrounded);
        serializer.SerializeValue(ref IsDashing);
        serializer.SerializeValue(ref currentState);

        serializer.SerializeValue(ref DashCooldownTimer);
        serializer.SerializeValue(ref DashDurationTimer);

        serializer.SerializeValue(ref DashStartPosition);
        serializer.SerializeValue(ref DashDirection);
    }

    public int GetHash() 
    {
        unchecked//允许存储溢出，溢出会从最底部重新开始，也变相增强哈希的随机性
        {
            int hash = 17;

            //Quantize方法将浮点数转换为整数，通过乘以1000来保留三位小数，从而减少浮点数精度问题对哈希值的影响
            int Quantize(float value) => (int)(value * 1000f);
            //乘一个质数31来扩散哈希值，质数也能减少哈希冲突，也区分a+b与b+a的情况
            hash = hash * 31 + Quantize(Position.x);
            hash = hash * 31 + Quantize(Position.y);
            hash = hash * 31 + Quantize(Position.z);

            hash = hash * 31 + (IsGrounded ? 1 : 0);
            hash = hash * 31 + (IsDashing ? 1 : 0);
            hash = hash * 31 + (int)currentState;

            hash = hash * 31 + Quantize(DashCooldownTimer);

            return hash;
        }
    }
    //Quantize()核心作用是「将连续的浮点数（如位置、旋转）转换为离散的整数 / 有限精度浮点数」,减少浮点精度，网络传输也更高效
}