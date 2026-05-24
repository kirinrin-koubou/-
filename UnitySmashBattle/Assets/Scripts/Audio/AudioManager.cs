using System.Collections.Generic;
using UnityEngine;

namespace SmashBattle
{
    /// <summary>
    /// Singleton audio dispatcher. Maps logical sound names to AudioClips assigned
    /// in the inspector and plays them via a pooled AudioSource.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [System.Serializable]
        public struct NamedClip
        {
            public string name;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        [Header("Clips")]
        [SerializeField] private NamedClip[] clips;

        [Header("Settings")]
        [SerializeField] private int voiceCount = 6;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField] private bool muted;

        private readonly Dictionary<string, NamedClip> _lookup = new Dictionary<string, NamedClip>();
        private AudioSource[] _sources;
        private int _nextSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _lookup.Clear();
            if (clips != null)
            {
                foreach (var c in clips)
                {
                    if (!string.IsNullOrEmpty(c.name))
                        _lookup[c.name] = c;
                }
            }

            _sources = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }
        }

        /// <summary>
        /// Plays the named sound (e.g. "jab", "smash", "ko", "shield", "shoot").
        /// Silently ignores unknown names.
        /// </summary>
        public void PlaySound(string name)
        {
            if (muted || string.IsNullOrEmpty(name)) return;
            if (!_lookup.TryGetValue(name, out var nc) || nc.clip == null) return;

            float vol = (nc.volume <= 0f ? 1f : nc.volume) * masterVolume;
            var src = NextSource();
            src.PlayOneShot(nc.clip, vol);
        }

        private AudioSource NextSource()
        {
            var s = _sources[_nextSource];
            _nextSource = (_nextSource + 1) % _sources.Length;
            return s;
        }

        /// <summary>Toggles all audio on or off.</summary>
        public void SetMuted(bool value) => muted = value;

        /// <summary>Sets master volume in the 0..1 range.</summary>
        public void SetMasterVolume(float v) => masterVolume = Mathf.Clamp01(v);
    }
}
