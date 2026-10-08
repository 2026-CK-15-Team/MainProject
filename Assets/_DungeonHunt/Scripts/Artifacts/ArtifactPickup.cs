using System.Collections.Generic;
using UnityEngine;

// 필드 배치형 아티팩트 오브젝트 (07 §4.8). F 상호작용 → 획득 UI.
// 획득 세션 상태(현재 후보, 확인 ID, 리롤 횟수)를 같은 층에 있는 동안 보존 (§4.13 7번).
// 상호작용 전에는 아이콘, 등급 색, 빛기둥만 보여주고 이름/효과는 획득 UI에서 공개 (08 §5.5).
public class ArtifactPickup : MonoBehaviour, IInteractable
{
    private static readonly List<ArtifactPickup> active = new List<ArtifactPickup>();
    public static IReadOnlyList<ArtifactPickup> Active => active;

    public ArtifactDefinition Candidate { get; private set; }
    public AcquisitionOrigin Origin { get; private set; }
    // 처음 확인하는 세션에서만 리롤 가능. 미선택/드랍 뒤 재획득은 리롤 없음
    public bool CanReroll { get; private set; }
    public int RerollsUsed { get; private set; }
    public bool SessionStarted { get; private set; }
    public RoomRuntime Room { get; set; }
    public readonly HashSet<string> SessionSeen = new HashSet<string>();

    private SpriteRenderer icon;
    private SpriteRenderer beam;

    public static ArtifactPickup Spawn(ArtifactDefinition def, Vector2 position, AcquisitionOrigin origin, bool allowReroll, RoomRuntime room)
    {
        var go = new GameObject($"Artifact_{def.Id}");
        go.transform.position = position;
        if (room != null) go.transform.SetParent(room.PickupRoot, true);
        var pickup = go.AddComponent<ArtifactPickup>();
        pickup.Candidate = def;
        pickup.Origin = origin;
        pickup.CanReroll = allowReroll && ArtifactGradeUtility.IsFieldOrigin(origin);
        pickup.Room = room;
        pickup.BuildVisual();
        return pickup;
    }

    // 교체/버리기/획득 취소로 떨군 복사본. 플레이어 0.75H 안 안전 지점 (07 §4.12)
    public static ArtifactPickup SpawnDropped(ArtifactDefinition def, AcquisitionOrigin origin)
    {
        PlayerController player = GameServices.Player;
        Vector2 at = player != null ? FindSafeDropPoint(player.Position) : Vector2.zero;
        RoomRuntime room = GameServices.Floor != null ? GameServices.Floor.CurrentRoom : null;
        return Spawn(def, at, origin, false, room);
    }

    public static Vector2 FindSafeDropPoint(Vector2 origin)
    {
        float radius = GameBalance.Current.Artifact.FieldDropSearchRadius;
        Vector2 best = origin;
        float bestDistance = float.MaxValue;
        for (int ring = 0; ring <= 3; ring++)
        {
            float r = radius * ring / 3f;
            int samples = ring == 0 ? 1 : 12;
            for (int i = 0; i < samples; i++)
            {
                Vector2 p = origin + Geometry2D.FromAngle(i * 360f / samples) * r;
                if (Physics2D.OverlapCircle(p, 0.25f, GameLayers.WallMask) != null) continue;
                if (OverlapsInteractable(p)) continue;
                float d = (p - origin).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            if (bestDistance < float.MaxValue) return best;
        }
        return origin;
    }

    private static bool OverlapsInteractable(Vector2 p)
    {
        foreach (IInteractable item in Interactables.All)
        {
            if (item == null) continue;
            if (Vector2.Distance(item.ClosestPoint(p), p) < 0.4f) return true;
        }
        return false;
    }

    private void BuildVisual()
    {
        Color gradeColor = ArtifactGradeUtility.Color(Candidate.Grade);
        beam = PlaceholderArt.CreateShape("Beam", transform, PlaceholderArt.Square, new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0.35f), 1f, 80);
        beam.transform.localScale = new Vector3(0.18f, 1.6f, 1f);
        beam.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        Sprite sprite = Candidate.Icon != null ? Candidate.Icon : PlaceholderArt.Diamond;
        icon = PlaceholderArt.CreateShape("Icon", transform, sprite, Candidate.Icon != null ? Color.white : gradeColor, 0.4f, 81);
        if (Candidate.Icon == null) icon.transform.localRotation = Quaternion.Euler(0, 0, 45f);
    }

