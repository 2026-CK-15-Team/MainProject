using System.Collections.Generic;
using UnityEngine;

// F 상호작용 대상 (상점 NPC, 도박 NPC, 필드 아티팩트, 층 이동 오브젝트, 상자)
public interface IInteractable
{
    bool CanInteract { get; }
    // 플레이어 중심에서 대상 충돌체 최근접점 (01-1 §3)
    Vector2 ClosestPoint(Vector2 from);
    Vector3 PromptPosition { get; }
    void Interact(PlayerController player);
}

public static class Interactables
{
    private static readonly List<IInteractable> all = new List<IInteractable>();
    public static IReadOnlyList<IInteractable> All => all;
    public static void Register(IInteractable item) { if (!all.Contains(item)) all.Add(item); }
    public static void Unregister(IInteractable item) => all.Remove(item);
}

// 상호작용 후보 선정과 F 안내 표시 (08 §4 F 입력 안내의 후보 선정)
// - Interact_DefaultRange 안, 벽/잠금 이펙트로 막히지 않은 대상만
// - 가장 가까운 하나. 같은 거리면 현재 선택 유지
public class PlayerInteractor : MonoBehaviour
{
    public IInteractable Current { get; private set; }

    private TextMesh prompt;

    private void Awake()
    {
        prompt = PlaceholderArt.CreateWorldText("InteractPrompt", transform, "F", 0.06f, Color.white, 500);
        prompt.gameObject.SetActive(false);
    }

    private void Update()
    {
        Current = FindBest();
        bool show = Current != null && !InputGate.GameplayBlocked;
        prompt.gameObject.SetActive(show);
        if (show)
            prompt.transform.position = Current.PromptPosition;
    }

    // UIManager가 입력 우선순위에 맞춰 호출. 상호작용했으면 true (같은 틱 다른 일회성 입력 소비)
    public bool TryInteract(PlayerController player)
    {
        if (Current == null || !Current.CanInteract) return false;
        if (player.Movement.IsDodging) return false;
        Current.Interact(player);
        return true;
    }

    private IInteractable FindBest()
    {
        if (GameServices.Player == null || GameServices.Player.Health.IsDead) return null;
        Vector2 origin = transform.position;
        float range = GameBalance.Current.Player.InteractRange;
        IInteractable best = null;
        float bestDistance = float.MaxValue;
        const float tie = 0.0001f;

        foreach (IInteractable item in Interactables.All)
        {
            if (item == null || !item.CanInteract) continue;
            Vector2 point = item.ClosestPoint(origin);
            float distance = Vector2.Distance(origin, point);
            if (distance > range) continue;
            if (!Geometry2D.HasLineOfSight(origin, point)) continue;

            bool better = distance < bestDistance - tie
                || (Mathf.Abs(distance - bestDistance) <= tie && item == Current);
            if (better)
            {
                best = item;
                bestDistance = distance;
            }
        }
        return best;
    }
}
