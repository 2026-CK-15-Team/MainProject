using UnityEngine;

public class Interactor : MonoBehaviour
{
    public float Range = 1.25f;
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private PlayerMovement movement;

    public Transform PromptTarget { get; private set; }

    private IInteractable currentTarget;

    private void OnEnable() => input.InteractPressed += OnInteractPressed;
    private void OnDisable() => input.InteractPressed -= OnInteractPressed;

    private void Update()
    {
        UpdateNearestTarget();
    }

    private void UpdateNearestTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, Range);

        IInteractable nearest = null;
        Transform nearestTransform = null;
        float nearestDist = float.MaxValue;
        float currentDist = float.MaxValue;
        bool currentStillInRange = false;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<IInteractable>(out var interactable)) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);

            if (interactable == currentTarget)
            {
                currentStillInRange = true;
                currentDist = dist;
            }

            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = interactable;
                nearestTransform = hit.transform;
            }
        }

        if (currentStillInRange && currentDist <= nearestDist + 0.001f)
        {
            nearest = currentTarget;
            nearestTransform = FindTransform(hits, currentTarget);
        }

        currentTarget = nearest;
        PromptTarget = nearestTransform;
    }

    private Transform FindTransform(Collider2D[] hits, IInteractable target)
    {
        foreach (var hit in hits)
            if (hit.TryGetComponent<IInteractable>(out var ia) && ia == target)
                return hit.transform;
        return null;
    }

    private void OnInteractPressed()
    {
        if (ModalGate.AnyModalOpen) return;
        if (movement.CurrentState == movement.DodgeState) return;

        currentTarget?.Interact();
    }
}