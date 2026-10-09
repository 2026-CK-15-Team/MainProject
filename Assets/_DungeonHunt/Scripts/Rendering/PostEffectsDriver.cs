using UnityEngine;
using UnityEngine.Rendering;

// 볼륨 스택의 DungeonLensEffects 값을 풀스크린 셰이더 전역 변수로 넘김. 메인 카메라에 붙음 (GameBootstrap).
// 셰이더는 URP Renderer의 Full Screen Pass Renderer Feature가 그림 (DungeonHunt/Setup/Setup Post Processing).
[RequireComponent(typeof(Camera))]
public class PostEffectsDriver : MonoBehaviour
{
    private static readonly int FogParams = Shader.PropertyToID("_DH_FogParams");       // 세기, 크기, 대비, 가장자리
    private static readonly int FogColor = Shader.PropertyToID("_DH_FogColor");
    private static readonly int FogOffset = Shader.PropertyToID("_DH_FogOffset");       // 흐름 누적 (월드 칸)
    private static readonly int CamRect = Shader.PropertyToID("_DH_CamRect");           // 카메라 중심 xy, 화면 폭/높이 (월드)
    private static readonly int FringeParams = Shader.PropertyToID("_DH_FringeParams"); // 세기, 폭(px), 문턱, 방사
    private static readonly int FringeColor = Shader.PropertyToID("_DH_FringeColor");
    private static readonly int FlareParams = Shader.PropertyToID("_DH_FlareParams");   // 세기, 문턱, 고스트 수, 간격
    private static readonly int FlareParams2 = Shader.PropertyToID("_DH_FlareParams2"); // 색 번짐, 헤일로 세기, 헤일로 반지름, 스트릭 세기
    private static readonly int FlareParams3 = Shader.PropertyToID("_DH_FlareParams3"); // 스트릭 길이
    private static readonly int FlareTint = Shader.PropertyToID("_DH_FlareTint");

    private Camera cam;
    private Vector2 fogOffset;

    private void Awake() => cam = GetComponent<Camera>();

    private void LateUpdate()
    {
        DungeonLensEffects fx = VolumeManager.instance != null && VolumeManager.instance.stack != null
            ? VolumeManager.instance.stack.GetComponent<DungeonLensEffects>()
            : null;
        if (fx == null || !fx.active)
        {
            Shader.SetGlobalVector(FogParams, Vector4.zero);
            Shader.SetGlobalVector(FringeParams, Vector4.zero);
            Shader.SetGlobalVector(FlareParams, Vector4.zero);
            return;
        }

        // 포그 흐름은 연출이라 일시정지와 무관하게 실제 시간으로
        fogOffset += fx.fogSpeed.value * Time.unscaledDeltaTime;
        float height = cam.orthographic ? cam.orthographicSize * 2f : 10f;
        Vector3 p = cam.transform.position;
        Shader.SetGlobalVector(CamRect, new Vector4(p.x, p.y, height * cam.aspect, height));
        Shader.SetGlobalVector(FogParams, new Vector4(fx.fogIntensity.value, fx.fogScale.value, fx.fogContrast.value, fx.fogEdgeBias.value));
        Shader.SetGlobalColor(FogColor, fx.fogColor.value);
        Shader.SetGlobalVector(FogOffset, fogOffset);

        Shader.SetGlobalVector(FringeParams, new Vector4(fx.fringeIntensity.value, fx.fringeWidth.value, fx.fringeThreshold.value, fx.fringeRadial.value));
        Shader.SetGlobalColor(FringeColor, fx.fringeColor.value);

        Shader.SetGlobalVector(FlareParams, new Vector4(fx.flareIntensity.value, fx.flareThreshold.value, fx.flareGhostCount.value, fx.flareGhostSpacing.value));
        Shader.SetGlobalVector(FlareParams2, new Vector4(fx.flareChromatic.value, fx.flareHaloIntensity.value, fx.flareHaloRadius.value, fx.flareStreakIntensity.value));
        Shader.SetGlobalVector(FlareParams3, new Vector4(fx.flareStreakLength.value, 0f, 0f, 0f));
        Shader.SetGlobalColor(FlareTint, fx.flareTint.value);
    }
}
