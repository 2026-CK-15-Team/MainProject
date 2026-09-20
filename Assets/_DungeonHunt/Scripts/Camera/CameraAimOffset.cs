using UnityEngine;

public class CameraAimOffset : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private float maxOffset = 0.65f;
    [SerializeField] private float followSpeed = 5f;

    private void Update()
    {
        Vector2 aimDir = AimUtility.ScreenPointToWorldDirection(input.AimScreenPosition, player.position);
        Vector3 targetPos = player.position + (Vector3)(aimDir * maxOffset);
        transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);
    }
}
