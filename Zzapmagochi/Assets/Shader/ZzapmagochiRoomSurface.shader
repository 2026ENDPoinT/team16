Shader "Zzapmagochi/Room/LitRoomSurfaceWithCornerAO"
{
    Properties
    {
        // SpriteRenderer가 스프라이트를 바꾸면 렌더러별 텍스처로 즉시 인식
        [PerRendererData] [MainTexture] _MainTex ("Sprite / Surface Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Surface Tint", Color) = (1, 1, 1, 1)

        [Header(Room Lighting)]
        _RoomLightColor ("Room Light Color", Color) = (1.0, 0.95, 0.86, 1.0)
        _RoomLightIntensity ("Room Light Intensity", Range(0, 2.5)) = 1.05
        _RoomAmbientColor ("Ambient Shadow Tint", Color) = (0.42, 0.38, 0.52, 1.0)
        _RoomLightDir ("Light Direction (World)", Vector) = (-0.35, 0.8, -0.45, 0)

        [Header(Corner Ambient Occlusion)]
        _RoomCornerMin ("Room Corner Min (X, Y, Z)", Vector) = (-2.5, 0.0, -2.5, 0)
        _RoomCornerMax ("Room Corner Max (X, Y, Z)", Vector) = (2.5, 4.0, 2.5, 0)
        _CornerDarkness ("Corner Darkness Strength", Range(0, 1)) = 0.45
        _CornerRadius ("Corner Shadow Radius", Range(0.2, 3.5)) = 1.35
        _ShadowColor ("Corner Shadow Color", Color) = (0.22, 0.18, 0.30, 1.0)

        [Header(Surface Shading)]
        _SurfaceBrightness ("Surface Face Brightness", Range(0.5, 1.2)) = 1.0
        [Toggle] _ZWrite ("Write Depth (ZWrite)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Cull Off
        ZWrite [_ZWrite]
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RoomSurfaceLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _RoomLightColor;
                float4 _RoomAmbientColor;
                float4 _RoomLightDir;
                float4 _RoomCornerMin;
                float4 _RoomCornerMax;
                float4 _ShadowColor;
                float _RoomLightIntensity;
                float _CornerDarkness;
                float _CornerRadius;
                float _SurfaceBrightness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 albedo = texSample.rgb * input.color.rgb * _BaseColor.rgb * _SurfaceBrightness;

                // 1. 방향성 조명 (벽면/바닥면 각도에 따른 자연스러운 명암 차이)
                float3 N = normalize(input.normalWS);
                float3 L = normalize(_RoomLightDir.xyz);
                // 양면(Cull Off) 스프라이트에서도 법선이 빛 쪽을 향하도록 보정
                float ndotl = abs(dot(N, L)) * 0.55 + 0.45;
                half3 litColor = lerp(_RoomAmbientColor.rgb, _RoomLightColor.rgb * _RoomLightIntensity, saturate(ndotl));

                // 2. 자취방 구석탱이(Corner) & 모서리(Crease) 음영 계산
                // 방 안쪽 벽은 x = _RoomCornerMin.x (-2.5), z = _RoomCornerMax.z (+2.5), 바닥은 y = _RoomCornerMin.y (0.0)
                float distLeftWall  = max(0.0, input.positionWS.x - _RoomCornerMin.x);
                float distBackWall  = max(0.0, _RoomCornerMax.z - input.positionWS.z);
                float distFloor     = max(0.0, input.positionWS.y - _RoomCornerMin.y);

                float r = max(0.2, _CornerRadius);

                // (a) 왼쪽 벽(-X)과 오른쪽 뒷벽(+Z)이 만나는 수직 구석 모서리
                float wallWallFactor = saturate(1.0 - (distLeftWall / r)) * saturate(1.0 - (distBackWall / r));

                // (b) 왼쪽 벽과 바닥이 만나는 모서리 & 뒷벽과 바닥이 만나는 모서리
                float leftFloorFactor = saturate(1.0 - (distLeftWall / (r * 0.85))) * saturate(1.0 - (distFloor / (r * 0.85)));
                float backFloorFactor = saturate(1.0 - (distBackWall / (r * 0.85))) * saturate(1.0 - (distFloor / (r * 0.85)));

                // (c) 세 면(왼벽 + 뒷벽 + 바닥)이 모두 만나는 가장 깊숙한 구석탱이 코너 (-2.5, 0, +2.5)
                float deepCornerDist = length(float3(distLeftWall, distFloor * 0.9, distBackWall));
                float deepCornerFactor = saturate(1.0 - (deepCornerDist / (r * 1.35)));
                deepCornerFactor = deepCornerFactor * deepCornerFactor;

                // 종합 구석 음영 계수 (0 = 밝음, 1 = 가장 어두운 구석)
                float ao = saturate(
                    wallWallFactor * 0.65 +
                    max(leftFloorFactor, backFloorFactor) * 0.50 +
                    deepCornerFactor * 0.75
                );

                float shadowAmount = saturate(ao * _CornerDarkness);

                half3 shaded = albedo * litColor;
                half3 finalRgb = lerp(shaded, shaded * _ShadowColor.rgb, shadowAmount);

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }
}
