using System.Collections.Generic;
using UnityEngine;

// 09 게임플레이 연출 출력 계층. 논리 결과가 확정된 뒤 요청만 받아 출력함 (시간 권한 없음).
// 최종 아트/사운드가 없을 때도 기본 도형으로 공격 예고, 탄 구분, 피격, 처치, 상호작용 가능 여부를 전달.
public class FeedbackService : MonoBehaviour
{
    private class TimedEffect
    {
        public GameObject Go;
        public float EndTime;
        public float Duration;
        public Vector3 Velocity;
        public SpriteRenderer Sprite;
        public TextMesh Text;
        public LineRenderer Line;
        public Color BaseColor;
        public bool Grow;
        public float GrowRate = 4f;
    }

    private readonly List<TimedEffect> effects = new List<TimedEffect>();
    private readonly HashSet<int> hitstopAttackIds = new HashSet<int>();
    // 같은 공격이 같은 적에게 준 피해는 숫자 하나로 (09 §5)
    private readonly Dictionary<(int, Vector2Int), TimedEffect> damageNumbers = new Dictionary<(int, Vector2Int), TimedEffect>();

    private void Awake() => GameServices.Feedback = this;

    private void OnEnable()
    {
        GameEvents.JustDodgeTriggered += OnJustDodge;
        GameEvents.SetTierChanged += OnSetTierChanged;
        GameEvents.ArtifactAcquired += OnArtifactAcquired;
    }

    private void OnDisable()
    {
        GameEvents.JustDodgeTriggered -= OnJustDodge;
        GameEvents.SetTierChanged -= OnSetTierChanged;
        GameEvents.ArtifactAcquired -= OnArtifactAcquired;
    }

    // ---------- 명중, 피격, 처치 ----------

    public void OnMonsterHit(DamageInfo info, Vector2 position)
    {
        ShowDamageNumber(info, position);
        SpawnHitSpark(info.HitPoint != Vector2.zero ? info.HitPoint : position, info.IsCritical);
        if (info.Source != DamageSource.Player) return;
        FeedbackBalance b = GameBalance.Current.Feedback;
        Vector2 shake = info.Weapon switch
        {
            WeaponType.Rifle => b.RifleHitShake,
            WeaponType.Pistol => b.PistolHitShake,
            _ => b.ShotgunHitShake,
        };
        GameServices.Camera?.Shake(shake.x, shake.y);
    }

    // 같은 AttackExecutionID의 처치는 히트스톱 최대 1회 (09 §6)
    public void OnMonsterKilled(DamageInfo info, Vector2 position, Color color)
    {
        if (info.AttackExecutionId < 0 || hitstopAttackIds.Add(info.AttackExecutionId))
            GameClock.RequestHitstop(GameBalance.Current.Feedback.KillHitstopDuration);
        if (hitstopAttackIds.Count > 256) hitstopAttackIds.Clear();
        Burst(position, color, 8, 0.25f, 2.5f);
    }

    public void OnPlayerHit()
    {
        Vector2 shake = GameBalance.Current.Feedback.PlayerHitShake;
        GameServices.Camera?.Shake(shake.x, shake.y);
    }

    private void ShowDamageNumber(DamageInfo info, Vector2 position)
    {
        if (damageNumbers.Count > 200) damageNumbers.Clear();
        Vector2Int key2 = Vector2Int.RoundToInt(position * 2f);
        var key = (info.AttackExecutionId, key2);
        Color color = info.IsCritical ? new Color(1f, 0.8f, 0.2f) : Color.white;
        float size = info.IsCritical ? 0.06f : 0.045f;
        if (info.AttackExecutionId >= 0 && damageNumbers.TryGetValue(key, out TimedEffect existing) && existing.Go != null)
        {
            existing.Text.text = info.Amount.ToString();
            return;
        }
        TextMesh text = PlaceholderArt.CreateWorldText("Damage", null, info.Amount.ToString(), size, color, 600);
        text.transform.position = position + new Vector2(Random.Range(-0.15f, 0.15f), 0.35f);
        var effect = new TimedEffect { Go = text.gameObject, Text = text, Duration = 0.6f, EndTime = Time.unscaledTime + 0.6f, Velocity = new Vector3(0f, 0.8f, 0f), BaseColor = color };
        effects.Add(effect);
        if (info.AttackExecutionId >= 0) damageNumbers[key] = effect;
    }