    private void RefreshVisual()
    {
        if (icon != null) Destroy(icon.gameObject);
        if (beam != null) Destroy(beam.gameObject);
        BuildVisual();
    }

    private void OnEnable()
    {
        active.Add(this);
        Interactables.Register(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
        Interactables.Unregister(this);
    }

    private void Update()
    {
        if (icon != null)
            icon.transform.localPosition = new Vector3(0f, 0.1f + Mathf.Sin(Time.time * 3f) * 0.05f, 0f);
    }

    // ---------- IInteractable ----------
    public bool CanInteract => Candidate != null && !InputGate.GameplayBlocked;
    public Vector2 ClosestPoint(Vector2 from)
    {
        Vector2 center = transform.position;
        Vector2 dir = from - center;
        return dir.magnitude <= 0.25f ? from : center + dir.normalized * 0.25f;
    }
    public Vector3 PromptPosition => transform.position + new Vector3(0f, 0.6f, 0f);

    public void Interact(PlayerController player)
    {
        if (!SessionStarted)
        {
            SessionStarted = true;
            SessionSeen.Add(Candidate.Id);
        }
        GameEvents.Raise(GameEvents.ArtifactPickedUp, Candidate);
        GameServices.UI?.OpenAcquire(this);
    }

    // ---------- 획득 UI 선택지 ----------

    public bool RerollVisible => CanReroll && RerollsUsed < GameBalance.Current.Artifact.RerollMaxCount;
    public bool NextRerollIsFree => RerollsUsed < GameBalance.Current.Artifact.RerollFreeCount;
    public int NextRerollCost => NextRerollIsFree ? 0 : GameBalance.Current.Shop.RerollCost;

    public bool RerollEnabled
    {
        get
        {
            if (!RerollVisible) return false;
            ArtifactSystem artifacts = GameServices.Artifacts;
            if (artifacts == null || !artifacts.HasAnyCandidate(Origin, SessionSeen)) return false;
            if (!NextRerollIsFree && (GameServices.Wallet == null || GameServices.Wallet.Balance < NextRerollCost)) return false;
            return true;
        }
    }

    // 새 후보 예약 성공 뒤에만 비용, 횟수, 후보를 한 번에 확정 (07 §4.6, §4.13 4번)
    public bool TryReroll()
    {
        if (!RerollEnabled) return false;
        ArtifactSystem artifacts = GameServices.Artifacts;
        ArtifactDefinition next = artifacts.Draw(Origin, SessionSeen);
        if (next == null) return false;
        int cost = NextRerollCost;
        if (cost > 0 && !GameServices.Wallet.TrySpend(cost)) return false;

        artifacts.Reserve(next);
        artifacts.Release(Candidate);
        Candidate = next;
        SessionSeen.Add(next.Id);
        RerollsUsed++;
        RefreshVisual();
        GameEvents.Raise(GameEvents.ArtifactRerolled, next);
        return true;
    }

    // 유지: 즉시 장착 또는 획득 대기 모드 (07 §4.10)
    public void Keep()
    {
        ArtifactDefinition def = Candidate;
        GameEvents.Raise(GameEvents.ArtifactAcquired, def);
        if (GameServices.Artifacts.TryAutoEquip(def))
        {
            Consume();
            return;
        }
        GameServices.UI?.OpenInventoryPending(def, Origin, onEquipped: Consume, onCancelled: OnPendingCancelled);
    }

    // 획득 대기 취소: 필드 배치형은 후보/리롤 상태 유지한 채 필드에 남음
    private void OnPendingCancelled() { }

    // 미선택: 오브젝트는 원래 자리에 남고 세션 종료. 다시 주우면 재획득 → 리롤 없음 (§4.8 6번)
    public void NotSelected()
    {
        GameEvents.Raise(GameEvents.ArtifactNotSelected, Candidate);
        CanReroll = false;
    }

    private void Consume()
    {
        Destroy(gameObject);
    }
}
