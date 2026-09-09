// Shared scrolling cloud shade for ToonLit ground + grass blades (not sun-tied).
#ifndef KVH_KYLEPIXELATOR_CLOUD_SHADOW_INCLUDED
#define KVH_KYLEPIXELATOR_CLOUD_SHADOW_INCLUDED

float4 _KylePixelator_CloudShadowParams; // xy = dirXZ, z = speed, w = scale
float  _KylePixelator_CloudShadowStrength; // 0..1 how hard clouds crush lit color
float  _KylePixelator_CloudShadowActive;
float  _KylePixelator_CloudShadowQuantize; // 0 = smooth; else chunky bands for low-res

float KyleCloudHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float KyleCloudValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = KyleCloudHash21(i);
    float b = KyleCloudHash21(i + float2(1, 0));
    float c = KyleCloudHash21(i + float2(0, 1));
    float d = KyleCloudHash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 1 = full sun, lower = under cloud. Large soft blobs for outdoor read at 640×360.
float KyleCloudShadowFactor(float2 xz)
{
    if (_KylePixelator_CloudShadowActive < 0.5 || _KylePixelator_CloudShadowStrength < 1e-4)
        return 1.0;

    float2 dir = _KylePixelator_CloudShadowParams.xy;
    float dirLen = max(length(dir), 1e-4);
    dir /= dirLen;
    float spd = _KylePixelator_CloudShadowParams.z;
    float scl = max(_KylePixelator_CloudShadowParams.w, 1e-3);

    // Two octaves — big sheets + a little breakup (still cheap).
    float2 uv0 = xz * scl + dir * (_Time.y * spd);
    float2 uv1 = xz * (scl * 2.3) + float2(-dir.y, dir.x) * (_Time.y * spd * 0.55);
    float n = KyleCloudValueNoise(uv0) * 0.65 + KyleCloudValueNoise(uv1) * 0.35;

    float q = _KylePixelator_CloudShadowQuantize;
    if (q > 1e-4)
        n = floor(n / q + 0.5) * q;

    // Remap so most of the field stays lit; only the low hump darkens.
    float cloud = smoothstep(0.35, 0.72, n);
    float shade = lerp(1.0, lerp(0.55, 1.0, cloud), _KylePixelator_CloudShadowStrength);
    return shade;
}

#endif
