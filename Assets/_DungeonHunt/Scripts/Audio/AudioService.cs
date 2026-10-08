using System;
using System.Collections.Generic;
using UnityEngine;

// 임시 사운드. 사운드 방향(00 §9.1)이 미정이라 런타임에 합성한 짧은 효과음과 단순 BGM 루프만 씀.
// 게임플레이 이벤트를 구독해서 출력만 함 (논리에 영향 없음, 09 §4).
// 실제 사운드가 들어오면 Clips 딕셔너리 대신 AudioClip 에셋을 연결하면 됨 (같은 키 이름 사용 권장).
// 음량: 전체 = AudioListener.volume, BGM/효과음 = 설정 화면 값 (08 §5.4)
public class AudioService : MonoBehaviour
{
    private const int SampleRate = 22050;
    private const int SfxSourceCount = 10;
    private const float SameClipMinInterval = 0.035f;

    [Tooltip("키 이름으로 덮어쓸 실제 효과음 (비우면 합성음)")]
    [SerializeField] private List<NamedClip> overrides = new List<NamedClip>();
    [SerializeField] private AudioClip bgmOverride;

    [Serializable]
    public class NamedClip
    {
        public string Key;
        public AudioClip Clip;
    }

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private AudioSource bgmSource;
    private int nextSource;
    private float sfxVolume = 1f;
    private float bgmVolume = 0.8f;
    private int lastHP = -1;

    private void Awake()
    {
        GameServices.Audio = this;
        for (int i = 0; i < SfxSourceCount; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            sfxSources.Add(source);
        }
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        foreach (NamedClip named in overrides)
            if (named != null && named.Clip != null && !string.IsNullOrEmpty(named.Key)) clips[named.Key] = named.Clip;
    }

    // 합성은 시작할 때 한 번에 (첫 재생 때 끊김 방지)
    private static readonly string[] Keys =
    {
        "shot_rifle", "shot_pistol", "shot_shotgun", "hit", "hit_crit", "kill", "telegraph", "player_hit", "player_die",
        "dodge", "just_dodge", "reload", "lock", "clear", "coin", "heal", "artifact", "secret", "boss_phase", "ending", "ui_click", "bgm",
    };

    private void Start()
    {
        foreach (string key in Keys) Get(key);
    }

    private void OnEnable()
    {
        GameEvents.WeaponFired += OnWeaponFired;
        GameEvents.ProjectileHit += OnHit;
        GameEvents.ShotgunConeHit += OnHit;
        GameEvents.MonsterDied += OnMonsterDied;
        GameEvents.MonsterAttackTelegraphed += OnTelegraph;
        GameEvents.PlayerHit += OnPlayerHit;
        GameEvents.PlayerHPChanged += OnHPChanged;
        GameEvents.PlayerDied += OnPlayerDied;
        GameEvents.DodgeStart += OnDodge;
        GameEvents.JustDodgeTriggered += OnJustDodge;
        GameEvents.ReloadComplete += OnReloadComplete;
        GameEvents.CombatRoomLocked += OnRoomLocked;
        GameEvents.FloorGateRoomLocked += OnRoomLocked;
        GameEvents.CombatRoomCleared += OnRoomCleared;
        GameEvents.FloorGateRoomCleared += OnRoomCleared;
        GameEvents.RunCurrencyGained += OnCurrency;
        GameEvents.ArtifactAcquired += OnArtifact;
        GameEvents.SecretRoomRevealed += OnSecret;
        GameEvents.BossPhaseChanged += OnBossPhase;
        GameEvents.BossDefeated += OnBossDefeated;
        GameEvents.EndingTriggered += OnEnding;
        GameEvents.RunStarted += OnRunStarted;
        GameEvents.RunOver += StopBgm;
        GameEvents.RunCleared += StopBgm;
    }

    private void OnDisable()
    {
        GameEvents.WeaponFired -= OnWeaponFired;
        GameEvents.ProjectileHit -= OnHit;
        GameEvents.ShotgunConeHit -= OnHit;
        GameEvents.MonsterDied -= OnMonsterDied;
        GameEvents.MonsterAttackTelegraphed -= OnTelegraph;
        GameEvents.PlayerHit -= OnPlayerHit;
        GameEvents.PlayerHPChanged -= OnHPChanged;
        GameEvents.PlayerDied -= OnPlayerDied;
        GameEvents.DodgeStart -= OnDodge;
        GameEvents.JustDodgeTriggered -= OnJustDodge;
        GameEvents.ReloadComplete -= OnReloadComplete;
        GameEvents.CombatRoomLocked -= OnRoomLocked;
        GameEvents.FloorGateRoomLocked -= OnRoomLocked;
        GameEvents.CombatRoomCleared -= OnRoomCleared;
        GameEvents.FloorGateRoomCleared -= OnRoomCleared;
        GameEvents.RunCurrencyGained -= OnCurrency;
        GameEvents.ArtifactAcquired -= OnArtifact;
        GameEvents.SecretRoomRevealed -= OnSecret;
        GameEvents.BossPhaseChanged -= OnBossPhase;
        GameEvents.BossDefeated -= OnBossDefeated;
        GameEvents.EndingTriggered -= OnEnding;
        GameEvents.RunStarted -= OnRunStarted;
        GameEvents.RunOver -= StopBgm;
        GameEvents.RunCleared -= StopBgm;
    }

