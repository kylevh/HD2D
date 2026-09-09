using UnityEngine;

namespace KVH.Game.Lighting
{
    // day factor + key euler from clock hours. no Light/Scene refs, LightingDirector binds it.
    public static class SolarClock
    {
        public static float DayAmount(float hours, float sunriseHour, float sunsetHour, float twilightHours = 0f)
        {
            hours = Mathf.Repeat(hours, 24f);
            var rise = sunriseHour;
            var set = Mathf.Max(sunsetHour, rise + 0.01f);
            var tw = Mathf.Max(0f, twilightHours);

            var dayStart = rise - tw;
            var dayEnd = set + tw;
            if (dayEnd - dayStart <= 0.01f)
                return 0f;

            float h = hours;
            // twilight can wrap past midnight, shift h so InverseLerp still works
            if (dayStart < 0f && h > dayEnd)
                h -= 24f;
            else if (dayEnd > 24f && h < rise)
                h += 24f;

            if (h < dayStart || h > dayEnd)
                return 0f;

            var dayT = Mathf.InverseLerp(dayStart, dayEnd, h);
            return Mathf.Sin(dayT * Mathf.PI);
        }

        public static void KeyEuler(
            float hours,
            float sunriseHour,
            float sunsetHour,
            float noonYaw,
            float yawSweep,
            bool flipSunPath,
            float noonPitch,
            float minPitch,
            out float pitch,
            out float yaw)
        {
            hours = Mathf.Repeat(hours, 24f);
            var rise = sunriseHour;
            var set = Mathf.Max(sunsetHour, rise + 0.01f);
            minPitch = Mathf.Clamp(minPitch, 15f, noonPitch);

            var duskYaw = noonYaw + (flipSunPath ? -yawSweep : yawSweep);
            var dawnYaw = noonYaw + (flipSunPath ? yawSweep : -yawSweep);

            if (hours >= rise && hours <= set)
            {
                var dayT = Mathf.InverseLerp(rise, set, hours);
                var elev = Mathf.Sin(dayT * Mathf.PI);
                pitch = Mathf.Lerp(minPitch, noonPitch, elev);

                var yawT = flipSunPath ? 1f - dayT : dayT;
                yaw = noonYaw + Mathf.Lerp(-yawSweep, yawSweep, yawT);
            }
            else
            {
                pitch = minPitch;
                var nh = hours < rise ? hours + 24f : hours;
                var nightT = Mathf.InverseLerp(set, rise + 24f, nh);
                yaw = Mathf.LerpAngle(duskYaw, dawnYaw, nightT);
            }
        }
    }
}
