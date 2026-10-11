using System;
using UnityEngine;

namespace PuzzleOnline.Network
{
    public sealed class VoiceChatController : MonoBehaviour
    {
        private const int SampleRate = 16000;
        private RealtimeClient _network;
        private AudioClip _capture;
        private AudioSource _speaker;
        private int _lastPosition;
        private float _nextSend;

        private float _volume = 1f;
        private float _micGain = 2.5f;
        private float _outputBoost = 2.2f;

        public bool IsTransmitting { get; private set; }
        public bool IsAvailable => Microphone.devices.Length > 0;
        public bool IsMuted { get; set; } = false;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                if (_speaker != null)
                {
                    _speaker.volume = _volume;
                }
            }
        }

        public float MicGain
        {
            get => _micGain;
            set
            {
                _micGain = Mathf.Clamp(value, 0.5f, 4.0f);
                PlayerPrefs.SetFloat("Voice_MicGain", _micGain);
            }
        }

        public float OutputBoost
        {
            get => _outputBoost;
            set => _outputBoost = Mathf.Clamp(value, 0.5f, 4.0f);
        }

        private void Awake()
        {
            _micGain = PlayerPrefs.GetFloat("Voice_MicGain", 2.5f);
        }

        public void Initialize(RealtimeClient network)
        {
            _network = network;
            _speaker = gameObject.AddComponent<AudioSource>();
            _speaker.playOnAwake = false;
            _speaker.spatialBlend = 0f;

            if (PuzzleOnline.Audio.AudioManager.Instance != null)
            {
                _volume = PuzzleOnline.Audio.AudioManager.Instance.VoiceVolume;
                if (PuzzleOnline.Audio.AudioManager.Instance.VoiceGroup != null)
                {
                    _speaker.outputAudioMixerGroup = PuzzleOnline.Audio.AudioManager.Instance.VoiceGroup;
                }
            }
            _speaker.volume = _volume;
        }

        public void SetTransmitting(bool value)
        {
            if (IsMuted && value) return;
            if (value == IsTransmitting) return;
            if (value && IsAvailable)
            {
                _capture = Microphone.Start(null, true, 1, SampleRate);
                _lastPosition = 0;
                IsTransmitting = true;
            }
            else
            {
                if (Microphone.IsRecording(null)) Microphone.End(null);
                _capture = null;
                IsTransmitting = false;
            }
        }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            if (IsMuted && IsTransmitting)
            {
                SetTransmitting(false);
            }
        }

        public void Receive(string base64Pcm)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64Pcm);
                if (bytes.Length < 2) return;
                var samples = new float[bytes.Length / 2];
                float effectiveGain = _volume * _outputBoost;
                for (var i = 0; i < samples.Length; i++)
                {
                    float raw = (short)(bytes[i * 2] | bytes[i * 2 + 1] << 8) / 32768f;
                    samples[i] = Mathf.Clamp(raw * effectiveGain, -1f, 1f);
                }
                var clip = AudioClip.Create("VoicePacket", samples.Length, 1, SampleRate, false);
                clip.SetData(samples, 0);
                if (_speaker != null)
                {
                    _speaker.volume = 1f;
                    _speaker.PlayOneShot(clip);
                }
                Destroy(clip, Mathf.Max(1f, samples.Length / (float)SampleRate + .2f));
            }
            catch (FormatException) { }
        }

        private void Update()
        {
            if (!IsTransmitting || _capture == null || _network == null || !_network.IsConnected) return;
            if (Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + .10f;
            var position = Microphone.GetPosition(null);
            if (position < 0 || position == _lastPosition) return;
            var totalSamples = _capture.samples;
            var count = position > _lastPosition ? position - _lastPosition : totalSamples - _lastPosition + position;
            count = Mathf.Min(count, SampleRate / 4);
            if (count <= 0) return;

            var samples = new float[count];
            _capture.GetData(samples, _lastPosition);
            _lastPosition = (_lastPosition + count) % totalSamples;
            var bytes = new byte[count * 2];
            for (var i = 0; i < count; i++)
            {
                float amplified = Mathf.Clamp(samples[i] * _micGain, -1f, 1f);
                var value = (short)Mathf.Clamp(Mathf.RoundToInt(amplified * 32767f), short.MinValue, short.MaxValue);
                bytes[i * 2] = (byte)(value & 0xff);
                bytes[i * 2 + 1] = (byte)((value >> 8) & 0xff);
            }
            _network.Send("VOICE", ("pcm", Convert.ToBase64String(bytes)));
        }

        private void OnDisable()
        {
            SetTransmitting(false);
        }
    }
}
