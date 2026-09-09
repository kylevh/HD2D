Shader "Hidden/KVH/KylePixelatorSharpUpscale"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // x=1/w, y=1/h, z=w, w=h

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 texel = i.uv * _MainTex_TexelSize.zw;

                // t3ssel8r / clone-style: nearest inside texel, bilinear ~1 screen px at borders.
                float2 alpha = fwidth(texel);
                alpha = max(alpha, float2(1e-5, 1e-5));
                float2 box = clamp(alpha, 1e-5, 1.0);

                float2 tx = texel - 0.5 * box;
                float2 txOffset = smoothstep(1.0 - box, 1.0, frac(tx));
                float2 uv = (floor(tx) + 0.5 + txOffset) * _MainTex_TexelSize.xy;

                return tex2Dlod(_MainTex, float4(uv, 0, 0));
            }
            ENDCG
        }
    }
    FallBack Off
}
