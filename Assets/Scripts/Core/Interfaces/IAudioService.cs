using UnityEngine;

namespace PuzzleOnline.Core
{
    /// <summary>
    /// Giao diện dịch vụ âm thanh (Audio Service Contract) cho toàn bộ trò chơi.
    /// Giúp tách rời (decouple) logic âm thanh khỏi triển khai cụ thể của AudioManager.
    /// </summary>
    public interface IAudioService
    {
        float MasterVolume { get; }
        float SFXVolume { get; }
        float MusicVolume { get; }
        float VoiceVolume { get; }

        void PlayClick();
        void PlaySnap();
        void PlayPick();
        void PlayDrop();
        void PlayRotate();
        void PlayComplete();
        void PlayError();
        void PlayMusic(AudioClip clip, bool loop = true);
        void StopMusic();
        void SetMasterVolume(float vol);
        void SetSFXVolume(float vol);
        void SetMusicVolume(float vol);
        void SetVoiceVolume(float vol);
    }
}
