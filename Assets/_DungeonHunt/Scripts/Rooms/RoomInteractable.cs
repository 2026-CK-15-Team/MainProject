using System;
using UnityEngine;

// 룸 고정 오브젝트(NPC, 카운터, 층 이동 오브젝트, 상자)의 F 상호작용 공용 컴포넌트
public class RoomInteractable : MonoBehaviour, IInteractable
{
    public Func<bool> CanInteractCheck;
    public Action<PlayerController> OnInteract;
    public float Radius = 0.45f;

    private SpriteRenderer visual;
    private TextMesh label;

    public SpriteRenderer Visual => visual;

    public static RoomInteractable Create(string name, Transform parent, Vector2 position, Sprite sprite, Color color, float size, string labelText)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        var interactable = go.AddComponent<RoomInteractable>();
        interactable.Radius = size * 0.5f;
        interactable.visual = PlaceholderArt.CreateShape("Visual", go.transform, sprite, color, size, 85);
        if (!string.IsNullOrEmpty(labelText))
        {
            interactable.label = PlaceholderArt.CreateWorldText("Label", go.transform, labelText, 0.035f, new Color(1f, 1f, 1f, 0.85f), 86);
            interactable.label.transform.localPosition = new Vector3(0f, -size * 0.5f - 0.15f, 0f);
            interactable.label.font = PlaceholderArt.UIFont;
            interactable.label.GetComponent<MeshRenderer>().sharedMaterial = interactable.label.font.material;
        }
        return interactable;
    }

    public void SetColor(Color color)
    {
        if (visual != null) visual.color = color;
    }

    private void OnEnable() => Interactables.Register(this);
    private void OnDisable() => Interactables.Unregister(this);

    public bool CanInteract => CanInteractCheck == null || CanInteractCheck();

    public Vector2 ClosestPoint(Vector2 from)
    {
        Vector2 center = transform.position;
        Vector2 dir = from - center;
        return dir.magnitude <= Radius ? from : center + dir.normalized * Radius;
    }

    public Vector3 PromptPosition => transform.position + new Vector3(0f, Radius + 0.3f, 0f);

    public void Interact(PlayerController player) => OnInteract?.Invoke(player);
}
