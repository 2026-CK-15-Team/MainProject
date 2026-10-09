using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 포스트프로세싱 한 번에 설정. 메뉴: DungeonHunt/Setup/Setup Post Processing (콘텐츠 생성기도 호출함)
// 1. Volume 프로필 생성 (Bloom, Vignette, Chromatic Aberration, DungeonLensEffects). 이미 있으면 빠진 효과만 추가하고 조정한 값은 안 건드림
// 2. 풀스크린 머티리얼(DungeonHunt/LensEffects) 생성
// 3. 현재 URP 렌더러(2D Renderer)에 Full Screen Pass Renderer Feature 추가 (After Rendering Post Processing)
// 4. GameConfig.PostProcessProfile 연결
public static class PostProcessSetup
{
    public const string Folder = "Assets/_DungeonHunt/Settings/PostProcess";
    public const string ProfilePath = Folder + "/GamePostProcessProfile.asset";
    public const string MaterialPath = Folder + "/DungeonLensEffects.mat";
    private const string ShaderName = "DungeonHunt/LensEffects";
    private const string FeatureName = "DungeonLensEffects";
    private const string FallbackRendererPath = "Assets/_DungeonHunt/Settings/Renderer2D.asset";

    [MenuItem("DungeonHunt/Setup/Setup Post Processing", priority = 2)]
    public static void SetupMenu()
    {
        VolumeProfile profile = Setup();
        if (profile != null)
        {
            Selection.activeObject = profile;
            EditorUtility.DisplayDialog("포스트프로세싱", $"설정 완료.\n수치 조정: {ProfilePath}\n(Play 중에 바꿔도 바로 반영되고 에셋에 저장됨)", "확인");
        }
    }

    public static VolumeProfile Setup()
    {
        PlaceholderContentGenerator.EnsureFolder(Folder);

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        AddDefaults(profile);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[포스트프로세싱] 셰이더 {ShaderName} 없음 (Assets/_DungeonHunt/Shaders/DungeonLensEffects.shader 컴파일 오류 확인)");
            return profile;
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = FeatureName };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        AddFullScreenFeature(material);

        GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_DungeonHunt/Resources/GameConfig.asset");
        if (config != null && config.PostProcessProfile == null)
        {
            config.PostProcessProfile = profile;
            EditorUtility.SetDirty(config);
        }
        AssetDatabase.SaveAssets();
        return profile;
    }

    // 없는 효과만 기본값으로 추가 (이미 있는 효과의 조정값은 유지)
    private static void AddDefaults(VolumeProfile profile)
    {
        if (!profile.Has<Bloom>())
        {
            Bloom bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
        }
        if (!profile.Has<Vignette>())
        {
            Vignette vignette = Add<Vignette>(profile);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
        }
        if (!profile.Has<ChromaticAberration>())
        {
            ChromaticAberration ca = Add<ChromaticAberration>(profile);
            ca.intensity.Override(0.12f);
        }
        if (!profile.Has<DungeonLensEffects>())
        {
            DungeonLensEffects fx = Add<DungeonLensEffects>(profile);
            fx.fogIntensity.Override(0.25f);
            fx.fogColor.Override(fx.fogColor.value);
            fx.fogScale.Override(fx.fogScale.value);
            fx.fogSpeed.Override(fx.fogSpeed.value);
            fx.fogEdgeBias.Override(fx.fogEdgeBias.value);
            fx.fringeIntensity.Override(0.35f);
            fx.flareIntensity.Override(0.35f);
        }
        EditorUtility.SetDirty(profile);
    }

    // 인스펙터의 Add Override와 같은 방식: 프로필 하위 에셋으로 저장
    private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static void AddFullScreenFeature(Material material)
    {
        ScriptableRendererData renderer = FindRendererData();
        if (renderer == null)
        {
            Debug.LogError("[포스트프로세싱] URP 렌더러를 못 찾음. Project Settings > Graphics/Quality에 UniversalRP 에셋이 지정돼 있는지 확인. " +
                           "수동: Renderer2D 에셋 > Add Renderer Feature > Full Screen Pass Renderer Feature, Pass Material = " + MaterialPath);
            return;
        }

        foreach (ScriptableRendererFeature existing in renderer.rendererFeatures)
        {
            if (existing is FullScreenPassRendererFeature && existing.name == FeatureName)
            {
                SetFeatureFields(existing, material);
                return;
            }
        }

        var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = FeatureName;
        SetFeatureFields(feature, material);
        AssetDatabase.AddObjectToAsset(feature, renderer);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

        // 렌더러 인스펙터의 Add Renderer Feature와 같은 방식 (기능 목록 + 파일 ID 목록)
        var so = new SerializedObject(renderer);
        SerializedProperty features = so.FindProperty("m_RendererFeatures");
        SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
        if (features == null || map == null)
        {
            Debug.LogError("[포스트프로세싱] 렌더러 기능 목록 필드를 못 찾음. 수동으로 Full Screen Pass Renderer Feature를 추가할 것");
            return;
        }
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer);
        Debug.Log($"[포스트프로세싱] {renderer.name}에 Full Screen Pass({FeatureName}) 추가");
    }

    // 필드 이름이 버전마다 달라도 컴파일이 깨지지 않게 직렬화 이름으로 설정
    private static void SetFeatureFields(ScriptableRendererFeature feature, Material material)
    {
        var so = new SerializedObject(feature);
        SerializedProperty mat = so.FindProperty("passMaterial");
        if (mat != null) mat.objectReferenceValue = material;
        else Debug.LogWarning("[포스트프로세싱] passMaterial 필드 없음 → Full Screen Pass 인스펙터에서 머티리얼 직접 지정");

        SerializedProperty injection = so.FindProperty("injectionPoint");
        if (injection != null)
        {
            int index = System.Array.IndexOf(injection.enumNames, "AfterRenderingPostProcessing");
            if (index >= 0) injection.enumValueIndex = index;
        }
        SerializedProperty fetch = so.FindProperty("fetchColorBuffer");
        if (fetch != null) fetch.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(feature);
    }

    private static ScriptableRendererData FindRendererData()
    {
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline != null)
        {
            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            SerializedProperty index = so.FindProperty("m_DefaultRendererIndex");
            if (list != null && list.arraySize > 0)
            {
                int i = index != null ? Mathf.Clamp(index.intValue, 0, list.arraySize - 1) : 0;
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData data) return data;
            }
        }
        return AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(FallbackRendererPath);
    }
}
