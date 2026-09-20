using System;
using UnityEngine;

namespace PuzzleOnline.Audio
{
    public sealed class PuzzleAudioService : MonoBehaviour
    {
        public static PuzzleAudioService Instance { get; private set; }

        private AudioSource _sfxSource;
        private AudioSource _musicSource;

        private AudioClip _pickClip;
        private AudioClip _rotateClip;
        private AudioClip _snapClip;
        private AudioClip _completeClip;
        private AudioClip _clickClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.volume = 0.35f;

            GenerateProceduralClips();
        }

        private void GenerateProceduralClips()
        {
            _pickClip = CreateToneClip("PickSFX", 0.08f, 320f, 0.4f, 1.2f);
            _rotateClip = CreateClickClip("RotateSFX", 0.05f, 540f, 0.3f);
            _snapClip = CreateChimeClip("SnapSFX", 0.28f, 660f, 880f, 0.6f);
            _completeClip = CreateFanfareClip("VictorySFX", 1.8f);
            _clickClip = CreateClickClip("ClickSFX", 0.04f, 750f, 0.25f);
        }

        [Header("Settings")]
        [Tooltip("Tạm thời tắt toàn bộ SFX theo yêu cầu")]
        public bool enableSfx = false;

        public void PlayPick()
        {
            if (!enableSfx) return;
            if (_sfxSource != null && _pickClip != null)
                _sfxSource.PlayOneShot(_pickClip, 0.7f);
        }

        public void PlayRotate()
        {
            if (!enableSfx) return;
            if (_sfxSource != null && _rotateClip != null)
                _sfxSource.PlayOneShot(_rotateClip, 0.6f);
        }

        public void PlaySnap()
        {
            if (!enableSfx) return;
            if (_sfxSource != null && _snapClip != null)
                _sfxSource.PlayOneShot(_snapClip, 0.9f);
        }

        public void PlayComplete()
        {
            if (!enableSfx) return;
            if (_sfxSource != null && _completeClip != null)
                _sfxSource.PlayOneShot(_completeClip, 1.0f);
        }

        public void PlayClick()
        {
            if (!enableSfx) return;
            if (_sfxSource != null && _clickClip != null)
                _sfxSource.PlayOneShot(_clickClip, 0.5f);
        }

        // Helpers to synthesize clear sound waves
        private static AudioClip CreateToneClip(string name, float duration, float freq, float volume, float decay)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-decay * t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateClickClip(string name, float duration, float freq, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float env = 1f - (i / (float)sampleCount);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * (UnityEngine.Random.value * 2f - 1f)) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateChimeClip(string name, float duration, float freq1, float freq2, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-4.5f * t);
                float wave1 = Mathf.Sin(2f * Mathf.PI * freq1 * t);
                float wave2 = Mathf.Sin(2f * Mathf.PI * freq2 * t);
                samples[i] = (wave1 * 0.6f + wave2 * 0.4f) * env * volume;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateFanfareClip(string name, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
            float noteDuration = duration / notes.Length;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                int noteIdx = Mathf.Clamp(Mathf.FloorToInt(t / noteDuration), 0, notes.Length - 1);
                float noteT = t - (noteIdx * noteDuration);
                float env = Mathf.Exp(-3.0f * (noteT / noteDuration));
                float wave = Mathf.Sin(2f * Mathf.PI * notes[noteIdx] * t);
                samples[i] = wave * env * 0.5f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
