using KVH.KylePixelator;
using UnityEngine;

namespace KVH.Game.Lighting
{
    // day/night grade on key+fill. clear color is a CameraRig runtime override, never write the settings asset.
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]
    public sealed class LightingDirector : MonoBehaviour
    {
        public enum Mode
        {
            Manual = 0,
            Cycle = 1,
        }

        [Header("Profiles")]
        [SerializeField] LightingProfile dayProfile;
        [SerializeField] LightingProfile nightProfile;

        [Header("Mode")]
        [SerializeField] Mode mode = Mode.Cycle;
        [Tooltip("Manual mode only. Night when on, Day when off.")]
        [SerializeField] bool useNight;

        [Header("Time of day")]
        [Range(0f, 24f)]
        [SerializeField] float timeOfDayHours = 12f;
        [Min(1f)]
        [SerializeField] float dayLengthSeconds = 180f;
        [SerializeField] bool autoAdvance = true;
        [Tooltip("Hours of ease outside sunrise/sunset so DayAmount doesn't cliff to 0.")]
        [Min(0f)]
        [SerializeField] float twilightHours = 1f;

        [Header("Sun (key light)")]
        [Tooltip("Arcs key during day; night yaw sweeps dusk→dawn (continuous) at Sun Min Pitch.")]
        [SerializeField] bool rotateKeyWithTime = true;
        [SerializeField] float sunNoonYaw = 325f;
        [Tooltip("Modest sweep. Large values flip front walls through cel bands.")]
        [SerializeField] float sunYawSweep = 50f;
        [SerializeField] bool flipSunPath = true;
        [SerializeField] float sunNoonPitch = 42f;
        [Tooltip("Lowest key pitch, short shadows. Never grazing.")]
        [SerializeField] float sunMinPitch = 28f;
        [Range(0f, 12f)] [SerializeField] float sunriseHour = 6f;
        [Range(12f, 24f)] [SerializeField] float sunsetHour = 18f;

        [Header("Scene lights")]
        [SerializeField] Light keyLight;
        [SerializeField] Light fillLight;

        [Header("Pixel camera")]
        [SerializeField] CameraRig cameraRig;

        LightShadows _keyShadowMode = LightShadows.Soft;

        // water / scenery shaders read this for night grade (W4)
        static readonly int DayAmountId = Shader.PropertyToID("_KVH_DayAmount");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitDayGlobal() => Shader.SetGlobalFloat(DayAmountId, 1f);

        public float DayAmount
        {
            get
            {
                if (mode == Mode.Manual)
                    return useNight ? 0f : 1f;
                return SolarClock.DayAmount(timeOfDayHours, sunriseHour, sunsetHour, twilightHours);
            }
        }

        public float TimeOfDayHours
        {
            get => timeOfDayHours;
            set
            {
                timeOfDayHours = Mathf.Repeat(value, 24f);
                Apply();
            }
        }

        public void SetNight(bool night)
        {
            mode = Mode.Manual;
            if (useNight == night)
            {
                Apply();
                return;
            }
            useNight = night;
            Apply();
        }

        public void SetMode(Mode next)
        {
            mode = next;
            Apply();
        }

        public void Apply()
        {
            var dayAmt = DayAmount;
            Shader.SetGlobalFloat(DayAmountId, dayAmt);

            if (dayProfile == null && nightProfile == null)
                return;

            Color keyColor;
            float keyIntensity;
            float keyShadow;
            Color fillColor;
            float fillIntensity;
            Color sky;

            if (dayProfile != null && nightProfile != null)
            {
                keyColor = Color.Lerp(nightProfile.keyColor, dayProfile.keyColor, dayAmt);
                keyIntensity = Mathf.Lerp(nightProfile.keyIntensity, dayProfile.keyIntensity, dayAmt);
                keyShadow = Mathf.Lerp(nightProfile.keyShadowStrength, dayProfile.keyShadowStrength, dayAmt);
                fillColor = Color.Lerp(nightProfile.fillColor, dayProfile.fillColor, dayAmt);
                fillIntensity = Mathf.Lerp(nightProfile.fillIntensity, dayProfile.fillIntensity, dayAmt);
                sky = Color.Lerp(nightProfile.skyColor, dayProfile.skyColor, dayAmt);
            }
            else
            {
                var p = dayProfile != null ? dayProfile : nightProfile;
                keyColor = p.keyColor;
                keyIntensity = p.keyIntensity;
                keyShadow = p.keyShadowStrength;
                fillColor = p.fillColor;
                fillIntensity = p.fillIntensity;
                sky = p.skyColor;
            }

            if (keyLight != null)
            {
                if (keyLight.shadows != LightShadows.None)
                    _keyShadowMode = keyLight.shadows;

                keyLight.color = keyColor;
                keyLight.intensity = keyIntensity;
                keyLight.shadowStrength = keyShadow;
                keyLight.shadows = keyShadow > 0.001f ? _keyShadowMode : LightShadows.None;

                if (rotateKeyWithTime && mode == Mode.Cycle)
                {
                    SolarClock.KeyEuler(
                        timeOfDayHours, sunriseHour, sunsetHour,
                        sunNoonYaw, sunYawSweep, flipSunPath, sunNoonPitch, sunMinPitch,
                        out var pitch, out var yaw);
                    keyLight.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                }
            }

            if (fillLight != null)
            {
                fillLight.color = fillColor;
                fillLight.intensity = fillIntensity;
            }

            // runtime clear only, never mutate DefaultPixelSettings.skyColor
            if (cameraRig != null)
            {
                cameraRig.SetClearColor(sky);
                cameraRig.Apply();
            }
        }

        void OnEnable()
        {
            if (keyLight != null && keyLight.shadows != LightShadows.None)
                _keyShadowMode = keyLight.shadows;
            Apply();
        }

        void OnValidate()
        {
            timeOfDayHours = Mathf.Clamp(timeOfDayHours, 0f, 24f);
            dayLengthSeconds = Mathf.Max(1f, dayLengthSeconds);
            twilightHours = Mathf.Max(0f, twilightHours);
            sunMinPitch = Mathf.Clamp(sunMinPitch, 15f, 80f);
            Apply();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;
            if (mode != Mode.Cycle || !autoAdvance)
                return;

            timeOfDayHours = Mathf.Repeat(timeOfDayHours + 24f * (Time.deltaTime / dayLengthSeconds), 24f);
            Apply();
        }
    }
}
