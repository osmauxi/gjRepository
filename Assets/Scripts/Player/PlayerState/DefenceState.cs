using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefenceState : State
{
    public DefenceState(StateMachine stateMachine, string animBoolName, PlayerController _player) : base(stateMachine, animBoolName, _player)
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
    }
}