    // 적중 이펙트. 치명타는 숫자 색/크기와 이 이펙트만 강화 (09 §5 4번, 흔들림/히트스톱은 그대로)
    public void SpawnHitSpark(Vector2 position, bool critical)
    {
        if (critical)
        {
            SpriteRenderer ring = PlaceholderArt.CreateShape("CritRing", null, PlaceholderArt.Ring, new Color(1f, 0.8f, 0.2f, 0.95f), 0.35f, 235);
            ring.transform.position = position;
            effects.Add(new TimedEffect { Go = ring.gameObject, Sprite = ring, Duration = 0.18f, EndTime = Time.unscaledTime + 0.18f, BaseColor = ring.color, Grow = true, GrowRate = 8f });
            Burst(position, new Color(1f, 0.85f, 0.3f), 6, 0.1f, 3f);
        }
        else
        {
            Burst(position, new Color(1f, 1f, 0.9f), 3, 0.07f, 1.8f);
        }
    }

    // ---------- 무기 ----------

    public void ShowMuzzleFlash(Vector2 position, Vector2 direction)
    {
        SpriteRenderer flash = PlaceholderArt.CreateShape("MuzzleFlash", null, PlaceholderArt.Circle, new Color(1f, 0.95f, 0.6f, 0.9f), 0.22f, 220);
        flash.transform.position = position;
        AddTimed(flash.gameObject, 0.05f, flash);
    }

    public void ShowShotgunCone(Vector2 origin, Vector2 direction, float range, float arc)
    {
        LineRenderer line = PlaceholderArt.CreateLine("ShotgunCone", null, new Color(1f, 0.8f, 0.4f, 0.8f), 0.04f, 220);
        const int points = 10;
        line.positionCount = points + 3;
        line.SetPosition(0, origin);
        for (int i = 0; i <= points; i++)
        {
            float a = -arc * 0.5f + arc * i / points;
            line.SetPosition(i + 1, origin + Geometry2D.Rotate(direction.normalized, a) * range);
        }
        line.SetPosition(points + 2, origin);
        effects.Add(new TimedEffect { Go = line.gameObject, Line = line, Duration = 0.08f, EndTime = Time.unscaledTime + 0.08f, BaseColor = line.startColor });
    }

    public void ShowSlash(Vector2 origin, Vector2 direction, float range, float arc)
    {
        LineRenderer line = PlaceholderArt.CreateLine("Slash", null, new Color(1f, 0.5f, 0.3f, 0.9f), 0.07f, 220);
        const int points = 8;
        line.positionCount = points + 1;
        for (int i = 0; i <= points; i++)
        {
            float a = -arc * 0.5f + arc * i / points;
            line.SetPosition(i, origin + Geometry2D.Rotate(direction.normalized, a) * range);
        }
        effects.Add(new TimedEffect { Go = line.gameObject, Line = line, Duration = 0.12f, EndTime = Time.unscaledTime + 0.12f, BaseColor = line.startColor });
    }

    public void ShowHitscan(Vector2 from, Vector2 to)
    {
        LineRenderer line = PlaceholderArt.CreateLine("Hitscan", null, new Color(1f, 1f, 0.6f, 1f), 0.12f, 230);
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        effects.Add(new TimedEffect { Go = line.gameObject, Line = line, Duration = 0.15f, EndTime = Time.unscaledTime + 0.15f, BaseColor = line.startColor });
    }

    // ---------- 벽, 탄 소멸, 룸, 보상 ----------

    public void SpawnWallImpact(Vector2 position) => Burst(position, new Color(0.85f, 0.85f, 0.8f), 3, 0.12f, 1.2f);

    // 적 탄 공통 소멸 표현 1종 (09 §7)
    public void SpawnVanish(Vector2 position)
    {
        SpriteRenderer ring = PlaceholderArt.CreateShape("Vanish", null, PlaceholderArt.Ring, new Color(1f, 0.6f, 0.6f, 0.8f), 0.2f, 215);
        ring.transform.position = position;
        effects.Add(new TimedEffect { Go = ring.gameObject, Sprite = ring, Duration = 0.15f, EndTime = Time.unscaledTime + 0.15f, BaseColor = ring.color, Grow = true });
    }

