using UnityEngine;


public class MoveState : IMovementState
{
    public void Enter(PlayerMovement ctx) { }

    public void Tick(PlayerMovement ctx, float deltaTime)
    {
        if (ctx.WantsDodge && ctx.DodgeCharge >= 1)
        {
            ctx.ChangeState(ctx.DodgeState);
            return;
        }

        if (ctx.Input.MoveInput == Vector2.zero)
        {
            ctx.ChangeState(ctx.IdleState);
            return;
        }

        Vector2 targetVelocity = ctx.Input.MoveInput.normalized * ctx.MoveMaxSpeed;
        ctx.CurrentVelocity = Vector2.MoveTowards(
            ctx.CurrentVelocity, targetVelocity, ctx.AccelSpeedPerSecond * deltaTime);
    }

    public void Exit(PlayerMovement ctx) { }
}