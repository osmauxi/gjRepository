using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
[System.Serializable]
public class MovementConfig 
{
    [Header("移动参数")]
    public float moveSpeed = 5f;
    public float rotateSmoothTime = 0.1f;
    public float rotateSpeed = 8f;
   
    [Header("重力参数")]
    public float gravity = -15f;
    public float maxFallSpeed = -20f;
    public float groundCheckRadius = 0.4f;
    public Vector3 GroundCheckOffset = new Vector3(0,-0.7f,0);
    public LayerMask groundLayer;

    [Header("模型参数")]
    public float playerHalfHeight = 1.0f;//角色半身长，因为适配帧同步手写物理代码，计算是从角色坐标点开始的，不是从胶囊体底部开始算的，所以这里要加个修正
}


//仅处理逻辑，不存储状态，作为纯逻辑计算单元存在
public class CoreMovementLogic : MonoBehaviour
//同步的逻辑层，只传入值并进行计算，然后返回结果，不能引用任何Unity的API，也不能有任何组件引用，因为这个方法是会在服务器跑的，服务器没那些东西
{
    private MovementConfig _config;
    private IPhysicsQuery _physics;

    public CoreMovementLogic(MovementConfig config, IPhysicsQuery physics)
    {
        _config = config;
        _physics = physics;
    }

    public PlayerNetworkState Execute(PlayerNetworkState currentState, PlayerInputPayload input, float deltaTime)
    {
        PlayerNetworkState newState = currentState;

        if (newState.DashCooldownTimer > 0)
            newState.DashCooldownTimer -= deltaTime;

        float groundHeight;
        newState.IsGrounded = _physics.CheckGround(newState.Position, _config.GroundCheckOffset, _config.groundCheckRadius, _config.groundLayer, out groundHeight);

        newState = ProcessNormalMove(newState, input, deltaTime, groundHeight);

        UpdateStateEnum(ref newState, input,deltaTime);//更新状态机枚举

        return newState;
    }

    private PlayerNetworkState ProcessNormalMove(PlayerNetworkState state, PlayerInputPayload input, float deltaTime, float groundHeight)
    {
        //计算旋转
        Quaternion targetRotation = Quaternion.Euler(0, input.LookAngleY, 0);

        state.Rotation = Quaternion.Slerp(state.Rotation,targetRotation,_config.rotateSpeed * deltaTime);

        Vector3 moveDirection = CalculateMoveDirection(input);//用于下方位移计算
        //计算重力
        if (state.IsGrounded && state.Velocity.y <= 0)
        {
            state.Velocity.y = 0f;

            // [关键代码] 强制吸附
            // 使用插值平滑吸附 (防止瞬移抖动)，但在高频帧同步中，直接赋值通常更好
            // 如果发现这里有画面抖动，是因为 groundHeight 变化太剧烈

            // 增加一个阈值：只有当高度差确实存在时才修
            if (Mathf.Abs(state.Position.y - groundHeight) > 0.001f)
            {
                state.Position.y = groundHeight + _config.playerHalfHeight;
            }
        }
        else
        {
            state.Velocity.y += _config.gravity * deltaTime;
        }

        //计算水平速度
        Vector3 horizontalVelocity = moveDirection * _config.moveSpeed;
        state.Velocity.x = horizontalVelocity.x;
        state.Velocity.z = horizontalVelocity.z;

        //更新位置状态
        state.Position += state.Velocity * deltaTime;

        return state;
    }

    private void UpdateStateEnum(ref PlayerNetworkState state, PlayerInputPayload input, float deltaTime) 
    {
        if(state.IsDashing)
            state.currentState = PlayerStateType.Dashing;
        else if(!state.IsGrounded)
            state.currentState = PlayerStateType.Falling;
        else if(input.MoveDirection.sqrMagnitude > 0.01f)
            state.currentState = PlayerStateType.Moving;
        else
            state.currentState = PlayerStateType.Idle;
    }

    private Vector3 CalculateMoveDirection(PlayerInputPayload input) 
    {
        if (input.MoveDirection.sqrMagnitude < 0.01f)
            return Vector3.zero;
        // 注意：input 里包含了 CameraForward，这使得逻辑层不需要知道 Camera 的存在
        // 我们假设 input.CameraForward 已经是水平投影过的（在输入收集层处理）
        return (input.CameraForward * input.MoveDirection.y + input.CameraRight * input.MoveDirection.x).normalized;
    }


}
