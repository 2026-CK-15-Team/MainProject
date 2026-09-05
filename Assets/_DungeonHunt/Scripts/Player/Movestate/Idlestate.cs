using UnityEngine;

public class IdleState : IMovementState
{
    public void Enter(PlayerMovement ctx) { }

    public void Tick(PlayerMovement ctx, float deltaTime)
    {
        if (ctx.WantsDodge && ctx.DodgeCharge >= 1)
        {
            ctx.ChangeState(ctx.DodgeState);
            return;
        }

        if (ctx.Input.MoveInput != Vector2.zero)
        {
            ctx.ChangeState(ctx.MoveState);
            return;
        }

        ctx.CurrentVelocity = Vector2.MoveTowards(
            ctx.CurrentVelocity, Vector2.zero, ctx.DecelSpeedPerSecond * deltaTime);
    }

    public void Exit(PlayerMovement ctx) { }
}