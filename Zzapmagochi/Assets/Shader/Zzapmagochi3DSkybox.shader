Shader "Zzapmagochi/Skybox/3DStarPatternSkybox"
{
    Properties
    {
        [Header(Background Colors)]
        _BgColorCenter ("Center Purple Color", Color) = (0.45, 0.38, 0.52, 1)
        _BgColorEdge ("Edge Vignette Color", Color) = (0.32, 0.25, 0.40, 1)
        _PatternColor ("Star Pattern Color", Color) = (0.62, 0.54, 0.72, 0.55)

        [Header(Star Pattern Settings)]
        _PatternTex ("Star Pattern Mask (RGBA)", 2D) = "black" {}
        _Tiling ("Pattern Density (Tiling)", Float) = 2.5
        _RotationDeg ("Diagonal Angle (Degrees)", Range(-180, 180)) = -18.0
        _ScrollVelocity ("Main Scroll Speed (X, Y)", Vector) = (0.03, 0.02, 0, 0)

        [Header(Sub Star Layer)]
        [Toggle] _UseSubLayer ("Enable Small Stars Layer", Float) = 1
        _SubLayerScale ("Sub Layer Scale", Float) = 2.1
        _SubLayerAlpha ("Sub Layer Opacity", Range(0, 1)) = 0.45
        _SubScrollVelocity ("Sub Scroll Speed (X, Y)", Vector) = (0.015, 0.01, 0, 0)

        [Header(3D Camera Orbit and Twinkle)]
        _OrbitParallax ("3D Camera Rotate Parallax", Range(0, 2)) = 0.6
        _PanParallax ("3D Camera Pan Parallax", Range(0, 0.2)) = 0.02
        _TwinkleSpeed ("Twinkle Speed", Range(0, 5)) = 1.8
        _TwinkleAmount ("Twinkle Intensity", Range(0, 0.5)) = 0.15
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            TEXTURE2D(_PatternTex);
            SAMPLER(sampler_PatternTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BgColorCenter;
                float4 _BgColorEdge;
                float4 _PatternColor;
                float4 _ScrollVelocity;
                float4 _SubScrollVelocity;
                float _Tiling;
                float _RotationDeg;
                float _UseSubLayer;
                float _SubLayerScale;
                float _SubLayerAlpha;
                float _OrbitParallax;
                float _PanParallax;
                float _TwinkleSpeed;
                float _TwinkleAmount;
            CBUFFER_END

            float2 RotateUV(float2 uv, float degrees)
            {
                float rad = radians(degrees);
                float s = sin(rad);
                float c = cos(rad);
                return mul(float2x2(c, -s, s, c), uv);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 화면 정규화 좌표 (-0.5 ~ +0.5)
                float2 rawUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float2 centeredUV = rawUV - 0.5;

                // 2. 중앙 -> 외곽 보라색 비네팅 그라데이션
                float vignette = saturate(length(centeredUV) * 1.414);
                half3 bgColor = lerp(_BgColorCenter.rgb, _BgColorEdge.rgb, vignette);

                // 3. 화면 종횡비(Aspect Ratio) 자동 보정
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uv = float2(centeredUV.x * aspect, centeredUV.y);

                // 4. 3D 카메라 회전(Orbit) 및 이동(Pan) 오프셋 반영
                // View Matrix에서 카메라의 전방 벡터를 추출해 Yaw/Pitch 회전량을 UV 오프셋으로 변환
                float3 camForward = -UNITY_MATRIX_V[2].xyz;
                float yawOffset = atan2(camForward.x, camForward.z) * 0.31830988; // 1 / PI
                float pitchOffset = asin(clamp(camForward.y, -1.0, 1.0)) * 0.63661977;

                float2 camera3DOffset = float2(yawOffset, pitchOffset) * _OrbitParallax
                                      + _WorldSpaceCameraPos.xy * _PanParallax;

                float2 baseUV = RotateUV(uv + camera3DOffset, _RotationDeg) * _Tiling;

                // 5. 큰 별 레이어 & 작은 별 레이어 샘플링
                float2 uvMain = baseUV + (_ScrollVelocity.xy * _Time.y);
                half mainStar = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, uvMain).a;

                half subStar = 0;
                if (_UseSubLayer > 0.5)
                {
                    float2 uvSub = (baseUV * _SubLayerScale) + (_SubScrollVelocity.xy * _Time.y) + float2(0.41, 0.63);
                    subStar = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, uvSub).a * _SubLayerAlpha;
                }

                // 6. 별 반짝임(Twinkle) 및 최종 합성
                float twinkle = 1.0 + sin(_Time.y * _TwinkleSpeed) * _TwinkleAmount;
                half starMask = saturate(mainStar + subStar) * _PatternColor.a * twinkle;

                half3 finalColor = lerp(bgColor, _PatternColor.rgb, starMask);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}