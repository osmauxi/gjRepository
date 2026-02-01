using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillState : State
{
    public SkillState(StateMachine stateMachine, string animBoolName, PlayerController _player) : base(stateMachine, animBoolName, _player)
    {
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        if (!player.isDashing)
        {
            if (player.isGrounded)
                stateMachine.ChangeState(player.idleState);
            else
                stateMachine.ChangeState(player.jumpState);
        }
    }
}