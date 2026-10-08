using UnityEngine;

// 회피: 이동 입력 방향(없으면 조준 방향)으로 고정 거리, 고정 시간 이동. 무적 → 후딜 (01-1 §5.1)
public class DodgeState : IMovementState
{
    private bool invincibilityEndFired;

    public void Enter(PlayerMovement ctx)
    {
        Vector2 direction = ctx.Input.MoveInput != Vector2.zero
            ? ctx.Input.MoveInput.normalized
            : ctx.GetAimDirection();

        ctx.DodgeDirection = direction;
        ctx.CurrentVelocity = direction * (ctx.DodgeLength / ctx.DodgeDuration);
        ctx.DodgeElapsed = 0f;
        ctx.JustDodgeUsed = false;
        ctx.ConsumeDodgeCharge();
        invincibilityEndFired = false;

        ctx.RaiseDodgeStart();
    }

    public void Tick(PlayerMovement ctx, float deltaTime)
    {
        ctx.DodgeElapsed += deltaTime;
        // 벽에 닿아도 회피 시간과 속도는 그대로 (벽 방향 성분은 이동 처리에서 제거됨)
        ctx.CurrentVelocity = ctx.DodgeDirection * (ctx.DodgeLength / ctx.DodgeDuration);

        if (!invincibilityEndFired && ctx.DodgeElapsed >= ctx.Balance.DodgeInvincibleDuration)
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
        // 회피 → 이동 복귀 시 속도는 0부터 다시 가속 (01-1 §5.1)
        ctx.CurrentVelocity = Vector2.zero;
        ctx.RaiseDodgeEnd();
    }
}