    // 권총 탄과 적 탄 상쇄: 일반 명중과 다른 짧은 섬광 + 원형 파동 (09 §5 6번)
    public void SpawnProjectileCancel(Vector2 position)
    {
        SpriteRenderer flash = PlaceholderArt.CreateShape("CancelFlash", null, PlaceholderArt.Circle, new Color(0.6f, 1f, 1f, 1f), 0.3f, 236);
        flash.transform.position = position;
        AddTimed(flash.gameObject, 0.06f, flash);
        SpriteRenderer ring = PlaceholderArt.CreateShape("CancelRing", null, PlaceholderArt.Ring, new Color(0.5f, 0.95f, 1f, 0.9f), 0.25f, 235);
        ring.transform.position = position;
        effects.Add(new TimedEffect { Go = ring.gameObject, Sprite = ring, Duration = 0.2f, EndTime = Time.unscaledTime + 0.2f, BaseColor = ring.color, Grow = true, GrowRate = 9f });
    }

    public void SpawnRewardBurst(Vector2 position, Color color) => Burst(position, color, 6, 0.18f, 1.8f);

    public void SpawnSummonEffect(Vector2 position)
    {
        SpriteRenderer ring = PlaceholderArt.CreateShape("Summon", null, PlaceholderArt.Ring, new Color(0.8f, 0.3f, 1f, 0.9f), 0.5f, 95);
        ring.transform.position = position;
        effects.Add(new TimedEffect { Go = ring.gameObject, Sprite = ring, Duration = 0.3f, EndTime = Time.unscaledTime + 0.3f, BaseColor = ring.color, Grow = true });
    }

    // 웨이브 등장 예고 표식 (전투 시간 기준으로 사라지게 호출 쪽이 관리)
    public GameObject SpawnWaveWarning(Vector2 position)
    {
        SpriteRenderer mark = PlaceholderArt.CreateShape("WaveWarning", null, PlaceholderArt.Ring, new Color(1f, 0.2f, 0.2f, 0.6f), 0.7f, 60);
        mark.transform.position = position;
        return mark.gameObject;
    }

    // 문 잠금 이펙트 등장/소멸 (09 §3 룸과 보상)
    public void SpawnLockAppear(Bounds bounds)
    {
        SpriteRenderer flash = PlaceholderArt.CreateShape("LockAppear", null, PlaceholderArt.Square, new Color(1f, 0.85f, 0.85f, 0.9f), 1f, 155);
        flash.transform.position = bounds.center;
        flash.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
        effects.Add(new TimedEffect { Go = flash.gameObject, Sprite = flash, Duration = 0.2f, EndTime = Time.unscaledTime + 0.2f, BaseColor = flash.color });
    }

    public void SpawnLockRelease(Bounds bounds)
    {
        SpriteRenderer fade = PlaceholderArt.CreateShape("LockRelease", null, PlaceholderArt.Square, RoomDoor.LockColor, 1f, 150);
        fade.transform.position = bounds.center;
        fade.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
        effects.Add(new TimedEffect { Go = fade.gameObject, Sprite = fade, Duration = 0.3f, EndTime = Time.unscaledTime + 0.3f, BaseColor = fade.color, Grow = true, GrowRate = 1.5f });
        Burst(bounds.center, RoomDoor.LockColor, 5, 0.12f, 1.5f);
    }

    // 회피 잔상
    public void SpawnGhost(Sprite sprite, Vector2 position, float diameter, Color color)
    {
        SpriteRenderer ghost = PlaceholderArt.CreateShape("Ghost", null, sprite, color, diameter, 98);
        ghost.transform.position = position;
        effects.Add(new TimedEffect { Go = ghost.gameObject, Sprite = ghost, Duration = 0.18f, EndTime = Time.unscaledTime + 0.18f, BaseColor = color });
    }

    // 엔딩 상자 개봉 (09 §7, EndingTriggered 순간)
    public void SpawnChestOpen(Vector2 position)
    {
        SpriteRenderer beam = PlaceholderArt.CreateShape("ChestBeam", null, PlaceholderArt.Square, new Color(1f, 0.95f, 0.6f, 0.8f), 1f, 236);
        beam.transform.position = position + new Vector2(0f, 1.5f);
        beam.transform.localScale = new Vector3(0.6f, 3f, 1f);
        effects.Add(new TimedEffect { Go = beam.gameObject, Sprite = beam, Duration = 0.6f, EndTime = Time.unscaledTime + 0.6f, BaseColor = beam.color });
        SpriteRenderer ring = PlaceholderArt.CreateShape("ChestRing", null, PlaceholderArt.Ring, new Color(1f, 0.9f, 0.4f, 1f), 0.8f, 237);
        ring.transform.position = position;
        effects.Add(new TimedEffect { Go = ring.gameObject, Sprite = ring, Duration = 0.5f, EndTime = Time.unscaledTime + 0.5f, BaseColor = ring.color, Grow = true, GrowRate = 5f });
        Burst(position, new Color(1f, 0.85f, 0.3f), 14, 0.16f, 3.5f);
    }

