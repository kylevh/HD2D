using UnityEngine;

namespace KVH.KylePixelator
{
    // pushes ToonLit globals from Settings. kept out of LowResOutput so present stays thin.
    public static class CelLightingGlobals
    {
        const string CelOffKeyword = "_KYLEPIXELATOR_CEL_OFF";

        static readonly int LightBandsId = Shader.PropertyToID("_KylePixelator_LightBands");
        static readonly int BandSoftnessId = Shader.PropertyToID("_KylePixelator_BandSoftness");
        static readonly int LightWrapId = Shader.PropertyToID("_KylePixelator_LightWrap");
        static readonly int AdditionalLightsModeId = Shader.PropertyToID("_KylePixelator_AdditionalLightsMode");
        static readonly int PunctualAttenStepsId = Shader.PropertyToID("_KylePixelator_PunctualAttenSteps");

        public static void Apply(Settings s)
        {
            if (s == null)
                return;

            Shader.SetGlobalFloat(LightBandsId, Mathf.Clamp(s.lightBands, 2, 6));
            Shader.SetGlobalFloat(BandSoftnessId, Mathf.Max(0f, s.bandSoftness));
            Shader.SetGlobalFloat(LightWrapId, s.lightWrap);
            Shader.SetGlobalFloat(AdditionalLightsModeId, (float)s.additionalLightsMode);
            Shader.SetGlobalFloat(
                PunctualAttenStepsId,
                s.stepPunctualAttenuation ? Mathf.Clamp(s.punctualAttenSteps, 2, 8) : 0f);

            if (s.enableCelLighting)
                Shader.DisableKeyword(CelOffKeyword);
            else
                Shader.EnableKeyword(CelOffKeyword);
        }
    }
}
