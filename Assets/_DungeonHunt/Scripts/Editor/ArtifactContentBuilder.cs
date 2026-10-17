using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 아티팩트 콘텐츠 (Docs/ArtifactContent.md 표) → 에셋 생성/갱신.
// 메뉴: DungeonHunt/Content/Build Artifact Content
// 결과: Assets/_DungeonHunt/Content/Artifacts/ (세트 6종, 아티팩트 28종, 카탈로그) + GameConfig.Artifacts 연결
// 같은 경로 에셋이 있으면 값만 덮어써서 GUID와 참조가 유지됨. 표가 바뀌면 이 파일의 데이터를 고치고 다시 실행.
public static class ArtifactContentBuilder
{
    public const string Root = "Assets/_DungeonHunt/Content/Artifacts";
    public const string CatalogPath = Root + "/ArtifactCatalog.asset";

    private const ArtifactGrade C = ArtifactGrade.Common;
    private const ArtifactGrade R = ArtifactGrade.Rare;
    private const ArtifactGrade L = ArtifactGrade.Legendary;
    private const ArtifactUnlockSource Init = ArtifactUnlockSource.Initial;
    private const ArtifactUnlockSource Cur = ArtifactUnlockSource.Currency;
    private const ArtifactUnlockSource Prog = ArtifactUnlockSource.Progress;

    private static StatModifier P(StatId stat, float value) => new StatModifier(stat, ModifierKind.Percent, value);
    private static StatModifier F(StatId stat, float value) => new StatModifier(stat, ModifierKind.Flat, value);
    private static ArtifactEffect E(ArtifactEffectKind kind, float value, float duration = 0f) => new ArtifactEffect(kind, value, duration);

    private class SetData
    {
        public string Id, Name;
        public Color Color;
        public bool Specialization;
        public WeaponType Weapon;
        public StatModifier[] T1, T2;
        public ArtifactEffect[] T1Effects, T2Effects;
    }

    private class ArtifactData
    {
        public string Id, Name, Set;
        public ArtifactGrade Grade;
        public ArtifactUnlockSource Unlock;
        public StatModifier[] Mods;
        public ArtifactEffect[] Effects;
    }

    // ---------------- 세트 (Tier2는 Tier1에 더해짐) ----------------
    private static readonly SetData[] Sets =
    {
        new SetData { Id = "SET_PISTOL", Name = "권총", Color = new Color(1f, 0.8f, 0.3f), Specialization = true, Weapon = WeaponType.Pistol,
            T1 = new[] { P(StatId.PistolFireInterval, -0.25f) }, T1Effects = new[] { E(ArtifactEffectKind.PistolRicochet, 0.5f) },
            T2 = new[] { P(StatId.PistolDamage, 0.5f) }, T2Effects = new[] { E(ArtifactEffectKind.PistolRicochet, 0.5f) } },          // 도탄 50% + 50% = 100%
        new SetData { Id = "SET_DODGE", Name = "회피 강화", Color = new Color(0.4f, 0.85f, 1f),
            T1Effects = new[] { E(ArtifactEffectKind.DodgeDamageBuff, 0.2f, 2f) },
            T2Effects = new[] { E(ArtifactEffectKind.JustDodgeInvincible, 0f, 1.5f) } },
        new SetData { Id = "SET_RIFLE", Name = "라이플", Color = new Color(1f, 0.95f, 0.5f), Specialization = true, Weapon = WeaponType.Rifle,
            T1Effects = new[] { E(ArtifactEffectKind.RifleStackDamage, 0.05f) },
            T2Effects = new[] { E(ArtifactEffectKind.RifleStackDamage, 0.02f), E(ArtifactEffectKind.RifleFiringMoveSpeed, 0.3f) } }, // 스택당 5% → 7%
        new SetData { Id = "SET_SHOTGUN", Name = "샷건", Color = new Color(1f, 0.55f, 0.3f), Specialization = true, Weapon = WeaponType.Shotgun,
            T1 = new[] { P(StatId.ShotgunRange, 1f), P(StatId.ShotgunFireInterval, -0.1f) },
            T2 = new[] { P(StatId.ShotgunRange, 1f), P(StatId.ShotgunConeAngle, 0.3f), P(StatId.ShotgunDamage, 0.3f) } },           // 반지름 +100% + 100% = +200%
        new SetData { Id = "SET_SWAP", Name = "무기 스왑 유도", Color = new Color(0.6f, 1f, 0.6f),
            T1Effects = new[] { E(ArtifactEffectKind.SwapEnhancedRounds, 2f) },
            T2Effects = new[] { E(ArtifactEffectKind.SwapRefill, 0.5f) } },
        new SetData { Id = "SET_ALLIN", Name = "전투력 올인", Color = new Color(1f, 0.35f, 0.35f),
            T1 = new[] { P(StatId.MaxHP, -0.5f), P(StatId.WeaponDamageAll, 0.7f) },
            T2 = new[] { P(StatId.MaxHP, -0.3f), P(StatId.MoveSpeed, 0.5f), P(StatId.DodgeChargeTime, -0.5f) } },                  // 최대 HP -50% → -80%
    };

