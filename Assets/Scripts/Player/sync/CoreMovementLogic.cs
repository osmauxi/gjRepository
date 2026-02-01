using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class MovementConfig
{
    [Header("移动参数")]
    public float maxMoveSpeed = 5f;
    public float acceleration = 20f; //加速度
    public float deceleration = 25f; //减速度
    public float rotateSpeed = 13f;
    [Range(0.1f, 1f)]
    public float backwardSpeedRatio = 0.5f;
    [Range(0.1f, 1f)]
    public float strafeSpeedRatio = 0.8f;

    [Header("跳跃参数")]
    public AnimationCurve jumpVelocityCurve;
    public float jumpMaxTime = 0.8f; //曲线采样的最大时间
    public float gravity = -20f; //下落阶段的重力
    public float jumpCooldown = 0.5f;
    public LayerMask groundLayer;

    [Header("技能/攻击")]
    public float attackDuration = 0.8f;
    public float attackCooldown = 1.0f;

    [Header("变身数值修正")]
    public float pandaSpeedMultiplier = 0.6f;
    public float dearSpeedMultiplier = 1.2f;

    [Header("熊猫技能")]
    public float pandaDashSpeed = 20f;
    public float pandaDashDuration = 0.4f;
    public float pandaSkillCooldown = 3.0f;

    [Header("鹿技能")]
    public float dearTeleportMaxDist = 8.0f;
    public float dearSkillCooldown = 5.0f;
    public LayerMask teleportObstacleLayer;
}


//仅处理逻辑，不存储状态，作为纯逻辑计算单元存在
public class CoreMovementLogic : MonoBehaviour
{
    private MovementConfig _config;
    private IPhysicsQuery _physics;
    public ISkill CurrentSkill { get; private set; }

    public CoreMovementLogic(MovementConfig config, IPhysicsQuery physics)
    {
        _config = config;
        _physics = physics;
    }
    public void EquipSkill(ISkill newSkill)
    {
        CurrentSkill = newSkill;
    }

    public PlayerNetworkState Execute(PlayerNetworkState currentState, PlayerInputPayload input, float deltaTime)
    { 
        if (currentState.IsDead)
        {
            currentState.Velocity = Vector3.zero;
            currentState.currentState = PlayerStateType.Dead;
            return currentState;
        }

        PlayerNetworkState newState = currentState;

        if (newState.AttackCooldownTimer > 0)
            newState.AttackCooldownTimer -= deltaTime;
        if (newState.JumpCooldownTimer > 0)
            newState.JumpCooldownTimer -= deltaTime;
        if (newState.SkillDurationTimer > 0)
            newState.SkillDurationTimer -= deltaTime;

        if (!newState.IsAttacking)
        {
            newState = ProcessMoveAndRotation(newState, input, deltaTime);
        }

        bool isLocked = false;

        if (newState.IsUsingSkill)
        {
            if (CurrentSkill != null)
            {
                bool isFinished;
                newState = CurrentSkill.Execute(newState, deltaTime, out isFinished);
                if (isFinished) newState.IsUsingSkill = false;
            }
            else
            {
                newState.IsUsingSkill = false;
            }
            isLocked = true; // 放技能时锁住跳跃
        }
        else if (newState.IsAttacking)
        {
            newState.AttackDurationTimer += deltaTime;
            if (newState.AttackDurationTimer >= _config.attackDuration)
            {
                newState.IsAttacking = false;
            }

            isLocked = true;
            // 攻击状态强制归零速度（站桩）
            newState.Velocity.x = 0;
            newState.Velocity.z = 0;
        }

        if (!newState.IsUsingSkill && !newState.IsAttacking)
        {
            if (input.AttackPressed && newState.IsGrounded && newState.AttackCooldownTimer <= 0)
            {
                SkillSlotUI.Instance.StartCooldown(SkillSlotType.Attack);
                newState.IsAttacking = true;
                newState.AttackDurationTimer = 0f;
                newState.AttackCooldownTimer = _config.attackCooldown;

                newState.Rotation = Quaternion.Euler(0, input.LookAngleY, 0);

                isLocked = true;
                newState.Velocity.x = 0;
                newState.Velocity.z = 0;
            }
            else if (input.SkillPressed && CurrentSkill != null)
            {
                newState.IsUsingSkill = true;
                newState.IsJumping = false;

                Vector3 preSkillPos = newState.Position;

                newState = CurrentSkill.OnEnter(newState);

                if (Vector3.SqrMagnitude(newState.Position - preSkillPos) > 0.1f)
                {
                    _physics.SyncTransform(newState.Position, newState.Rotation);
                }

                isLocked = true;
            }
        }

        newState = ProcessGravityAndJump(newState, input, deltaTime, isLocked);

        _physics.SetRotation(newState.Rotation);
        Vector3 finalMotion = newState.Velocity * deltaTime;
        _physics.Move(finalMotion);

        newState.Position = _physics.Position;
        newState.IsGrounded = _physics.IsGrounded;

        if (newState.IsGrounded && !newState.IsJumping && newState.Velocity.y < 0)
        {
            newState.Velocity.y = -2f;
        }

        UpdateStateEnum(ref newState, input);
        return newState;
    }

