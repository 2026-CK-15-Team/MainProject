// 풀스크린 후처리: 포그, 프린징, 렌즈 플레어 (고스트, 헤일로, 가로 스트릭).
// URP Full Screen Pass Renderer Feature(After Rendering Post Processing, Fetch Color Buffer 켬)로 그림.
// 수치는 Volume 프로필의 DungeonLensEffects → PostEffectsDriver → 전역 변수. 세기가 0인 효과는 계산을 건너뜀.
Shader "DungeonHunt/LensEffects"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "LensEffects"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _DH_FogParams;     // 세기, 크기, 대비, 가장자리
            float4 _DH_FogColor;
            float4 _DH_FogOffset;
            float4 _DH_CamRect;       // 카메라 중심 xy, 화면 폭/높이 (월드)
            float4 _DH_FringeParams;  // 세기, 폭(px), 문턱, 방사
            float4 _DH_FringeColor;
            float4 _DH_FlareParams;   // 세기, 문턱, 고스트 수, 간격
            float4 _DH_FlareParams2;  // 색 번짐, 헤일로 세기, 헤일로 반지름, 스트릭 세기
            float4 _DH_FlareParams3;  // 스트릭 길이
            float4 _DH_FlareTint;

            float3 SampleColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb; // 반복문 안 샘플링이라 LOD 고정
            }

            float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            // ---------- 포그 ----------
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    v += Noise(p) * amp;
                    p = p * 2.03 + float2(17.1, 9.2);
                    amp *= 0.5;
                }
                return v;
            }

            float3 ApplyFog(float3 col, float2 uv)
            {
                float intensity = _DH_FogParams.x;
                if (intensity <= 0.0) return col;
                // 월드 좌표 기준이라 카메라가 움직여도 안개가 땅에 붙어 있음
                float2 world = _DH_CamRect.xy + (uv - 0.5) * _DH_CamRect.zw;
                float2 p = (world + _DH_FogOffset.xy) / max(_DH_FogParams.y, 0.01);
                float n = Fbm(p) * 0.65 + Fbm(p * 0.5 - _DH_FogOffset.xy * 0.07) * 0.35;
                n = saturate((n - 0.5) * _DH_FogParams.z + 0.5);
                float2 d = (uv - 0.5) * float2(_DH_CamRect.z / max(_DH_CamRect.w, 0.0001), 1.0);
                float edge = saturate(length(d) * 1.4);
                float mask = lerp(1.0, edge * edge, _DH_FogParams.w);
                return lerp(col, _DH_FogColor.rgb, saturate(n * mask * intensity));
            }

            // ---------- 프린징 ----------
            float3 ApplyFringe(float3 col, float2 uv)
            {
                float intensity = _DH_FringeParams.x;
                if (intensity <= 0.0) return col;
                float2 px = _DH_FringeParams.y / _ScreenParams.xy;
                float l = Luma(col);
                float gx = abs(Luma(SampleColor(uv + float2(px.x, 0))) - Luma(SampleColor(uv - float2(px.x, 0))));
                float gy = abs(Luma(SampleColor(uv + float2(0, px.y))) - Luma(SampleColor(uv - float2(0, px.y))));
                float edge = saturate((max(gx, gy) - _DH_FringeParams.z) * 4.0);
                // 밝은 쪽 경계 바깥(어두운 쪽)에 색이 번지는 느낌: 어두울수록 더 보이게
                edge *= saturate(1.2 - l);
                float radial = saturate(length(uv - 0.5) * 2.0);
                float weight = lerp(1.0, radial * radial, _DH_FringeParams.w);
                return col + _DH_FringeColor.rgb * edge * weight * intensity;
            }

            // ---------- 렌즈 플레어 ----------
            float3 Bright(float2 uv)
            {
                float3 c = SampleColor(uv);
                float l = Luma(c);
                return c * saturate((l - _DH_FlareParams.y) / max(1.0 - _DH_FlareParams.y, 0.05));
            }

            float3 BrightChromatic(float2 uv, float2 dir)
            {
                float k = _DH_FlareParams2.x;
                return float3(Bright(uv + dir * k).r, Bright(uv).g, Bright(uv - dir * k).b);
            }

            float3 ApplyFlare(float3 col, float2 uv)
            {
                float intensity = _DH_FlareParams.x;
                if (intensity <= 0.0) return col;
                float2 flipped = 1.0 - uv;                       // 화면 중심 반대편
                float2 toCenter = 0.5 - flipped;
                float2 dir = normalize(toCenter + 1e-5);
                float3 flare = 0;

                // 고스트: 밝은 곳의 반대편에 줄지어 생기는 상
                int ghosts = (int)_DH_FlareParams.z;
                float2 step = toCenter * _DH_FlareParams.w;
                [loop] for (int i = 0; i < ghosts; i++)
                {
                    float2 s = flipped + step * (float)i;
                    float w = pow(saturate(1.0 - length(0.5 - s) / 0.7071), 6.0);
                    flare += BrightChromatic(s, dir) * w;
                }

                // 헤일로: 화면 중심을 둘러싼 고리
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 haloVec = dir * _DH_FlareParams2.z * float2(1.0 / aspect, 1.0);
                float2 hs = flipped + haloVec;
                float hw = pow(saturate(1.0 - abs(length(0.5 - hs) - 0.35) * 6.0), 3.0);
                flare += BrightChromatic(hs, dir) * hw * _DH_FlareParams2.y;

                // 가로 스트릭: 밝은 점에서 좌우로 길게
                float3 streak = 0;
                float len = _DH_FlareParams3.x;
                [unroll] for (int k = 1; k <= 8; k++)
                {
                    float t = k / 8.0;
                    float w = (1.0 - t) * (1.0 - t);
                    streak += (Bright(uv + float2(len * t, 0)) + Bright(uv - float2(len * t, 0))) * w;
                }
                flare += streak * 0.25 * _DH_FlareParams2.w;

                return col + flare * _DH_FlareTint.rgb * intensity;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 col = SampleColor(uv);
                col = ApplyFog(col, uv);
                col = ApplyFringe(col, uv);
                col = ApplyFlare(col, uv);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