    // ---------------- 아티팩트 (이름은 더미) ----------------
    private static readonly ArtifactData[] Artifacts =
    {
        A("PISTOL_01", "가벼운 권총집", "SET_PISTOL", C, Init, P(StatId.MoveSpeed, 0.1f)),
        A("PISTOL_02", "확장 권총 탄창", "SET_PISTOL", C, Init, F(StatId.PistolMagazine, 2)),
        A("PISTOL_03", "정밀 총열", "SET_PISTOL", R, Cur, P(StatId.PistolDamage, 0.15f), F(StatId.PistolMagazine, 2)),
        A("PISTOL_04", "날쌘 발걸음", "SET_PISTOL", L, Prog, F(StatId.DodgeMaxCharge, 1)),

        A("DODGE_01", "가죽 장화", "SET_DODGE", C, Init, P(StatId.DodgeChargeTime, -0.15f)),
        A("DODGE_02", "예민한 감각", "SET_DODGE", C, Init, P(StatId.JustDodgeWindow, 0.3f)),
        A("DODGE_03", "반사 장갑", "SET_DODGE", C, Init, F(StatId.JustDodgeAmmoRefund, 0.2f), P(StatId.JustDodgeWindow, 0.2f)),
        A("DODGE_04", "그림자 망토", "SET_DODGE", L, Cur, P(StatId.DodgeChargeTime, -0.2f), P(StatId.JustDodgeWindow, 0.2f)),

        A("RIFLE_01", "강화 탄두", "SET_RIFLE", C, Init, P(StatId.RifleDamage, 0.1f)),
        A("RIFLE_02", "확장 라이플 탄창", "SET_RIFLE", C, Init, F(StatId.RifleMagazine, 6)),
        A("RIFLE_03", "드럼 탄창", "SET_RIFLE", R, Prog, F(StatId.RifleMagazine, 10)),
        A("RIFLE_04", "빠른 장전 손잡이", "SET_RIFLE", R, Cur, P(StatId.RifleReloadTime, -0.2f)),

        A("SHOTGUN_01", "산탄 주머니", "SET_SHOTGUN", C, Init, F(StatId.ShotgunMagazine, 2)),
        A("SHOTGUN_02", "무거운 산탄", "SET_SHOTGUN", R, Init, P(StatId.ShotgunDamage, 0.15f)),
        A("SHOTGUN_03", "넓은 총구", "SET_SHOTGUN", R, Init, P(StatId.ShotgunRange, 0.1f), P(StatId.ShotgunReloadTime, -0.3f)),
        A("SHOTGUN_04", "사냥꾼의 산탄총", "SET_SHOTGUN", L, Prog, P(StatId.ShotgunDamage, 0.15f), P(StatId.ShotgunRange, 0.2f), P(StatId.ShotgunConeAngle, 0.1f), P(StatId.ShotgunReloadTime, -0.15f)),

        A("SWAP_01", "교대 장갑", "SET_SWAP", C, Init, new[] { P(StatId.WeaponDamageAll, 0.03f) }, E(ArtifactEffectKind.SwapDamageWindow, 1.08f, 1.5f)),
        A("SWAP_02", "만능 공구", "SET_SWAP", C, Init, P(StatId.WeaponDamageAll, 0.09f)),
        A("SWAP_03", "가벼운 탄띠", "SET_SWAP", C, Init, new[] { P(StatId.MagazineSizeAll, -0.25f), P(StatId.WeaponDamageAll, 0.02f) }, E(ArtifactEffectKind.SwapDamageWindow, 1.08f, 1.5f)),
        A("SWAP_04", "곡예사의 손목", "SET_SWAP", R, Cur, new[] { P(StatId.WeaponDamageAll, 0.06f) }, E(ArtifactEffectKind.SwapDamageWindow, 1.12f, 1.5f)),

        A("ALLIN_01", "도박사의 눈", "SET_ALLIN", C, Cur, F(StatId.CritChance, 0.2f)),
        A("ALLIN_02", "날 선 탄", "SET_ALLIN", R, Cur, F(StatId.CritDamage, 0.5f)),
        A("ALLIN_03", "처형인의 표식", "SET_ALLIN", L, Cur, F(StatId.CritChance, 0.2f), F(StatId.CritDamage, 1f)),
        A("ALLIN_04", "광전사의 인장", "SET_ALLIN", L, Cur, P(StatId.WeaponDamageAll, 0.3f)),

        A("UNIV_01", "튼튼한 갑옷", null, R, Init, F(StatId.MaxHP, 10), P(StatId.HitInvincibleDuration, 0.2f)),
        A("UNIV_02", "전쟁의 왕관", null, L, Cur, P(StatId.WeaponDamageAll, 0.25f), F(StatId.CritChance, 0.3f)),
        A("UNIV_03", "여행자의 부츠", null, C, Init, P(StatId.MoveSpeed, 0.1f), P(StatId.DodgeChargeTime, -0.1f)),
        A("UNIV_04", "자동 장전기", null, R, Prog, P(StatId.FireIntervalAll, -0.15f), P(StatId.ReloadTimeAll, -0.2f)),
    };