    // 아티팩트 획득: 플레이어 위치에 등급 색 파동
    private void OnArtifactAcquired(ArtifactDefinition def)
    {
        PlayerController player = GameServices.Player;
        if (player == null || def == null) return;
        Color color = ArtifactGradeUtility.Color(def.Grade);
        SpriteRenderer ring = PlaceholderArt.CreateShape("Acquire", null, PlaceholderArt.Ring, color, 0.5f, 238);
        ring.transform.position = player.Position;
        effects.Add(new TimedEffect { Go = ring.gameObject, Sprite = ring, Duration = 0.35f, EndTime = Time.unscaledTime + 0.35f, BaseColor = color, Grow = true, GrowRate = 6f });
        Burst(player.Position, color, 8, 0.12f, 2.2f);
    }

    private void OnJustDodge()
    {
        PlayerController player = GameServices.Player;
        if (player == null) return;
        SpriteRenderer ring = PlaceholderArt.CreateShape("JustDodge", null, PlaceholderArt.Ring, new Color(1f, 1f, 1f, 0.9f), 0.6f, 240);
        ring.transform.position = player.Position;
        effects.Add(new TimedEffect { Go = ring.gameObject, Sprite = ring, Duration = 0.25f, EndTime = Time.unscaledTime + 0.25f, BaseColor = ring.color, Grow = true });
    }

    // 세트 활성 안내: UI가 닫힌 뒤 화면 중앙 (09 §7)
    private void OnSetTierChanged(ArtifactSetDefinition set, SetTier tier)
    {
        float duration = GameBalance.Current.Feedback.SetActivationToastDuration;
        if (tier == SetTier.Inactive)
        {
            // 세트 해제도 같은 자리에 회색으로 (09 §3 시스템: 세트 활성과 해제)
            GameServices.UI?.QueueSetToast($"<color=#9A9AA5>{set.DisplayName}  해제</color>", duration);
            return;
        }
        GameServices.UI?.QueueSetToast($"{set.DisplayName}  {(tier == SetTier.Tier2 ? "Tier2" : "Tier1")}", duration);
    }

    private void Burst(Vector2 position, Color color, int count, float size, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer dot = PlaceholderArt.CreateShape("Particle", null, PlaceholderArt.Circle, color, size, 230);
            dot.transform.position = position;
            Vector2 dir = Geometry2D.FromAngle(i * 360f / count + Random.Range(-15f, 15f));
            effects.Add(new TimedEffect { Go = dot.gameObject, Sprite = dot, Duration = 0.3f, EndTime = Time.unscaledTime + 0.3f, Velocity = dir * speed, BaseColor = color });
        }
    }

    private void AddTimed(GameObject go, float duration, SpriteRenderer sprite)
        => effects.Add(new TimedEffect { Go = go, Sprite = sprite, Duration = duration, EndTime = Time.unscaledTime + duration, BaseColor = sprite != null ? sprite.color : Color.white });

    // 이펙트는 히트스톱 중에도 계속 재생 (09 §6)
    private void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            TimedEffect e = effects[i];
            if (e.Go == null) { effects.RemoveAt(i); continue; }
            float remaining = e.EndTime - now;
            if (remaining <= 0f)
            {
                Destroy(e.Go);
                effects.RemoveAt(i);
                continue;
            }
            float t = remaining / e.Duration;
            e.Go.transform.position += e.Velocity * dt;
            Color c = e.BaseColor;
            c.a *= t;
            if (e.Sprite != null)
            {
                e.Sprite.color = c;
                if (e.Grow) e.Go.transform.localScale *= 1f + dt * e.GrowRate;
            }
            if (e.Text != null) e.Text.color = c;
            if (e.Line != null) { e.Line.startColor = c; e.Line.endColor = c; }
        }
    }

    public void ClearAll()
    {
        foreach (TimedEffect e in effects) if (e.Go != null) Destroy(e.Go);
        effects.Clear();
        damageNumbers.Clear();
    }
}
