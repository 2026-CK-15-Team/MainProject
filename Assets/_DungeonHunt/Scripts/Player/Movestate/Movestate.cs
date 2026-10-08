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

        // 방향은 즉시 바꾸고 속력만 선형 가감속 (01-1 §7.1)
        Vector2 direction = ctx.Input.MoveInput.normalized;
        float speed = Mathf.MoveTowards(ctx.CurrentVelocity.magnitude, ctx.MoveMaxSpeed, ctx.AccelSpeedPerSecond * deltaTime);
        if (speed > ctx.MoveMaxSpeed) speed = ctx.MoveMaxSpeed;
        ctx.CurrentVelocity = direction * speed;
    }

    public void Exit(PlayerMovement ctx) { }
}