    // 진행 해금 조건 연결 (표 순서대로 2층 도달 / 3층 도달 / 첫 클리어 / 두 번째 클리어)
    private static readonly string[] ProgressOrder = { "PISTOL_04", "RIFLE_03", "SHOTGUN_04", "UNIV_04" };

    private static ArtifactData A(string id, string name, string set, ArtifactGrade grade, ArtifactUnlockSource unlock, params StatModifier[] mods)
        => new ArtifactData { Id = id, Name = name, Set = set, Grade = grade, Unlock = unlock, Mods = mods, Effects = new ArtifactEffect[0] };

    private static ArtifactData A(string id, string name, string set, ArtifactGrade grade, ArtifactUnlockSource unlock, StatModifier[] mods, params ArtifactEffect[] effects)
        => new ArtifactData { Id = id, Name = name, Set = set, Grade = grade, Unlock = unlock, Mods = mods, Effects = effects };

    [MenuItem("DungeonHunt/Content/Build Artifact Content", priority = 10)]
    public static void BuildMenu()
    {
        ArtifactCatalog catalog = Build();
        Selection.activeObject = catalog;
        EditorUtility.DisplayDialog("아티팩트 콘텐츠", $"세트 {Sets.Length}종, 아티팩트 {Artifacts.Length}종 생성/갱신\n{Root}\nGameConfig.Artifacts에 연결됨", "확인");
    }

    public static ArtifactCatalog Build()
    {
        PlaceholderContentGenerator.EnsureFolder(Root + "/Sets");

        var sets = new Dictionary<string, ArtifactSetDefinition>();
        int order = 0;
        foreach (SetData data in Sets)
        {
            ArtifactSetDefinition set = CreateOrLoad<ArtifactSetDefinition>($"{Root}/Sets/{data.Id}.asset");
            set.Id = data.Id;
            set.DisplayName = data.Name;
            set.Color = data.Color;
            set.IsWeaponSpecialization = data.Specialization;
            set.SpecializedWeapon = data.Weapon;
            set.Tier1Modifiers = new List<StatModifier>(data.T1 ?? new StatModifier[0]);
            set.Tier2Modifiers = new List<StatModifier>(data.T2 ?? new StatModifier[0]);
            set.Tier1Effects = new List<ArtifactEffect>(data.T1Effects ?? new ArtifactEffect[0]);
            set.Tier2Effects = new List<ArtifactEffect>(data.T2Effects ?? new ArtifactEffect[0]);
            set.CodexGroupOrder = order++;
            EditorUtility.SetDirty(set);
            sets[data.Id] = set;
        }

        var artifacts = new List<ArtifactDefinition>();
        order = 0;
        foreach (ArtifactData data in Artifacts)
        {
            ArtifactDefinition def = CreateOrLoad<ArtifactDefinition>($"{Root}/{data.Id}.asset");
            def.Id = data.Id;
            def.DisplayName = data.Name;
            def.Grade = data.Grade;
            def.Set = data.Set != null ? sets[data.Set] : null;
            def.UnlockSource = data.Unlock;
            def.Modifiers = new List<StatModifier>(data.Mods);
            def.Effects = new List<ArtifactEffect>(data.Effects);
            def.Description = "";
            def.CodexOrder = order++;
            def.IsTestPlaceholder = false;
            EditorUtility.SetDirty(def);
            artifacts.Add(def);
        }

        ArtifactCatalog catalog = CreateOrLoad<ArtifactCatalog>(CatalogPath);
        catalog.Sets = new List<ArtifactSetDefinition>(sets.Values);
        catalog.Artifacts = artifacts;
        catalog.ReachFloor2 = artifacts.Find(a => a.Id == ProgressOrder[0]);
        catalog.ReachFloor3 = artifacts.Find(a => a.Id == ProgressOrder[1]);
        catalog.FirstClear = artifacts.Find(a => a.Id == ProgressOrder[2]);
        catalog.SecondClear = artifacts.Find(a => a.Id == ProgressOrder[3]);
        EditorUtility.SetDirty(catalog);

        GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_DungeonHunt/Resources/GameConfig.asset");
        if (config != null)
        {
            config.Artifacts = catalog;
            EditorUtility.SetDirty(config);
        }
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }
}