    private PlayerNetworkState ProcessMoveAndRotation(PlayerNetworkState state, PlayerInputPayload input, float deltaTime)
    {
        Quaternion targetRotation = Quaternion.Euler(0, input.LookAngleY, 0);
        state.Rotation = Quaternion.Slerp(state.Rotation, targetRotation, _config.rotateSpeed * deltaTime);

        // 注意：这里只改 X 和 Z，不改 Y
        if (state.IsGrounded && !state.IsJumping)
        {
            Vector3 targetDir = CalculateMoveDirection(input);
            float currentMaxSpeed = _config.maxMoveSpeed;

            if (targetDir.sqrMagnitude > 0.01f)
            {
                Vector3 faceDir = state.Rotation * Vector3.forward;
                float dot = Vector3.Dot(targetDir, faceDir);
                if (dot >= 0)
                    currentMaxSpeed *= Mathf.Lerp(_config.strafeSpeedRatio, 1.0f, dot);
                else
                    currentMaxSpeed *= Mathf.Lerp(_config.strafeSpeedRatio, _config.backwardSpeedRatio, -dot);
            }

            Vector3 targetVelocity = targetDir * currentMaxSpeed;
            Vector3 currentHorizontal = new Vector3(state.Velocity.x, 0, state.Velocity.z);
            float accelRate = (input.MoveDirection.sqrMagnitude > 0.01f) ? _config.acceleration : _config.deceleration;
            Vector3 newHorizontal = Vector3.MoveTowards(currentHorizontal, targetVelocity, accelRate * deltaTime);

            state.Velocity.x = newHorizontal.x;
            state.Velocity.z = newHorizontal.z;
        }
        return state;
    }

    private PlayerNetworkState ProcessGravityAndJump(PlayerNetworkState state, PlayerInputPayload input, float deltaTime, bool isLocked)
    {
        if (!isLocked && input.JumpPressed && state.IsGrounded && !state.IsJumping && state.JumpCooldownTimer <= 0)
        {
            state.IsJumping = true;
            state.JumpTimer = 0f;
        }

        if (state.IsJumping)
        {
            state.JumpTimer += deltaTime;
            state.Velocity.y = _config.jumpVelocityCurve.Evaluate(state.JumpTimer);

            if (state.JumpTimer >= _config.jumpMaxTime)
            {
                state.IsJumping = false;
                state.JumpCooldownTimer = _config.jumpCooldown;
                SkillSlotUI.Instance.StartCooldown(SkillSlotType.Jump);
            }
        }
        else
        {
            if (!state.IsGrounded)
            {
                state.Velocity.y += _config.gravity * deltaTime;
            }
        }
        return state;
    }

    private void UpdateStateEnum(ref PlayerNetworkState state, PlayerInputPayload input)
    {
        if (state.IsAttacking)
            state.currentState = PlayerStateType.Attacking;
        else if (state.IsUsingSkill) //确保 Skill 状态能被正确设置
            state.currentState = PlayerStateType.Skill;
        else if (!state.IsGrounded)
            state.currentState = PlayerStateType.Falling;
        else if (input.MoveDirection.sqrMagnitude > 0.01f)
            state.currentState = PlayerStateType.Moving;
        else
            state.currentState = PlayerStateType.Idle;
    }

    private Vector3 CalculateMoveDirection(PlayerInputPayload input)
    {
        if (input.MoveDirection.sqrMagnitude < 0.01f) return Vector3.zero;
        return (input.CameraForward * input.MoveDirection.y + input.CameraRight * input.MoveDirection.x).normalized;
    }
}