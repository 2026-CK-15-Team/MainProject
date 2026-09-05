using UnityEngine;

public static class AimUtility
{
    public static Vector2 ScreenPointToWorldDirection(Vector2 screenPosition, Vector3 origin)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.right;

        Vector3 screenPoint = screenPosition;
        screenPoint.z = -cam.transform.position.z;
        Vector3 worldPoint = cam.ScreenToWorldPoint(screenPoint);

        Vector2 direction = (Vector2)worldPoint - (Vector2)origin;
        return direction == Vector2.zero ? Vector2.right : direction.normalized;
    }
}