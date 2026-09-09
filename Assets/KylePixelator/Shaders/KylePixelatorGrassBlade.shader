// Billboard grass — MeshRenderer baked field (UV1=scale/atlas/shade, UV2=rootWS).
// Instanced draws were dropping _BaseMap → white; this path matches a normal MeshRenderer.
Shader "KVH/KylePixelatorGrassBlade"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.45, 0.72, 0.38, 1)
        [MainTexture] _BaseMap ("Shape Atlas (A)", 2D) = "white" {}
        _ShadowTint ("Shadow Tint", Color) = (0.32, 0.48, 0.30, 1)
        _HighlightTint ("Highlight Tint", Color) = (0.55, 0.82, 0.45, 1)
        _DarkMul ("Dark Mul (F8)", Range(0.5, 0.95)) = 0.88
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.35
        _AtlasColumns ("Atlas Columns", Float) = 4
        _Width ("Width", Float) = 0.55
        _Height ("Height", Float) = 0.85
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.08
        _WindSpeed ("Wind Speed", Range(0, 4)) = 1.1
        _WindScale ("Wind Scale", Range(0.05, 2)) = 0.35
        _WindDirX ("Wind Dir X", Range(-1, 1)) = 1
        _WindDirZ ("Wind Dir Z", Range(-1, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _KYLEPIXELATOR_CEL_OFF

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Include/KylePixelatorGrassWind.hlsl"
            #include "Include/KylePixelatorCloudShadow.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadowTint;
                half4  _HighlightTint;
                float  _DarkMul;
                float  _Cutoff;
                float  _AtlasColumns;
                float  _Width;
                float  _Height;
                float  _WindStrength;
                float  _WindSpeed;
                float  _WindScale;
                float  _WindDirX;
                float  _WindDirZ;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _KylePixelator_LightBands;
            float _KylePixelator_BandSoftness;
            float _KylePixelator_LightWrap;
            float _KylePixelator_AdditionalLightsMode;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 scale      : TEXCOORD1; // x = scale, y = atlas cell, z = dark shade 0/1
                float3 rootWS     : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 rootWS     : TEXCOORD1;
                nointerpolation float shade : TEXCOORD2; // hard 0/1 — no soft gradient
            };

            // Match ToonLit: optional step on punctual atten so fill stays readable at low-res.
            float MaybeStepPunctualAtten(float atten)
            {
                float steps = 4.0;
                if (steps < 1.5)
                    return atten;
                return floor(atten * steps + 1e-4) / steps;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;

                float3 rootWS = input.rootWS;
                if (dot(rootWS, rootWS) < 1e-8)
                    rootWS = TransformObjectToWorld(float3(0, 0, 0));

                float instScale = max(input.scale.x, 0.05);
                float2 card = input.positionOS.xy; // unit quad ±0.5
                float tip = saturate(card.y + 0.5);

                float3 camWS = GetCameraPositionWS();
                float3 toCam = camWS - rootWS;
                toCam.y = 0;
                float len2 = max(dot(toCam, toCam), 1e-6);
                toCam *= rsqrt(len2);
                float3 right = normalize(cross(float3(0, 1, 0), toCam));
                float3 up = float3(0, 1, 0);

                float2 windDir;
                float windStr;
                float nSigned;
                if (_KylePixelator_GrassWindActive > 0.5)
                {
                    float nUnsigned;
                    KyleGrassWindSample(rootWS.xz, windDir, nSigned, nUnsigned);
                    windStr = _KylePixelator_GrassWindParams.z;
                }
                else
                {
                    windDir = float2(_WindDirX, _WindDirZ);
                    float dirLen = max(length(windDir), 1e-4);
                    windDir /= dirLen;
                    windStr = _WindStrength;
                    float2 noiseUV = rootWS.xz * max(_WindScale, 1e-3) + windDir * (_Time.y * _WindSpeed);
                    nSigned = KyleGrassValueNoise(noiseUV) * 2.0 - 1.0;
                }

                float sway = KyleGrassQuantizeSway(nSigned * windStr * tip * tip);
                float3 world = rootWS
                    + right * (card.x * _Width * instScale)
                    + up * (tip * _Height * instScale)
                    + float3(windDir.x, 0.0, windDir.y) * sway;

                o.positionCS = TransformWorldToHClip(world);

                float cols = max(round(_AtlasColumns), 1.0);
                float cell = clamp(floor(input.scale.y + 0.5), 0.0, cols - 1.0);
                float2 localUv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.uv = float2((localUv.x + cell) / cols, localUv.y);
                o.rootWS = rootWS;
                o.shade = input.scale.z > 0.5 ? 1.0 : 0.0;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a - _Cutoff);

                // Shape from alpha; darker roll pulls toward lawn ShadowTint so it matches cel family.
                half3 darkAlbedo = lerp(_BaseColor.rgb * _DarkMul, _ShadowTint.rgb, 0.55);
                half3 albedo = lerp(_BaseColor.rgb, darkAlbedo, input.shade);

                // Peer-style: sample sun shadow at root so the whole tuft dims evenly.
                float3 rootPos = input.rootWS + float3(0.0, 0.08, 0.0);
                float4 shadowCoord = TransformWorldToShadowCoord(rootPos);
                Light mainLight = GetMainLight(shadowCoord, rootPos, half4(1, 1, 1, 1));

                // Real shadows → ShadowTint/HighlightTint. Clouds multiply last (same as ToonLit lawn).
                half3 tint = lerp(_ShadowTint.rgb, _HighlightTint.rgb, mainLight.shadowAttenuation);
                half3 lit = tint * mainLight.color;

            #if defined(_ADDITIONAL_LIGHTS)
                float addMode = _KylePixelator_AdditionalLightsMode;
                if (addMode < 1.5)
                {
                    half4 shadowMask = half4(1, 1, 1, 1);
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, rootPos, shadowMask);
                        float atten = MaybeStepPunctualAtten(l.distanceAttenuation) * l.shadowAttenuation;
                        lit += _HighlightTint.rgb * l.color * atten;
                    LIGHT_LOOP_END
                }
            #endif

                half3 color = albedo * lit;
                color *= KyleCloudShadowFactor(input.rootWS.xz);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
