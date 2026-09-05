public interface IMovementState
{
    void Enter(PlayerMovement ctx);
    void Tick(PlayerMovement ctx, float deltaTime);
    void Exit(PlayerMovement ctx);
}