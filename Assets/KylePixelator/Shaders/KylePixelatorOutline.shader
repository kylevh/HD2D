// 1px outlines on the low-res RT (pixel camera only, before sharp present).
//
// Pass 0 "Mask":      depth + normals → RGBA line mask (rgb = ink colour, a = weight).
// Pass 1 "Composite": lerp(color, mask.rgb, mask.a).
//
// Silhouette: eye-depth cliffs vs 4 cardinals, angle-adaptive threshold, farther-side only.
// Crease: brighten form edges (Leng/KYRIOTA); silenced on silhouettes / far occlusion side.
// Depth debug = binary ink mask (not fog). Knobs arrive as shader globals.
Shader "Hidden/KVH/KylePixelatorOutline"
{
    Properties
    {
        // Declared so Material.Set* binds through URP (undeclared ints were stuck at 0 on GPU).
        _KylePixelatorOutlineSilhouetteStepTexels ("Step Texels", Float) = 2.5
        _OutlineSilhouetteSide ("Side", Float) = 1
        _KylePixelatorOutlineLineDarken ("Line Darken", Float) = 0.25
        _KylePixelatorOutlineCreaseLow ("Crease Low", Float) = 0.08
        _KylePixelatorOutlineCreaseHigh ("Crease High", Float) = 0.3
        _KylePixelatorOutlineCreaseBrighten ("Crease Brighten", Float) = 0.35
        _KylePixelatorOutlineEnableCreases ("Enable Creases", Float) = 1
        _OutlineDebugMode ("Debug Mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // globals. do NOT put these in UnityPerMaterial (Blit.hlsl already owns that cbuffer).
        float _KylePixelatorOutlineSilhouetteStepTexels;
        float _OutlineSilhouetteSide;
        float _KylePixelatorOutlineLineDarken;
        float _KylePixelatorOutlineCreaseLow;
        float _KylePixelatorOutlineCreaseHigh;
        float _KylePixelatorOutlineCreaseBrighten;
        float _KylePixelatorOutlineEnableCreases;
        float _OutlineDebugMode;
        ENDHLSL

        // ------------------------------------------------------------------ Mask
        Pass
        {
            Name "Mask"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMask
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            #define DBG_COMPOSITE   0
            #define DBG_COLOR_ONLY  1
            #define DBG_SILHOUETTE  2
            #define DBG_DEPTH       3

            // Larger = farther from camera (ortho + perspective).
            float EyeDepth(float raw)
            {
                if (IsPerspectiveProjection())
                    return LinearEyeDepth(raw, _ZBufferParams);
                return LinearDepthToEyeDepth(raw);
            }

            float3 NormalWS(float2 uv)
            {
                float3 n = SampleSceneNormals(uv);
                float len = length(n);
                return len > 1e-4 ? n / len : float3(0, 1, 0);
            }

            float4 FragMask(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texel = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);

                float eyeC = EyeDepth(SampleSceneDepth(uv));
                float3 nC = NormalWS(uv);
                float3 nV = mul((float3x3)UNITY_MATRIX_V, nC);
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                float texelWorld = IsPerspectiveProjection()
                    ? 2.0 * eyeC / max(UNITY_MATRIX_P._m11 * _ScreenParams.y, 1e-4)
                    : 2.0 * unity_OrthoParams.y / _ScreenParams.y;
                texelWorld = max(texelWorld, 1e-5);

                // Angle-adaptive threshold (Roystan / Leng): grazing faces need more slack.
                float facing = 1.0 - nV.z;
                float t01 = saturate((facing - 0.5) / 0.5);
                float thr = max(_KylePixelatorOutlineSilhouetteStepTexels, 1e-3) * (t01 * 2.0 + 1.0);

                // Cardinals only for silhouettes (Otavio / KYRIOTA). Diagonals in an 8-tap
                // kernel thicken rotated-box stairs into a 2px band.
                static const float2 offs[4] =
                {
                    float2(0, -1), float2(-1, 0), float2(1, 0), float2(0, 1)
                };

                float silhouette = 0.0;
                float maxFarther = 0.0;
                float closestEye = eyeC;
                float2 closestUv = uv;
                float invDepthSum = 0.0;
                float creaseMax = 0.0;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 suv = uv + offs[i] * texel;
                    float e = EyeDepth(SampleSceneDepth(suv));
                    float farther = (eyeC - e) / texelWorld;
                    maxFarther = max(maxFarther, farther);

                    if (farther > thr)
                        silhouette = 1.0;

                    if (e < closestEye)
                    {
                        closestEye = e;
                        closestUv = suv;
                    }

                    if (_KylePixelatorOutlineEnableCreases > 0.5)
                    {
                        float3 nN = NormalWS(suv);
                        float3 nNV = mul((float3x3)UNITY_MATRIX_V, nN);
                        invDepthSum += saturate(farther);
                        creaseMax = max(creaseMax, 1.0 - saturate(dot(nV, nNV)));
                    }
                }

                float3 inkSrc = (_OutlineSilhouetteSide < 0.5)
                    ? color.rgb
                    : SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, closestUv).rgb;
                float3 silhouetteRgb = inkSrc * _KylePixelatorOutlineLineDarken;

                // creases lift (Leng / KYRIOTA). same darken as silhouettes made front faces
                // read as double black rims (top crease + outer silhouette).
                float creaseW = 0.0;
                float3 creaseRgb = saturate(color.rgb * (1.0 + _KylePixelatorOutlineCreaseBrighten));
                if (_KylePixelatorOutlineEnableCreases > 0.5)
                {
                    float farSide = saturate(invDepthSum * 0.25);
                    creaseW = smoothstep(_KylePixelatorOutlineCreaseLow, _KylePixelatorOutlineCreaseHigh, creaseMax);
                    creaseW *= (1.0 - silhouette) * (1.0 - farSide);
                }

                int dbg = (int)round(_OutlineDebugMode);
                if (dbg == DBG_COLOR_ONLY)
                    return float4(color.rgb, 0.0);
                if (dbg == DBG_SILHOUETTE)
                    return float4(silhouette, silhouette * 0.15, silhouette * 0.15, 1.0);
                if (dbg == DBG_DEPTH)
                {
                    // exact ink mask in grayscale (same gate as composite). soft edge-strength
                    // used to show pulsing gray 2px bands along rotated diagonals, which was misleading.
                    return float4(silhouette, silhouette, silhouette, 1.0);
                }

                float4 mask = float4(creaseRgb, creaseW);
                if (silhouette > 0.0)
                    mask = float4(silhouetteRgb, 1.0);

                return mask;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------- Composite
        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma target 3.0

            TEXTURE2D_X(_KylePixelatorOutlineMaskTex);

            float4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                float4 mask  = SAMPLE_TEXTURE2D_X(_KylePixelatorOutlineMaskTex, sampler_PointClamp, uv);
                return float4(lerp(color.rgb, mask.rgb, mask.a), color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
