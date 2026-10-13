using System.Collections;
using UnityEngine;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Manages atmospheric audio for horror immersion:
    /// - Deep spaceship ventilation drone / ambient hum
    /// - Spontaneous distant metallic clangs and hull groans
    /// - Stalker / alien attack warning cues and sirens
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class AudioAmbienceController : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _ambientHumSource;
        [SerializeField] private AudioSource _metallicGroanSource;
        [SerializeField] private AudioSource _alarmWarningSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip _ambientHumClip;
        [SerializeField] private AudioClip[] _distantMetallicClips;
        [SerializeField] private AudioClip _attackWarningClip;

        [Header("Metallic Sounds Timing")]
        [SerializeField] private bool _enableRandomMetallicSounds = true;
        [SerializeField] private float _minGroanInterval = 6f;
        [SerializeField] private float _maxGroanInterval = 14f;

        [Header("Volume Sliders")]
        [Range(0f, 1f)] [SerializeField] private float _ambientVolume = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float _groanVolume = 0.65f;
        [Range(0f, 1f)] [SerializeField] private float _warningVolume = 0.85f;

        private Coroutine _groanRoutine;

        public bool IsAlarmActive { get; private set; }

        private void Awake()
        {
            SetupSources();
        }

        private void Start()
        {
            StartAmbience();
            if (_enableRandomMetallicSounds)
            {
                _groanRoutine = StartCoroutine(RandomMetallicSoundsLoop());
            }
        }

        private void OnDisable()
        {
            if (_groanRoutine != null)
            {
                StopCoroutine(_groanRoutine);
                _groanRoutine = null;
            }
        }

        private void SetupSources()
        {
            if (_ambientHumSource == null)
            {
                _ambientHumSource = gameObject.AddComponent<AudioSource>();
            }
            if (_metallicGroanSource == null)
            {
                _metallicGroanSource = gameObject.AddComponent<AudioSource>();
            }
            if (_alarmWarningSource == null)
            {
                _alarmWarningSource = gameObject.AddComponent<AudioSource>();
            }

            _ambientHumSource.loop = true;
            _ambientHumSource.playOnAwake = false;
            _ambientHumSource.volume = _ambientVolume;

            _metallicGroanSource.loop = false;
            _metallicGroanSource.playOnAwake = false;
            _metallicGroanSource.volume = _groanVolume;

            _alarmWarningSource.loop = true;
            _alarmWarningSource.playOnAwake = false;
            _alarmWarningSource.volume = _warningVolume;
        }

        public void StartAmbience()
        {
            if (_ambientHumClip != null && _ambientHumSource != null)
            {
                _ambientHumSource.clip = _ambientHumClip;
                _ambientHumSource.Play();
            }
        }

        public void StopAmbience()
        {
            if (_ambientHumSource != null && _ambientHumSource.isPlaying)
            {
                _ambientHumSource.Stop();
            }
        }

        public void TriggerDistantMetallicGroan()
        {
            if (_distantMetallicClips != null && _distantMetallicClips.Length > 0 && _metallicGroanSource != null)
            {
                int index = Random.Range(0, _distantMetallicClips.Length);
                AudioClip clip = _distantMetallicClips[index];
                if (clip != null)
                {
                    _metallicGroanSource.pitch = Random.Range(0.85f, 1.15f);
                    _metallicGroanSource.PlayOneShot(clip, _groanVolume);
                }
            }
        }

        public void TriggerAttackWarning(bool active)
        {
            IsAlarmActive = active;
            if (_alarmWarningSource == null) return;

            if (active)
            {
                if (_attackWarningClip != null)
                {
                    _alarmWarningSource.clip = _attackWarningClip;
                    if (!_alarmWarningSource.isPlaying)
                    {
                        _alarmWarningSource.Play();
                    }
                }
            }
            else
            {
                if (_alarmWarningSource.isPlaying)
                {
                    _alarmWarningSource.Stop();
                }
            }
        }

        private IEnumerator RandomMetallicSoundsLoop()
        {
            while (true)
            {
                float delay = Random.Range(_minGroanInterval, _maxGroanInterval);
                yield return new WaitForSeconds(delay);
                TriggerDistantMetallicGroan();
            }
        }
    }
}
