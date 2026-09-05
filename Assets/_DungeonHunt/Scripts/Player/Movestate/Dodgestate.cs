using UnityEngine;

public class DodgeState : IMovementState
{
    private bool invincibilityEndFired;

    public void Enter(PlayerMovement ctx)
    {
        Vector2 direction = ctx.Input.MoveInput != Vector2.zero
            ? ctx.Input.MoveInput.normalized
            : ctx.GetAimDirection();
        
        ctx.CurrentVelocity = direction * (ctx.DodgeLength / ctx.DodgeDuration);
        ctx.DodgeElapsed = 0f;
        ctx.ConsumeDodgeCharge();
        invincibilityEndFired = false;

        ctx.RaiseDodgeStart();
    }

    public void Tick(PlayerMovement ctx, float deltaTime)
    {
        ctx.DodgeElapsed += deltaTime;

        if (!invincibilityEndFired && ctx.DodgeElapsed >= ctx.DodgeInvincibleDuration)
        {
            invincibilityEndFired = true;
            ctx.RaiseDodgeInvincibilityEnd();
        }

        if (ctx.DodgeElapsed >= ctx.DodgeDuration)
        {
            IMovementState next = ctx.Input.MoveInput != Vector2.zero
                ? (IMovementState)ctx.MoveState
                : ctx.IdleState;
            ctx.ChangeState(next);
        }

    }

    public void Exit(PlayerMovement ctx)
    {
        ctx.CurrentVelocity = Vector2.zero;
        ctx.RaiseDodgeEnd();
    }
}