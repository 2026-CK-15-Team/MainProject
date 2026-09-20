using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] private Collider2D blockingCollider;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Color lockedColor = Color.red;
    [SerializeField] private Color unlockedColor = Color.green;

    public void Lock()
    {
        blockingCollider.enabled = true;
        if (visual != null) visual.color = lockedColor;
    }

    public void Unlock()
    {
        blockingCollider.enabled = false;
        if (visual != null) visual.color = unlockedColor;
    }
}