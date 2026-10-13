using System.Collections;
using UnityEngine;

namespace OblivionsEdge.Environment
{
    public enum LightingMode
    {
        DarkHorror,
        RedEmergencyStrobe,
        BlueVioletDeathStorm,
        FlickeringBulb
    }

    /// <summary>
    /// Drives dynamic sci-fi horror lighting effects:
    /// - Oppressive dark ambience
    /// - Red emergency alert pulsating beacons
    /// - Blue-violet mystical resonance for the DeathStorm forge and core
    /// - Flickering dying ballast lamps
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class AtmosphericLighting : MonoBehaviour
    {
        [Header("Lighting Targets")]
        [SerializeField] private Light _targetLight;
        [SerializeField] private SpriteRenderer _glowSprite;

        [Header("Mode & Colors")]
        [SerializeField] private LightingMode _activeMode = LightingMode.DarkHorror;
        [SerializeField] private Color _darkHorrorColor = new Color(0.12f, 0.16f, 0.22f, 1f);
        [SerializeField] private Color _redEmergencyColor = new Color(1.0f, 0.08f, 0.05f, 1f);
        [SerializeField] private Color _deathStormVioletColor = new Color(0.55f, 0.15f, 0.95f, 1f);

        [Header("Pulsing Configuration")]
        [SerializeField] private float _pulseFrequency = 2.5f;
        [SerializeField] private float _minIntensity = 0.2f;
        [SerializeField] private float _maxIntensity = 1.6f;

        [Header("Flicker Randomness")]
        [SerializeField] private float _flickerChance = 0.35f;
        [SerializeField] private float _flickerSpeed = 0.08f;

        private float _timeAccumulator;
        private Coroutine _flickerRoutine;

        public LightingMode ActiveMode
        {
            get => _activeMode;
            set
            {
                _activeMode = value;
                ApplyModeConfiguration();
            }
        }

        public Color CurrentColor { get; private set; }
        public float CurrentIntensity { get; private set; }

        private void Awake()
        {
            if (_targetLight == null)
            {
                _targetLight = GetComponent<Light>();
            }
            if (_glowSprite == null)
            {
                _glowSprite = GetComponent<SpriteRenderer>();
            }
            ApplyModeConfiguration();
        }

        private void OnEnable()
        {
            if (_activeMode == LightingMode.FlickeringBulb)
            {
                _flickerRoutine = StartCoroutine(FlickerLoop());
            }
        }

        private void OnDisable()
        {
            if (_flickerRoutine != null)
            {
                StopCoroutine(_flickerRoutine);
                _flickerRoutine = null;
            }
        }

        private void Update()
        {
            _timeAccumulator += Time.deltaTime;

            switch (_activeMode)
            {
                case LightingMode.DarkHorror:
                    UpdateDarkHorror();
                    break;

                case LightingMode.RedEmergencyStrobe:
                    UpdateEmergencyStrobe();
                    break;

                case LightingMode.BlueVioletDeathStorm:
                    UpdateDeathStormGlow();
                    break;

                case LightingMode.FlickeringBulb:
                    // Handled in coroutine
                    break;
            }
        }

        private void ApplyModeConfiguration()
        {
            if (_flickerRoutine != null)
            {
                StopCoroutine(_flickerRoutine);
                _flickerRoutine = null;
            }

            if (_activeMode == LightingMode.FlickeringBulb && isActiveAndEnabled)
            {
                _flickerRoutine = StartCoroutine(FlickerLoop());
            }
        }

        private void UpdateDarkHorror()
        {
            // Low, oppressive breathing intensity
            float t = (Mathf.Sin(_timeAccumulator * 0.8f) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(_minIntensity, _minIntensity * 1.5f, t);
            ApplyLightAndGlow(_darkHorrorColor, intensity);
        }

        private void UpdateEmergencyStrobe()
        {
            // Sharp red emergency pulse
            float wave = (Mathf.Sin(_timeAccumulator * _pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(_minIntensity * 0.5f, _maxIntensity, wave);
            ApplyLightAndGlow(_redEmergencyColor, intensity);
        }

        private void UpdateDeathStormGlow()
        {
            // Mystical ethereal blue-violet oscillation
            float wave = (Mathf.Sin(_timeAccumulator * 1.8f) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(_minIntensity * 0.8f, _maxIntensity * 1.2f, wave);
            ApplyLightAndGlow(_deathStormVioletColor, intensity);
        }

        private IEnumerator FlickerLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(_flickerSpeed);

                bool isDark = Random.value < _flickerChance;
                float intensity = isDark ? 0.05f : Random.Range(_minIntensity, _maxIntensity);
                Color c = Color.Lerp(_darkHorrorColor, Color.white, 0.4f);
                ApplyLightAndGlow(c, intensity);
            }
        }

        private void ApplyLightAndGlow(Color color, float intensity)
        {
            CurrentColor = color;
            CurrentIntensity = intensity;

            if (_targetLight != null)
            {
                _targetLight.color = color;
                _targetLight.intensity = intensity;
            }

            if (_glowSprite != null)
            {
                Color sc = color;
                sc.a = Mathf.Clamp01(intensity / _maxIntensity);
                _glowSprite.color = sc;
            }
        }
    }
}
