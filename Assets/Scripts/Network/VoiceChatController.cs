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

        public bool IsTransmitting { get; private set; }
        public bool IsAvailable => Microphone.devices.Length > 0;

        public void Initialize(RealtimeClient network)
        {
            _network = network;
            _speaker = gameObject.AddComponent<AudioSource>();
            _speaker.playOnAwake = false;
            _speaker.spatialBlend = 0f;
        }

        public void SetTransmitting(bool value)
        {
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

        public void Receive(string base64Pcm)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64Pcm);
                if (bytes.Length < 2) return;
                var samples = new float[bytes.Length / 2];
                for (var i = 0; i < samples.Length; i++)
                    samples[i] = (short)(bytes[i * 2] | bytes[i * 2 + 1] << 8) / 32768f;
                var clip = AudioClip.Create("VoicePacket", samples.Length, 1, SampleRate, false);
                clip.SetData(samples, 0);
                _speaker.PlayOneShot(clip);
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
                var value = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
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
