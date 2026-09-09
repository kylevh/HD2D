// Shared wind noise + globals for billboard tufts and optional ToonLit ground shade.
#ifndef KVH_KYLEPIXELATOR_GRASS_WIND_INCLUDED
#define KVH_KYLEPIXELATOR_GRASS_WIND_INCLUDED

float4 _KylePixelator_GrassWindParams; // xy = dirXZ, z = strength, w = speed
float  _KylePixelator_GrassWindScale;
float  _KylePixelator_GrassWindActive;
float  _KylePixelator_GrassWindQuantize; // world-unit step; 0 = smooth sway
float  _KylePixelator_GrassWindGround;   // 0..1 shade amount on receiving ToonLit mats

float KyleGrassHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float KyleGrassValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = KyleGrassHash21(i);
    float b = KyleGrassHash21(i + float2(1, 0));
    float c = KyleGrassHash21(i + float2(0, 1));
    float d = KyleGrassHash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Same scrolled gust sample tufts use — keep ground + blades locked together.
void KyleGrassWindSample(float2 xz, out float2 windDir, out float signedNoise, out float unsignedNoise)
{
    windDir = _KylePixelator_GrassWindParams.xy;
    float dirLen = max(length(windDir), 1e-4);
    windDir /= dirLen;
    float windSpd = _KylePixelator_GrassWindParams.w;
    float windScl = max(_KylePixelator_GrassWindScale, 1e-3);
    float2 noiseUV = xz * windScl + windDir * (_Time.y * windSpd);
    unsignedNoise = KyleGrassValueNoise(noiseUV);
    signedNoise = unsignedNoise * 2.0 - 1.0;
}

float KyleGrassQuantizeSway(float sway)
{
    float stepSize = _KylePixelator_GrassWindQuantize;
    if (stepSize < 1e-5)
        return sway;
    // Pixel-art tick: snap displacement so tips hop between poses instead of melting.
    return floor(sway / stepSize + 0.5) * stepSize;
}

#endif
