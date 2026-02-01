using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateSyncDriver : IMovementDriver
{
    private PlayerController controller; //持有宿主引用

    [Header("网络设置")]//限制脚本的发包速率，不然按update帧数来卡飞掉
    public float networkSendRate = 0.02f;
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
       
    }

    public void OnUpdate(float deltaTime)
    {
        if (!controller.IsOwner)
        {
            controller.SmoothInterpolateTo(controller.ServerState, deltaTime);
        }
        else
        {
            
        }
    }

    public void OnFixedUpdate(float deltaTime)
    {
        //直接丢给Owner跑逻辑，完全信任
        if (controller.IsOwner)
        {
            HandleOwnerLogic(deltaTime);
        }
    }

    private void HandleOwnerLogic(float deltaTime)
    {
        if (controller.LocalState.IsDead) 
            return;

        controller.currentInput = controller.CollectInput();

        var newState = controller.LogicCore.Execute(controller.LocalState, controller.currentInput, deltaTime);

        controller.UpdateLocalState(newState);

        //controller.ApplyStateToView(newState);

        controller.ForceUpdateNetState(newState);
    }

    public void OnDisable()
    {
        
    }
}
