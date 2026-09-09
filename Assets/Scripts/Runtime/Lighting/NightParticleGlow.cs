using UnityEngine;

namespace KVH.Game.Lighting
{
    // fades particle systems with the day clock. glow itself is additive pixels, not bloom / point lights.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-35)]
    [AddComponentMenu("KVH/Lighting/Night Particle Glow")]
    public sealed class NightParticleGlow : MonoBehaviour
    {
        [SerializeField] LightingDirector lightingDirector;
        [Tooltip("How much of the authored particle stays visible in full day.")]
        [Range(0f, 1f)] [SerializeField] float dayVisibility = 0.05f;

        ParticleSystem[] _systems;
        ParticleSystem.MinMaxCurve[] _baseRates;
        ParticleSystem.MinMaxGradient[] _baseColors;
        LightingDirector _director;
        float _lastDay = float.NaN;

        void OnEnable()
        {
            CacheBases();
            Apply(force: true);
        }

        void OnDisable()
        {
            RestoreBases();
            _lastDay = float.NaN;
        }

        void OnValidate()
        {
            dayVisibility = Mathf.Clamp01(dayVisibility);
            _lastDay = float.NaN;
            if (isActiveAndEnabled)
                Apply(force: true);
        }

        void LateUpdate()
        {
            Apply(force: false);
        }

        LightingDirector ResolvedDirector()
        {
            if (lightingDirector != null)
                return lightingDirector;
            if (_director == null)
                _director = FindAnyObjectByType<LightingDirector>();
            return _director;
        }

        void CacheBases()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            var n = _systems.Length;
            _baseRates = new ParticleSystem.MinMaxCurve[n];
            _baseColors = new ParticleSystem.MinMaxGradient[n];
            for (var i = 0; i < n; i++)
            {
                _baseRates[i] = _systems[i].emission.rateOverTime;
                _baseColors[i] = _systems[i].main.startColor;
            }
        }

        void RestoreBases()
        {
            if (_systems == null)
                return;
            for (var i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                if (ps == null)
                    continue;
                var emission = ps.emission;
                emission.rateOverTime = _baseRates[i];
                var main = ps.main;
                main.startColor = _baseColors[i];
            }
        }

        void Apply(bool force)
        {
            if (_systems == null || _systems.Length == 0)
                CacheBases();
            if (_systems == null || _systems.Length == 0)
                return;

            var director = ResolvedDirector();
            var day = director != null ? director.DayAmount : 1f;
            if (!force && Mathf.Approximately(day, _lastDay))
                return;
            _lastDay = day;

            var vis = Mathf.Lerp(dayVisibility, 1f, 1f - Mathf.Clamp01(day));
            for (var i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                if (ps == null)
                    continue;

                var emission = ps.emission;
                emission.rateOverTime = ScaleCurve(_baseRates[i], vis);

                var main = ps.main;
                main.startColor = ScaleGradient(_baseColors[i], vis);
            }
        }

        static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float vis)
        {
            curve.constant *= vis;
            curve.constantMin *= vis;
            curve.constantMax *= vis;
            return curve;
        }

        static ParticleSystem.MinMaxGradient ScaleGradient(ParticleSystem.MinMaxGradient g, float vis)
        {
            g.color = ScaleColor(g.color, vis);
            g.colorMin = ScaleColor(g.colorMin, vis);
            g.colorMax = ScaleColor(g.colorMax, vis);
            return g;
        }

        static Color ScaleColor(Color c, float vis)
        {
            c.a *= vis;
            return c;
        }
    }
}
