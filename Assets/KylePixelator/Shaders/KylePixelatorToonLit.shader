// cel / banded lighting for the 3D pixel-art path.
// Realtime URP lights in, 2–6 flat value bands out. Shadow attenuation is folded into the
// band value (a shadow *is* the darkest band, never a separate smooth gradient).
// Per-material palette: albedo * lerp(ShadowTint, HighlightTint, band) * lightColor
// (t3ssel8r / Leng-style artistic control, not bare albedo x N.L).
//
// Globals (pushed by CelLightingGlobals from Settings):
//   _KylePixelator_LightBands              2..6   (0 = unset → 3)
//   _KylePixelator_BandSoftness            0..0.1 half-width of each threshold ramp, in N·L units
//   _KylePixelator_LightWrap              -0.5..0.5 terminator shift
//   _KylePixelator_AdditionalLightsMode    0=Banded (unset) 1=TintOnly 2=Off
//   _KylePixelator_PunctualAttenSteps      0=off, else floor(att*N)/N on distance atten
//   keyword _KYLEPIXELATOR_CEL_OFF        A/B: smooth Lambert with the same tint model
Shader "KVH/KylePixelatorToonLit"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _ShadowTint ("Shadow Tint", Color) = (0.45, 0.5, 0.65, 1)
        _HighlightTint ("Highlight Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [Toggle] _ReceiveGrassWind ("Receive Grass Wind Shade", Float) = 0
        _GrassWindShade ("Grass Wind Shade", Range(0, 1)) = 0.35
        [Toggle] _ReceiveCloudShadow ("Receive Cloud Shadow", Float) = 0
        _CloudShadowStrength ("Cloud Shadow Strength", Range(0, 1)) = 1
        // Set via CameraOcclusionFade MPB when this mesh blocks the player view (0 = solid).
        [HideInInspector] _OcclusionFade ("Occlusion Fade", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Identical in every pass → SRP Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _ShadowTint;
            half4  _HighlightTint;
            half4  _EmissionColor;
            float  _ReceiveGrassWind;
            float  _GrassWindShade;
            float  _ReceiveCloudShadow;
            float  _CloudShadowStrength;
            float  _OcclusionFade;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        // ------------------------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ToonVert
            #pragma fragment ToonFrag

            // URP lighting keywords (subset the toon model actually reads).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _KYLEPIXELATOR_CEL_OFF

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _KylePixelator_LightBands;
            float _KylePixelator_BandSoftness;
            float _KylePixelator_LightWrap;
            float _KylePixelator_AdditionalLightsMode;
            float _KylePixelator_PunctualAttenSteps;

            #include "Include/KylePixelatorGrassWind.hlsl"
            #include "Include/KylePixelatorCloudShadow.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ToonVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(input.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS   = nrm.normalWS;
                o.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            // Bands levels 0 .. bands-1 → 0..1. Thresholds sit at k/bands in x, each with a
            // symmetric ramp of half-width `softness` (N·L units) so a moving mesh does not
            // flip a whole texel on a hair's breadth.
            float Quantize(float x, float bands, float softness)
            {
                float s = x * bands;
                float i = floor(s + 0.5);           // nearest boundary
                float d = s - i;                    // signed distance to it, in cells
                float w = max(softness * bands, 1e-4);
                float level = i - 1.0 + smoothstep(-w, w, d);
                return saturate(level / max(bands - 1.0, 1.0));
            }

            float BandValue(float raw, float bands, float softness)
            {
            #if defined(_KYLEPIXELATOR_CEL_OFF)
                return raw;
            #else
                return Quantize(raw, bands, softness);
            #endif
            }

            // Dir lights keep atten≈1; stepping only bites point/spot falloff.
            float MaybeStepPunctualAtten(float atten)
            {
                float steps = _KylePixelator_PunctualAttenSteps;
                if (steps < 1.5)
                    return atten;
                return floor(atten * steps + 1e-4) / steps;
            }

            half3 PaletteShade(float q)
            {
                return lerp(_ShadowTint.rgb, _HighlightTint.rgb, q);
            }

            half4 ToonFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // CameraOcclusionFade: screen-door dither so cover in front of the hero goes see-through.
                if (_OcclusionFade > 1e-3)
                {
                    float2 sp = input.positionCS.xy;
                    uint xi = (uint)sp.x & 3u;
                    uint yi = (uint)sp.y & 3u;
                    static const float kBayer4[16] = {
                        0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                        12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                        3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                        15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
                    };
                    float threshold = kBayer4[yi * 4u + xi];
                    clip(threshold - _OcclusionFade);
                }

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                // Same gust sheets as billboard tufts — darken the lawn where wind densifies (not geo sway).
                if (_ReceiveGrassWind > 0.5 && _KylePixelator_GrassWindActive > 0.5 && _GrassWindShade > 1e-4)
                {
                    float2 wdir;
                    float nSigned;
                    float nUnsigned;
                    KyleGrassWindSample(input.positionWS.xz, wdir, nSigned, nUnsigned);
                    // Low noise = denser bend / darker patch; keep it subtle under cel bands.
                    float gust = lerp(1.0, lerp(0.82, 1.0, nUnsigned), _GrassWindShade);
                    albedo *= gust;
                }

                float3 n = NormalizeNormalPerPixel(input.normalWS);

                float bands    = (_KylePixelator_LightBands < 1.5) ? 3.0 : round(_KylePixelator_LightBands);
                float softness = _KylePixelator_BandSoftness;
                float wrap     = _KylePixelator_LightWrap;

                // ---- Main light ------------------------------------------------------------
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));

                float raw = saturate(dot(n, mainLight.direction) + wrap);
                raw = min(raw, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                float q = BandValue(raw, bands, softness);

                half3 color = albedo * PaletteShade(q) * mainLight.color;

                // ---- Additional lights (fill directional; avoid stock Point/Spot for the look) ----
            #if defined(_ADDITIONAL_LIGHTS)
                float addMode = _KylePixelator_AdditionalLightsMode;
                if (addMode < 1.5)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    half4 shadowMask = half4(1, 1, 1, 1);

                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        float atten = MaybeStepPunctualAtten(l.distanceAttenuation) * l.shadowAttenuation;
                        float r = saturate(dot(n, l.direction) + wrap) * atten;
                        float qa = (addMode > 0.5) ? r : BandValue(r, bands, softness);
                        color += albedo * PaletteShade(qa) * l.color;
                    LIGHT_LOOP_END
                }
            #endif

                // emission (bulb / crystal) is source glow, not scene lighting
                color += _EmissionColor.rgb;

                if (_ReceiveCloudShadow > 0.5)
                {
                    float cloud = KyleCloudShadowFactor(input.positionWS.xz);
                    // Per-mat strength scales the global driver (usually leave at 1).
                    cloud = lerp(1.0, cloud, saturate(_CloudShadowStrength));
                    color *= cloud;
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------------------------
        // Feeds _CameraNormalsTexture for the M5 outline pass (Forward renderer prepass).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                // Same encoding as URP's DepthNormalsPass.hlsl (forward path, no oct packing).
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
