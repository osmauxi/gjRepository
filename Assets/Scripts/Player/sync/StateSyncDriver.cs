using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateSyncDriver : IMovementDriver
//一个具体的策略，真正约束当前同步状态下的运行逻辑
//状态同步驱动器，封装状态同步的独有逻辑
{
    private PlayerController controller; //持有宿主引用

    //
    //这里维护该模式独有的变量
    //

    [Header("网络设置")]//限制脚本的发包速率，不然按update帧数来卡飞掉
    public float networkSendRate = 0.05f;//发送频率：0.05秒发一次 = 每秒20次。
    private float _networkTimer;
    private const float HEARTBEAT_INTERVAL = 1.0f;//如果超过1秒没有发包（因为玩家没动），强制发一个包确认状态
    private float _heartbeatTimer;//心跳机制：防止丢包导致状态卡死
    private PlayerInputPayload _lastSentInput;

    public void Initialize(PlayerController controller)
    {
        this.controller = controller;
    }

    public void OnNetworkSpawn()
    {
        // 状态同步需要在开始时监听变量变化
        // controller.NetState.OnValueChanged += ... (这里需要Controller公开NetState)
    }

    public void OnUpdate(float deltaTime)
    {
        //非Owner需要插值
        if (!controller.IsOwner)
        {
            controller.SmoothInterpolateTo(controller.ServerState, deltaTime);
        }
    }

    public void OnFixedUpdate(float deltaTime)
    {
        if (controller.IsServer)
        {
            //服务器一直跑模拟然后更新服务器状态
            var newState = controller.LogicCore.Execute(controller.ServerState, controller.ServerInput, deltaTime);
            controller.UpdateServerState(newState);
        }

        if (controller.IsOwner)
        {
            //客户端预测 + 发送
            HandleOwnerLogic(deltaTime);
        }
    }

    private void HandleOwnerLogic(float deltaTime)
    {
        //采集输入
        controller.currentInput = controller.CollectInput();

        //本地预测
        var newState = controller.LogicCore.Execute(controller.LocalState, controller.currentInput, deltaTime);
        controller.UpdateLocalState(newState);
        controller.ApplyStateToView(newState);

        //发包
        _networkTimer += deltaTime;
        _heartbeatTimer += deltaTime;

        bool isInputChanged = controller.IsInputChanged(controller.currentInput, _lastSentInput);
        bool isHeartbeat = _heartbeatTimer >= HEARTBEAT_INTERVAL;
        bool isSendTime = _networkTimer >= networkSendRate;

        if (isInputChanged || (isSendTime && isHeartbeat))
        {
            if (isInputChanged || isSendTime)//有输入时是立马发包，无输入时看心跳
            {
                controller.SendInputRpc(controller.currentInput);

                _lastSentInput = controller.currentInput;
                _networkTimer = 0;
                if (isInputChanged) _heartbeatTimer = 0;
            }
        }
    }

    public void OnDisable()
    {
        
    }
}
