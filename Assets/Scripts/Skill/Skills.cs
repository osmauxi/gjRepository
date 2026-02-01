using UnityEngine;

// 熊猫技能：向面朝方向持续冲刺
public class PandaSkill : ISkill
{
    private float _timer;
    private Vector3 _dashDir;
    private MovementConfig _config;

    public void Initialize(MovementConfig config)
    {
        _config = config;
    }

    public PlayerNetworkState OnEnter(PlayerNetworkState currentState)
    {
        _timer = 0;
        // 锁定冲刺方向为当前面朝方向
        _dashDir = currentState.Rotation * Vector3.forward;
        currentState.IsUsingSkill = true;
        currentState.SkillDurationTimer = _config.pandaDashDuration;
        return currentState;
    }

    public PlayerNetworkState Execute(PlayerNetworkState currentState, float deltaTime, out bool isFinished)
    {
        _timer += deltaTime;

        // 持续赋予高速度
        currentState.Velocity = _dashDir * _config.pandaDashSpeed;
        // 冲刺期间保持水平，Y轴速度归零或保留重力看需求，这里简单处理设为0防止起飞
        currentState.Velocity.y = 0;

        if (_timer >= _config.pandaDashDuration)
        {
            isFinished = true;
            currentState.IsUsingSkill = false;
            currentState.Velocity = Vector3.zero; // 冲刺结束急停
        }
        else
        {
            isFinished = false;
        }

        return currentState;
    }
}

// 神鹿技能：向鼠标方向瞬移
public class DearSkill : ISkill
{
    private MovementConfig _config;
    private Vector3 _targetPos;

    public void Initialize(MovementConfig config, PlayerInputPayload input, Vector3 currentPos)
    {
        _config = config;

        // 方案 B：按面朝方向传送 (最稳健)
        Vector3 dir = Quaternion.Euler(0, input.LookAngleY, 0) * Vector3.forward;

        // 射线检测阻挡
        float dist = _config.dearTeleportMaxDist;
        if (Physics.Raycast(currentPos + Vector3.up, dir, out RaycastHit hit, dist, _config.teleportObstacleLayer))
        {
            dist = hit.distance - 0.5f; // 撞墙稍微后退一点
        }

        _targetPos = currentPos + dir * dist;
    }

    public PlayerNetworkState OnEnter(PlayerNetworkState currentState)
    {
        // 瞬移是瞬间完成的
        currentState.Position = _targetPos;
        // 可以加个无敌帧或者特效标记
        currentState.IsUsingSkill = true;
        return currentState;
    }

    public PlayerNetworkState Execute(PlayerNetworkState currentState, float deltaTime, out bool isFinished)
    {
        // 因为是瞬移，进来直接结束
        isFinished = true;
        currentState.IsUsingSkill = false;
        return currentState;
    }
}

public class MonkeySkill : ISkill
{
    // 暂时留空
    public PlayerNetworkState OnEnter(PlayerNetworkState currentState) { return currentState; }
    public PlayerNetworkState Execute(PlayerNetworkState currentState, float deltaTime, out bool isFinished)
    {
        isFinished = true; return currentState;
    }
}