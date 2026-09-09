// Surface = depth-projected world-distance bands. Halo = perfect screen-space disc at the lamp.
Shader "KVH/KylePixelatorVolumeLight"
{
    Properties
    {
        [HDR] _LightColor ("Light Color", Color) = (1, 0.55, 0.22, 1)
        _Intensity ("Intensity", Float) = 1.5
        _Radius ("Surface Radius", Float) = 5
        _LightBlend ("Light Blend", Range(0, 2)) = 1
        _Quantization ("Surface Quantization", Range(2, 16)) = 6
        _CornerBrightness ("Corner Brightness", Range(0, 1)) = 0.35

        _HaloColor ("Halo Color", Color) = (1, 0.7, 0.35, 1)
        _HaloSize ("Halo Size (px)", Float) = 64
        _HaloBlend ("Halo Blend", Range(0, 2)) = 0.45
        _HaloQuantization ("Halo Quantization", Range(2, 16)) = 4

        _OutlineBoost ("Outline Boost (unused)", Range(0, 1)) = 0

        [Toggle] _ScreenSpaceShadows ("Screen Space Shadows", Float) = 0
        _ShadowSteps ("Shadow Steps (max)", Range(8, 64)) = 32
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1
        _ShadowThickness ("Shadow Thickness", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VolumeLight"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                float _Intensity;
                float _Radius;
                float _LightBlend;
                float _Quantization;
                float _CornerBrightness;
                half4 _HaloColor;
                float _HaloSize;
                float _HaloBlend;
                float _HaloQuantization;
                float _OutlineBoost;
                float _ScreenSpaceShadows;
                float _ShadowSteps;
                float _ShadowStrength;
                float _ShadowThickness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 lightPosWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.lightPosWS = GetObjectToWorldMatrix()._m03_m13_m23;
                return o;
            }

            float Quantize01(float x, float steps)
            {
                float s = max(round(steps), 2.0);
                return floor(saturate(x) * s + 1e-4) / s;
            }

            float3 SampleNormalWS(float2 uv)
            {
                float3 n = SampleSceneNormals(uv);
                float len = length(n);
                return len > 1e-4 ? n / len : float3(0, 1, 0);
            }

            float2 PositionToDepthUv(float3 positionWS)
            {
                float4 screen = ComputeScreenPos(TransformWorldToHClip(positionWS));
                return screen.xy / max(screen.w, 1e-5);
            }

            float ViewEye(float3 positionWS)
            {
                return -TransformWorldToView(positionWS).z;
            }

            // Surface-only occlusion. Halo is never multiplied by this.
            float ScreenSpaceShadow(float3 positionWS, float3 lightPosWS)
            {
                if (_ScreenSpaceShadows < 0.5)
                    return 1.0;

                float3 toLight = lightPosWS - positionWS;
                float dist = length(toLight);
                if (dist < 1e-3)
                    return 1.0;

                float3 dir = toLight / dist;
                float2 uv0 = PositionToDepthUv(positionWS);
                float2 uv1 = PositionToDepthUv(lightPosWS);
                float texelLen = length((uv1 - uv0) * _ScreenParams.xy);
                int maxSteps = (int)clamp(round(_ShadowSteps), 8.0, 64.0);
                int steps = (int)clamp(ceil(texelLen), 8.0, (float)maxSteps);

                float worldStep = dist / (float)steps;
                float bias = max(0.05, worldStep * 0.25);
                float thickness = max(max(_ShadowThickness, 0.35), worldStep * 1.25);
                float lightBody = 0.5;

                float t0 = 1.0 / (float)steps;
                float t1 = 0.88;

                [loop]
                for (int i = 1; i <= steps; i++)
                {
                    float t = lerp(t0, t1, (float)i / (float)steps);
                    float3 sampleWS = positionWS + dir * (dist * t);
                    float2 uv = PositionToDepthUv(sampleWS);
                    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                        continue;

                    float sceneRaw = SampleSceneDepth(uv);
                    if (sceneRaw >= 0.999999)
                        continue;

                    float3 sceneWS = ComputeWorldSpacePosition(uv, sceneRaw, UNITY_MATRIX_I_VP);
                    if (length(sceneWS - lightPosWS) < lightBody)
                        continue;

                    float depthDelta = ViewEye(sampleWS) - ViewEye(sceneWS);
                    if (depthDelta > bias && depthDelta < thickness)
                        return 1.0 - saturate(_ShadowStrength);
                }
                return 1.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                float3 lightPosWS = input.lightPosWS;

                // --- Halo: perfect screen-space circle centered on the lamp ---
                float2 lightUV = PositionToDepthUv(lightPosWS);
                float dPx = length((uv - lightUV) * _ScreenParams.xy);
                float haloPx = max(_HaloSize, 1.0);
                half3 halo = 0;
                if (dPx < haloPx)
                {
                    float h = Quantize01(1.0 - dPx / haloPx, _HaloQuantization);
                    halo = _HaloColor.rgb * (_Intensity * _HaloBlend) * h;
                }

                // --- Surface: world-distance bands on reconstructed geometry ---
                half3 surface = 0;
                float rawDepth = SampleSceneDepth(uv);
                if (rawDepth < 0.999999)
                {
                    float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                    float radius = max(_Radius, 1e-3);
                    float dist = length(lightPosWS - positionWS);
                    if (dist <= radius)
                    {
                        float3 ldir = (lightPosWS - positionWS) / max(dist, 1e-4);
                        float ndotl = saturate(dot(SampleNormalWS(uv), ldir));
                        float face = lerp(1.0, ndotl, saturate(_CornerBrightness));
                        float shadow = ScreenSpaceShadow(positionWS, lightPosWS);
                        float atten = Quantize01(1.0 - dist / radius, _Quantization);
                        surface = _LightColor.rgb * (_Intensity * _LightBlend) * atten * face * shadow;
                    }
                }

                half3 lighting = surface + halo;
                lighting *= 1.0; // outline boost removed, kept multiply for stable shader variants
                return half4(lighting, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
