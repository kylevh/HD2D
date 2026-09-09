// 1px interactable silhouette on the low-res RT (after artistic OutlineFeature, before present).
//
// Pass 0 "Mask":      DrawRenderer solid coverage (feature draws with NO scene depth).
// Pass 1 "Composite": blit — 4-neighbor ring (cardinals only, same as KylePixelatorOutline).
//
// X-ray select: full silhouette even when the player/walls sit in front. Feature must not
// bind camera depth on the mask pass — depth binding punched holes despite ZTest Always.
//
// Color/fill are SHADER GLOBALS only (SetGlobal*). Do not put them in Properties —
// Blit.hlsl owns UnityPerMaterial on Composite, so material floats would stick at 0.
Shader "Hidden/KVH/KylePixelatorHighlightOutline"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Blend Off

        // ------------------------------------------------------------------ Mask (mesh override)
        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragMask
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Empty UnityPerMaterial keeps SRP Batcher / BRG happy on override draws.
            CBUFFER_START(UnityPerMaterial)
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings VertMask(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 FragMask(Varyings input) : SV_Target
            {
                return 1.0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------- Composite (fullscreen)
        Pass
        {
            Name "Composite"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_KylePixelatorHighlightMaskTex);
            // globals — not UnityPerMaterial (Blit owns that cbuffer)
            float4 _KylePixelatorHighlightColor;
            float _KylePixelatorHighlightFill;

            float4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                float inside = SAMPLE_TEXTURE2D_X(_KylePixelatorHighlightMaskTex, sampler_PointClamp, uv).r;

                float2 texel = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                static const float2 offs[4] =
                {
                    float2(0, -1), float2(-1, 0), float2(1, 0), float2(0, 1)
                };

                float dilated = inside;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 suv = uv + offs[i] * texel;
                    dilated = max(dilated, SAMPLE_TEXTURE2D_X(_KylePixelatorHighlightMaskTex, sampler_PointClamp, suv).r);
                }

                // ring sits *outside* the object so it doesn't fight the artistic 1px ink
                float ring = dilated * (1.0 - inside);
                float3 rgb = lerp(color.rgb, _KylePixelatorHighlightColor.rgb, inside * _KylePixelatorHighlightFill);
                rgb = lerp(rgb, _KylePixelatorHighlightColor.rgb, ring);
                return float4(rgb, color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
