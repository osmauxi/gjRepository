using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JumpState : State
{
    public JumpState(StateMachine stateMachine, string animBoolName, PlayerController _player) : base(stateMachine, animBoolName, _player)
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

        if (player.isGrounded && player.velocity.y <= 0)
            stateMachine.ChangeState(player.idleState);
    }
}