using System;
using UnityEngine;
using UnityEngine.Rendering;

// URP에 2D용 기본 효과가 없는 포그, 프린징, 렌즈 플레어 수치. Volume 프로필에 Bloom/Vignette/Chromatic Aberration과 같이 넣고 인스펙터에서 조정.
// 실제 그리기는 풀스크린 셰이더(DungeonHunt/LensEffects) 한 장. PostEffectsDriver가 매 프레임 값을 셰이더 전역 변수로 넘김.
// 볼륨 블렌딩(여러 Volume, 가중치)이 그대로 적용됨.
[Serializable, VolumeComponentMenu("DungeonHunt/Lens Effects (Fog, Fringing, Lens Flare)")]
public sealed class DungeonLensEffects : VolumeComponent
{
    [Header("포그 (월드에 붙은 흙먼지/안개, 화면 가장자리에 더 짙게)")]
    [Tooltip("0이면 끔")]
    public ClampedFloatParameter fogIntensity = new ClampedFloatParameter(0f, 0f, 1f);
    public ColorParameter fogColor = new ColorParameter(new Color(0.55f, 0.5f, 0.45f, 1f));
    [Tooltip("노이즈 한 덩어리 크기 (월드 칸)")]
    public ClampedFloatParameter fogScale = new ClampedFloatParameter(6f, 0.5f, 40f);
    [Tooltip("흐르는 속도 (칸/초)")]
    public Vector2Parameter fogSpeed = new Vector2Parameter(new Vector2(0.25f, 0.08f));
    [Tooltip("노이즈 대비. 클수록 덩어리가 또렷함")]
    public ClampedFloatParameter fogContrast = new ClampedFloatParameter(1.4f, 0.2f, 4f);
    [Tooltip("0 = 화면 전체 고르게, 1 = 가장자리에만")]
    public ClampedFloatParameter fogEdgeBias = new ClampedFloatParameter(0.6f, 0f, 1f);

    [Header("프린징 (밝은 경계의 보라/청록 번짐)")]
    public ClampedFloatParameter fringeIntensity = new ClampedFloatParameter(0f, 0f, 2f);
    public ColorParameter fringeColor = new ColorParameter(new Color(0.65f, 0.25f, 1f, 1f));
    [Tooltip("경계 검사 거리 (픽셀)")]
    public ClampedFloatParameter fringeWidth = new ClampedFloatParameter(2f, 0.5f, 8f);
    [Tooltip("이 밝기 차보다 큰 경계에만")]
    public ClampedFloatParameter fringeThreshold = new ClampedFloatParameter(0.15f, 0f, 1f);
    [Tooltip("0 = 화면 전체, 1 = 가장자리로 갈수록 강하게")]
    public ClampedFloatParameter fringeRadial = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Header("렌즈 플레어 (밝은 곳 기준 고스트, 헤일로, 가로 스트릭)")]
    public ClampedFloatParameter flareIntensity = new ClampedFloatParameter(0f, 0f, 2f);
    [Tooltip("이 밝기보다 밝은 픽셀만 플레어를 만듦")]
    public ClampedFloatParameter flareThreshold = new ClampedFloatParameter(0.85f, 0f, 2f);
    public ColorParameter flareTint = new ColorParameter(new Color(1f, 0.85f, 0.7f, 1f));
    [Tooltip("고스트 개수 (0~4)")]
    public ClampedIntParameter flareGhostCount = new ClampedIntParameter(3, 0, 4);
    [Tooltip("고스트 사이 간격 (화면 비율)")]
    public ClampedFloatParameter flareGhostSpacing = new ClampedFloatParameter(0.35f, 0.05f, 1f);
    [Tooltip("고스트 색 번짐")]
    public ClampedFloatParameter flareChromatic = new ClampedFloatParameter(0.006f, 0f, 0.03f);
    public ClampedFloatParameter flareHaloIntensity = new ClampedFloatParameter(0.5f, 0f, 2f);
    [Tooltip("헤일로 반지름 (화면 세로 비율)")]
    public ClampedFloatParameter flareHaloRadius = new ClampedFloatParameter(0.45f, 0.05f, 1f);
    public ClampedFloatParameter flareStreakIntensity = new ClampedFloatParameter(0.4f, 0f, 2f);
    [Tooltip("가로 스트릭 길이 (화면 가로 비율)")]
    public ClampedFloatParameter flareStreakLength = new ClampedFloatParameter(0.12f, 0.01f, 0.5f);

    public bool AnyActive => fogIntensity.value > 0f || fringeIntensity.value > 0f || flareIntensity.value > 0f;
}
