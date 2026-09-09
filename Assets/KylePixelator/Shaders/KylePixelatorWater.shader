// Circular pond: radial depth + shore foam + Holland packed hop waves + float foam + skim/night.
// ZWrite On so OutlineFeature doesn't ink basin cliffs through the surface.
Shader "KVH/KylePixelatorWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.45, 0.68, 0.72, 0.92)
        _DeepColor ("Deep Color", Color) = (0.10, 0.14, 0.32, 0.96)
        _NightDeepColor ("Night Deep Color", Color) = (0.04, 0.06, 0.14, 0.98)
        _RimColor ("Rim Color", Color) = (0.75, 0.82, 0.78, 1)
        _FoamColor ("Foam Color", Color) = (0.92, 0.94, 0.90, 1)
        _WaveColor ("Wave Color", Color) = (0.85, 0.92, 0.95, 1)
        _SkimColor ("Skim Color", Color) = (0.78, 0.72, 0.52, 1)
        _WaveCells ("Wave Cells", 2D) = "gray" {}
        _PondCenter ("Pond Center WS", Vector) = (0, 0, 0, 0)
        _PondRadius ("Pond Radius", Float) = 4
        _DepthBands ("Depth Bands", Range(2, 8)) = 4
        _CenterDepthBoost ("Center Depth Boost", Range(0, 1)) = 1
        _RimWidth ("Rim Width", Range(0.02, 0.25)) = 0.06
        _FoamDistance ("Foam Distance", Range(0.05, 1.5)) = 0.35
        _FoamBands ("Foam Bands", Range(1, 4)) = 2
        _ShoreFoamWidth ("Shore Foam Width", Range(0.02, 0.3)) = 0.1
        [Toggle] _DebugFoam ("Debug Foam Mask", Float) = 0
        _WaveScale ("Wave Scale", Range(0.2, 4)) = 1.4
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.7
        _WaveWidth ("Wave Width", Range(0.02, 0.4)) = 0.14
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.45
        _FloatFoamScale ("Float Foam Scale", Range(0.3, 6)) = 2.2
        _FloatFoamStrength ("Float Foam Strength", Range(0, 1)) = 0.35
        _SkimStrength ("Skim Strength", Range(0, 1)) = 0.38
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
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGBA

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _KVH_DayAmount;

            TEXTURE2D(_WaveCells);
            SAMPLER(sampler_WaveCells);

            // outside UnityPerMaterial so PondSurface MPB can move the pond
            float4 _PondCenter;
            float _PondRadius;

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _NightDeepColor;
                half4 _RimColor;
                half4 _FoamColor;
                half4 _WaveColor;
                half4 _SkimColor;
                float4 _WaveCells_ST;
                float _DepthBands;
                float _CenterDepthBoost;
                float _RimWidth;
                float _FoamDistance;
                float _FoamBands;
                float _ShoreFoamWidth;
                float _DebugFoam;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveWidth;
                float _WaveStrength;
                float _FloatFoamScale;
                float _FloatFoamStrength;
                float _SkimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float DepthFoam01(float3 waterWS, float4 positionCS)
            {
                float2 uv = GetNormalizedScreenSpaceUV(positionCS);
                float raw = SampleSceneDepth(uv);
                if (raw >= 0.999999)
                    return 0.0;

                float3 sceneWS = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float gap = waterWS.y - sceneWS.y;
                float maxD = max(_FoamDistance, 1e-3);
                return saturate(1.0 - gap / maxD);
            }

            // Holland-ish: packed cell tex → view-flat hop dashes (world-locked).
            float HopWave01(float3 waterWS)
            {
                float cell = max(_WaveScale, 0.05);
                float2 uv = waterWS.xz / cell;
                float4 packed = SAMPLE_TEXTURE2D(_WaveCells, sampler_WaveCells, uv);
                float2 cellDir = packed.rg * 2.0 - 1.0;
                float cellDirLen = length(cellDir);
                cellDir = cellDirLen > 1e-4 ? cellDir / cellDirLen : float2(1, 0);

                float3 camFwd = unity_OrthoParams.w > 0.5
                    ? -UNITY_MATRIX_V[2].xyz
                    : normalize(_WorldSpaceCameraPos - waterWS);
                float2 flat = float2(camFwd.x, camFwd.z);
                float flatLen = length(flat);
                flat = flatLen > 1e-4 ? flat / flatLen : float2(1, 0);

                // only draw when cell dir is roughly horizontal vs view
                float align = abs(dot(cellDir, float2(-flat.y, flat.x)));
                if (align < 0.55)
                    return 0.0;

                float along = dot(waterWS.xz, flat);
                float local = frac(along / cell);
                float phase = packed.b;
                float dist = packed.a; // 1 at cell center → 0 at edge

                float pulse = sin(_TimeParameters.x * _WaveSpeed + phase * 6.2831) * 0.5 + 0.5;
                pulse = floor(pulse * 3.0 + 1e-4) / 3.0;
                if (pulse < 0.34)
                    return 0.0;

                float halfW = _WaveWidth * 0.5 * pulse * saturate(dist + 0.15);
                float waveLine = step(0.5 - halfW, local) * step(local, 0.5 + halfW);
                return waveLine * saturate(_WaveStrength) * saturate(dist * 1.4);
            }

            // chunky float foam islands that hop in/out
            float FloatFoam01(float3 waterWS, float u)
            {
                float scale = max(_FloatFoamScale, 0.2);
                float2 p = waterWS.xz / scale;
                float2 cell = floor(p);
                float2 f = frac(p) - 0.5;

                // cheap hash
                float n = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
                float n2 = frac(sin(dot(cell + 17.0, float2(269.5, 183.3))) * 43758.5453);
                if (n < 0.62)
                    return 0.0; // sparse

                float rad = 0.18 + n2 * 0.22;
                float d = length(f);
                float blob = 1.0 - step(rad, d);
                blob *= step(d, rad * 0.55) * 0.35 + step(rad * 0.55, d); // hard ring-ish

                float pulse = sin(_TimeParameters.x * (_WaveSpeed * 0.85) + n * 9.0) * 0.5 + 0.5;
                pulse = floor(pulse * 2.0 + 1e-4) / 2.0;
                if (pulse < 0.5)
                    return 0.0;

                // denser near shore
                float shoreBias = smoothstep(0.35, 0.95, u);
                return blob * saturate(_FloatFoamStrength) * lerp(0.35, 1.0, shoreBias);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 delta = input.positionWS.xz - _PondCenter.xz;
                float radius = max(_PondRadius, 1e-3);
                float u = length(delta) / radius;
                if (u > 1.02)
                    discard;

                float d = saturate(1.0 - u) * saturate(_CenterDepthBoost);

                float bands = max(round(_DepthBands), 2.0);
                float dq = floor(d * bands + 1e-4) / bands;

                float day = saturate(_KVH_DayAmount);
                half4 shallow = _ShallowColor;
                half4 deep = lerp(_NightDeepColor, _DeepColor, day);
                shallow.rgb = lerp(shallow.rgb * 0.45h, shallow.rgb, day);
                half4 col = lerp(shallow, deep, dq);

                float rim = step(1.0 - saturate(_RimWidth), u);
                col.rgb = lerp(col.rgb, _RimColor.rgb, rim * 0.4);

                float skimMask = saturate(1.0 - dq) * day * saturate(_SkimStrength);
                skimMask = floor(skimMask * 3.0 + 1e-4) / 3.0;

                float depthFoam = DepthFoam01(input.positionWS, input.positionCS);
                float shoreFoam = saturate((u - (1.0 - saturate(_ShoreFoamWidth))) / max(_ShoreFoamWidth, 1e-3));
                float floatFoam = FloatFoam01(input.positionWS, u);
                float foam = max(max(depthFoam, shoreFoam), floatFoam);
                float fb = max(round(_FoamBands), 1.0);
                foam = floor(foam * fb + 1e-4) / fb;

                if (_DebugFoam > 0.5)
                    return half4(foam, 0, 0, 1);

                col.rgb = lerp(col.rgb, _SkimColor.rgb, skimMask * (1.0 - foam));
                col.rgb = lerp(col.rgb, _FoamColor.rgb, foam);

                float wave = HopWave01(input.positionWS) * (1.0 - foam);
                col.rgb = lerp(col.rgb, _WaveColor.rgb, wave);

                col.a = 0.96h;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
