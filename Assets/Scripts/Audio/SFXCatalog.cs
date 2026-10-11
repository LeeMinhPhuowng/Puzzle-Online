using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleOnline.Audio
{
    [CreateAssetMenu(fileName = "SFXCatalog", menuName = "Puzzle Online/Audio/SFX Catalog", order = 10)]
    public class SFXCatalog : ScriptableObject
    {
        [Header("--- UI Sound Effects ---")]
        [Tooltip("Âm thanh khi bấm nút / click trong UI")]
        public AudioClip clickSound;
        [Range(0f, 1f)] public float clickVolume = 0.6f;

        [Tooltip("Âm thanh khi mở bảng thông báo / modal")]
        public AudioClip modalOpenSound;
        [Range(0f, 1f)] public float modalOpenVolume = 0.7f;

        [Tooltip("Âm thanh khi đóng bảng thông báo / modal")]
        public AudioClip modalCloseSound;
        [Range(0f, 1f)] public float modalCloseVolume = 0.6f;

        [Tooltip("Âm thanh cảnh báo lỗi (phòng đầy, sai mật khẩu, v.v.)")]
        public AudioClip errorSound;
        [Range(0f, 1f)] public float errorVolume = 0.8f;

        [Header("--- Puzzle Gameplay Sound Effects ---")]
        [Tooltip("Âm thanh khi nhặt mảnh ghép lên")]
        public AudioClip piecePickSound;
        [Range(0f, 1f)] public float piecePickVolume = 0.7f;

        [Tooltip("Âm thanh khi đặt mảnh ghép xuống mặt bàn")]
        public AudioClip pieceDropSound;
        [Range(0f, 1f)] public float pieceDropVolume = 0.6f;

        [Tooltip("Âm thanh khi xoay mảnh ghép (cuộn chuột)")]
        public AudioClip pieceRotateSound;
        [Range(0f, 1f)] public float pieceRotateVolume = 0.65f;

        [Tooltip("Âm thanh khi mảnh ghép khớp đúng vị trí (Snap)")]
        public AudioClip pieceSnapSound;
        [Tooltip("Danh sách âm thanh khi khớp mảnh (hệ thống sẽ phát ngẫu nhiên nếu có nhiều file)")]
        public List<AudioClip> pieceSnapClips = new List<AudioClip>();
        [Range(0f, 1f)] public float pieceSnapVolume = 0.9f;

        [Tooltip("Âm thanh ăn mừng khi hoàn thành toàn bộ bức tranh")]
        public AudioClip puzzleCompleteSound;
        [Range(0f, 1f)] public float puzzleCompleteVolume = 1.0f;

        [Tooltip("Âm thanh đếm ngược thời gian (Tick)")]
        public AudioClip timerTickSound;
        [Range(0f, 1f)] public float timerTickVolume = 0.5f;

        [Header("--- Music & Ambient Tracks ---")]
        [Tooltip("Nhạc nền giao diện Menu chính")]
        public AudioClip menuMusic;
        [Range(0f, 1f)] public float menuMusicVolume = 0.35f;

        [Tooltip("Âm thanh môi trường: Thiên nhiên (tiếng gió, chim hót)")]
        public AudioClip natureAmbience;

        [Tooltip("Âm thanh môi trường: Vườn hoa anh đào (gió thoảng, tiếng chuông gió)")]
        public AudioClip sakuraAmbience;

        [Tooltip("Âm thanh môi trường: Lớp học cổ điển (tiếng đồng hồ tích tắc nhẹ)")]
        public AudioClip classroomAmbience;

        [Header("--- Procedural Fallback Settings ---")]
        [Tooltip("Tự động sinh âm thanh dự phòng nếu chưa kéo file âm thanh vào Catalog")]
        public bool useProceduralFallbackIfMissing = true;

        // Bộ nhớ đệm các clip dự phòng được khởi tạo khi thiếu file
        [NonSerialized] private AudioClip _fallbackClick;
        [NonSerialized] private AudioClip _fallbackPick;
        [NonSerialized] private AudioClip _fallbackDrop;
        [NonSerialized] private AudioClip _fallbackRotate;
        [NonSerialized] private AudioClip _fallbackSnap;
        [NonSerialized] private AudioClip _fallbackComplete;

        public AudioClip GetClickClip()
        {
            if (clickSound != null) return clickSound;
            if (!useProceduralFallbackIfMissing) return null;
            if (_fallbackClick == null) _fallbackClick = CreateClickClip("Fallback_Click", 0.04f, 750f, clickVolume);
            return _fallbackClick;
        }

        public AudioClip GetPickClip()
        {
            if (piecePickSound != null) return piecePickSound;
            if (!useProceduralFallbackIfMissing) return null;
            if (_fallbackPick == null) _fallbackPick = CreateToneClip("Fallback_Pick", 0.08f, 320f, piecePickVolume, 1.2f);
            return _fallbackPick;
        }

        public AudioClip GetDropClip()
        {
            if (pieceDropSound != null) return pieceDropSound;
            if (!useProceduralFallbackIfMissing) return null;
            if (_fallbackDrop == null) _fallbackDrop = CreateToneClip("Fallback_Drop", 0.06f, 240f, pieceDropVolume, 2.0f);
            return _fallbackDrop;
        }

        public AudioClip GetRotateClip()
        {
            if (pieceRotateSound != null) return pieceRotateSound;
            if (!useProceduralFallbackIfMissing) return null;
            if (_fallbackRotate == null) _fallbackRotate = CreateClickClip("Fallback_Rotate", 0.05f, 540f, pieceRotateVolume);
            return _fallbackRotate;
        }

        public AudioClip GetSnapClip()
        {
            if (pieceSnapClips != null && pieceSnapClips.Count > 0)
            {
                var valid = pieceSnapClips.FindAll(c => c != null);
                if (valid.Count > 0)
                {
                    return valid[UnityEngine.Random.Range(0, valid.Count)];
                }
            }
            if (pieceSnapSound != null) return pieceSnapSound;
            if (!useProceduralFallbackIfMissing) return null;
            if (_fallbackSnap == null) _fallbackSnap = CreateChimeClip("Fallback_Snap", 0.28f, 660f, 880f, pieceSnapVolume);
            return _fallbackSnap;
        }

        public AudioClip GetCompleteClip()
        {
            if (puzzleCompleteSound != null) return puzzleCompleteSound;
            if (!useProceduralFallbackIfMissing) return null;
            if (_fallbackComplete == null) _fallbackComplete = CreateFanfareClip("Fallback_Complete", 1.8f);
            return _fallbackComplete;
        }

        // ==========================================
        // PROCEDURAL AUDIO SYNTHESIZERS (Fallback)
        // ==========================================
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
                float env = 1f - (t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * volume;
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
                float env = Mathf.Exp(-3.5f * t / duration);
                float s1 = Mathf.Sin(2f * Mathf.PI * freq1 * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * freq2 * t);
                samples[i] = (s1 + s2) * 0.5f * env * volume;
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

            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float seg = duration / freqs.Length;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                int idx = Mathf.Min((int)(t / seg), freqs.Length - 1);
                float localT = t - (idx * seg);
                float env = Mathf.Exp(-2.2f * localT / seg);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freqs[idx] * t) * env * 0.5f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
