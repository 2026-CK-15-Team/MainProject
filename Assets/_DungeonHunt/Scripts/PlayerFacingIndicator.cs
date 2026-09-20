using UnityEngine;

public class PlayerFacingIndicator : MonoBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Transform triangle;

    private void Update()
    {
        Vector2 aimDir = movement.GetAimDirection();
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        triangle.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