    // 설정 화면 값 (0~100)
    public void SetVolumes(float bgm, float sfx)
    {
        bgmVolume = Mathf.Clamp01(bgm / 100f);
        sfxVolume = Mathf.Clamp01(sfx / 100f);
        if (bgmSource != null) bgmSource.volume = bgmVolume * 0.35f;
    }

    public void Play(string key, float volume = 1f, float pitch = 1f)
    {
        if (sfxVolume <= 0f) return;
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(key, out float last) && now - last < SameClipMinInterval) return;
        lastPlayed[key] = now;
        AudioClip clip = Get(key);
        if (clip == null) return;
        AudioSource source = sfxSources[nextSource];
        nextSource = (nextSource + 1) % sfxSources.Count;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * sfxVolume);
    }

    // UI 버튼 등 외부에서 부르는 짧은 클릭음
    public void PlayUIClick() => Play("ui_click", 0.5f);

    // ---------- 이벤트 → 소리 ----------

    private void OnWeaponFired(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Rifle: Play("shot_rifle", 0.45f, UnityEngine.Random.Range(0.95f, 1.05f)); break;
            case WeaponType.Pistol: Play("shot_pistol", 0.6f, UnityEngine.Random.Range(0.97f, 1.03f)); break;
            default: Play("shot_shotgun", 0.75f); break;
        }
    }

    private void OnHit(DamageInfo info)
    {
        if (info.Source != DamageSource.Player) return;
        if (info.IsCritical) Play("hit_crit", 0.6f);
        else Play("hit", 0.4f, UnityEngine.Random.Range(0.9f, 1.1f));
    }

    private void OnMonsterDied(Monster monster) => Play("kill", 0.6f, UnityEngine.Random.Range(0.92f, 1.08f));
    private void OnTelegraph(Monster monster) => Play("telegraph", 0.25f);
    private void OnPlayerHit(int damage) => Play("player_hit", 0.9f);
    private void OnPlayerDied() => Play("player_die", 1f);
    private void OnDodge() => Play("dodge", 0.4f);
    private void OnJustDodge() => Play("just_dodge", 0.7f);
    private void OnReloadComplete(WeaponType type) => Play("reload", 0.45f);
    private void OnRoomLocked(RoomRuntime room) => Play("lock", 0.7f);
    private void OnRoomCleared(RoomRuntime room) => Play("clear", 0.7f);
    private void OnCurrency(int amount) => Play("coin", 0.45f, UnityEngine.Random.Range(0.95f, 1.1f));
    private void OnArtifact(ArtifactDefinition def) => Play("artifact", 0.7f);
    private void OnSecret(RoomRuntime room) => Play("secret", 0.8f);
    private void OnBossPhase() => Play("boss_phase", 0.9f);
    private void OnBossDefeated() => Play("clear", 1f, 0.8f);
    private void OnEnding() => Play("ending", 0.9f);

    private void OnHPChanged(int current, int max)
    {
        if (lastHP >= 0 && current > lastHP) Play("heal", 0.6f);
        lastHP = current;
    }

    private void OnRunStarted()
    {
        lastHP = -1;
        bgmSource.clip = bgmOverride != null ? bgmOverride : Get("bgm");
        bgmSource.volume = bgmVolume * 0.35f;
        if (bgmSource.clip != null) bgmSource.Play();
    }

    private void StopBgm()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    // ---------- 합성 ----------

    private AudioClip Get(string key)
    {
        if (clips.TryGetValue(key, out AudioClip clip)) return clip;
        clip = Synthesize(key);
        clips[key] = clip;
        return clip;
    }

    private static AudioClip Synthesize(string key)
    {
        var rng = new System.Random(key.GetHashCode());
        float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        switch (key)
        {
            case "shot_rifle": return Make(key, 0.07f, (t, d) => (Noise() * 0.7f + Square(t, 900f - 4000f * t) * 0.3f) * Env(t, d, 40f));
            case "shot_pistol": return Make(key, 0.12f, (t, d) => (Noise() * 0.6f + Square(t, 520f - 1800f * t) * 0.4f) * Env(t, d, 28f));
            case "shot_shotgun": return Make(key, 0.25f, LowNoise(rng, 0.35f, 14f));
            case "hit": return Make(key, 0.05f, (t, d) => Square(t, 1400f) * Env(t, d, 60f) * 0.5f);
            case "hit_crit": return Make(key, 0.16f, (t, d) => (Sine(t, 1760f) + Sine(t, 2640f) * 0.5f) * Env(t, d, 18f) * 0.6f);
            case "kill": return Make(key, 0.18f, (t, d) => (Square(t, 300f - 900f * t) * 0.5f + Noise() * 0.3f) * Env(t, d, 16f));
            case "telegraph": return Make(key, 0.08f, (t, d) => Sine(t, 660f) * Env(t, d, 30f) * 0.6f);
            case "player_hit": return Make(key, 0.22f, (t, d) => (Square(t, 160f - 200f * t) * 0.6f + Noise() * 0.4f) * Env(t, d, 12f));
            case "player_die": return Make(key, 0.8f, (t, d) => Square(t, 220f - 180f * t) * Env(t, d, 3f) * 0.7f);
            case "dodge": return Make(key, 0.14f, LowNoise(rng, 0.5f, 20f));
            case "just_dodge": return Make(key, 0.3f, (t, d) => (Sine(t, 1320f) + Sine(t, 1980f) * 0.6f) * Env(t, d, 9f) * 0.6f);
            case "reload": return Make(key, 0.12f, (t, d) => (t < 0.03f || (t > 0.07f && t < 0.1f) ? Noise() * 0.6f : 0f));
            case "lock": return Make(key, 0.35f, (t, d) => Square(t, 110f) * Env(t, d, 6f) * 0.5f);
            case "clear": return Make(key, 0.5f, (t, d) => Arp(t, new[] { 523f, 659f, 784f, 1047f }, 0.09f) * Env(t, d, 4f) * 0.5f);
            case "coin": return Make(key, 0.1f, (t, d) => Square(t, t < 0.04f ? 988f : 1319f) * Env(t, d, 20f) * 0.35f);
            case "heal": return Make(key, 0.3f, (t, d) => Arp(t, new[] { 440f, 554f, 659f }, 0.07f) * Env(t, d, 7f) * 0.5f);
            case "artifact": return Make(key, 0.6f, (t, d) => Arp(t, new[] { 392f, 523f, 659f, 784f, 1047f }, 0.08f) * Env(t, d, 3.5f) * 0.5f);
            case "secret": return Make(key, 0.7f, (t, d) => Arp(t, new[] { 523f, 622f, 740f, 880f }, 0.12f) * Env(t, d, 3f) * 0.5f);
            case "boss_phase": return Make(key, 0.7f, (t, d) => (Square(t, 82f) * 0.6f + Noise() * 0.2f) * Env(t, d, 3f));
            case "ending": return Make(key, 1.5f, (t, d) => (Sine(t, 523f) + Sine(t, 659f) + Sine(t, 784f)) / 3f * Env(t, d, 1.5f));
            case "ui_click": return Make(key, 0.03f, (t, d) => Square(t, 1800f) * Env(t, d, 80f) * 0.4f);
            case "bgm": return MakeBgm();
            default: return null;
        }
    }

    private static Func<float, float, float> LowNoise(System.Random rng, float gain, float decay)
    {
        float y = 0f;
        return (t, d) =>
        {
            y += ((float)(rng.NextDouble() * 2.0 - 1.0) - y) * 0.25f; // 간단한 저역 통과
            return y * gain * 2.2f * Env(t, d, decay);
        };
    }

    private static float Sine(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t);
    private static float Square(float t, float f) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * Mathf.Max(20f, f) * t));
    private static float Env(float t, float duration, float decay) => Mathf.Exp(-decay * t) * Mathf.Clamp01((duration - t) / 0.01f) * Mathf.Clamp01(t / 0.002f);
    private static float Arp(float t, float[] notes, float step) => Sine(t, notes[Mathf.Min(notes.Length - 1, (int)(t / step))]);

    private static AudioClip Make(string name, float duration, Func<float, float, float> sample)
    {
        int count = Mathf.CeilToInt(duration * SampleRate);
        var data = new float[count];
        for (int i = 0; i < count; i++)
            data[i] = Mathf.Clamp(sample(i / (float)SampleRate, duration), -1f, 1f);
        AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 16초짜리 어두운 패드 루프 (Am - F - C - G)
    private static AudioClip MakeBgm()
    {
        float[][] chords =
        {
            new[] { 110f, 220f, 261.6f, 329.6f },
            new[] { 87.3f, 174.6f, 220f, 261.6f },
            new[] { 130.8f, 196f, 261.6f, 329.6f },
            new[] { 98f, 196f, 246.9f, 293.7f },
        };
        const float chordLength = 4f;
        float duration = chordLength * chords.Length;
        return Make("bgm", duration, (t, d) =>
        {
            int index = Mathf.Min(chords.Length - 1, (int)(t / chordLength));
            float local = t - index * chordLength;
            float fade = Mathf.Clamp01(local / 0.6f) * Mathf.Clamp01((chordLength - local) / 0.6f);
            float v = 0f;
            foreach (float f in chords[index]) v += Sine(t, f) * 0.22f + Sine(t, f * 1.003f) * 0.12f;
            float pulse = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            return v * fade * pulse * 0.5f;
        });
    }
}
