using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using PuzzleOnline.Core;

namespace PuzzleOnline.Audio
{
    /// <summary>
    /// Quản lý toàn bộ âm thanh trò chơi (Sound Effects, Background Music, Ambience, Audio Mixer).
    /// Hỗ trợ AudioMixer Groups chuyên biệt và kết nối trực tiếp với SFXCatalog.
    /// Triển khai IAudioService để hỗ trợ Dependency Injection.
    /// </summary>
    public class AudioManager : MonoBehaviour, IAudioService
    {
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<AudioManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[AudioManager]");
                        _instance = go.AddComponent<AudioManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("SFX Catalog")]
        [Tooltip("Catalog chứa danh mục các file âm thanh. Kéo thả file SFXCatalog.asset vào đây.")]
        [SerializeField] private SFXCatalog catalog;

        [Header("Audio Mixer Integration (Tùy chọn)")]
        [Tooltip("Audio Mixer chính của trò chơi (nếu có)")]
        [SerializeField] private AudioMixer audioMixer;

        [Tooltip("Nhóm Mixer cho âm thanh tổng")]
        [SerializeField] private AudioMixerGroup masterGroup;

        [Tooltip("Nhóm Mixer cho nhạc nền và âm thanh môi trường")]
        [SerializeField] private AudioMixerGroup musicGroup;

        [Tooltip("Nhóm Mixer cho các hiệu ứng âm thanh (SFX)")]
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Tooltip("Nhóm Mixer cho Voice Chat")]
        [SerializeField] private AudioMixerGroup voiceGroup;

        [Header("Settings")]
        [Tooltip("Bật hoặc tắt toàn bộ SFX")]
        public bool sfxEnabled = true;

        [Tooltip("Bật hoặc tắt nhạc nền")]
        public bool musicEnabled = true;

        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float voiceVolume = 1.0f;

        private AudioSource _musicSource;
        private AudioSource _ambienceSource;
        private readonly List<AudioSource> _sfxPool = new List<AudioSource>();
        private const int SFX_POOL_SIZE = 8;
        private int _sfxPoolIndex = 0;

        public SFXCatalog Catalog => catalog;
        public AudioMixerGroup VoiceGroup => voiceGroup;
        public float MasterVolume => masterVolume;
        public float SFXVolume => sfxVolume;
        public float MusicVolume => musicVolume;
        public float VoiceVolume => voiceVolume;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            ServiceLocator.Register<IAudioService>(this);

            masterVolume = PlayerPrefs.GetFloat("Vol_Master", masterVolume);
            sfxVolume = PlayerPrefs.GetFloat("Vol_SFX", sfxVolume);
            musicVolume = PlayerPrefs.GetFloat("Vol_Music", musicVolume);
            voiceVolume = PlayerPrefs.GetFloat("Vol_Voice", voiceVolume);

            InitializeCatalog();
            InitializeAudioSources();
            EnsureAudioListener();
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                ServiceLocator.Unregister<IAudioService>();
                _instance = null;
            }
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            PlayBGMForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            EnsureAudioListener();
            PlayBGMForScene(scene.name);
        }

        private void EnsureAudioListener()
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                if (cam != null && cam.GetComponent<AudioListener>() == null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                }
                else if (gameObject.GetComponent<AudioListener>() == null)
                {
                    gameObject.AddComponent<AudioListener>();
                }
            }
        }

        public void PlayBGMForScene(string sceneName)
        {
            if (catalog == null) return;
            if (sceneName == "SCN_Menu" || sceneName == "SCN_MainMenu")
            {
                StopAmbience();
                if (catalog.menuMusic != null)
                {
                    PlayMusic(catalog.menuMusic, true);
                }
            }
            else if (sceneName == "SCN_Nature")
            {
                StopMusic();
                if (catalog.natureAmbience != null)
                {
                    PlayAmbience(catalog.natureAmbience);
                }
            }
            else if (sceneName == "SCN_Sakura")
            {
                StopMusic();
                if (catalog.sakuraAmbience != null)
                {
                    PlayAmbience(catalog.sakuraAmbience);
                }
            }
            else if (sceneName == "SCN_Classroom")
            {
                StopMusic();
                if (catalog.classroomAmbience != null)
                {
                    PlayAmbience(catalog.classroomAmbience);
                }
            }
        }

        private void InitializeCatalog()
        {
            if (catalog == null)
            {
                catalog = Resources.Load<SFXCatalog>("SFXCatalog");
            }
#if UNITY_EDITOR
            if (catalog == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SFXCatalog");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<SFXCatalog>(path);
                }
            }
#endif
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SFXCatalog>();
                Debug.LogWarning("[AudioManager] Không tìm thấy file SFXCatalog trong Resources, tạo catalog tạm.");
            }
            else
            {
                Debug.Log($"<color=green><b>[AudioManager] Đã nạp thành công SFXCatalog: {catalog.name} (Pick: {(catalog.piecePickSound != null ? catalog.piecePickSound.name : "none")}, Snap: {(catalog.pieceSnapSound != null ? catalog.pieceSnapSound.name : "none")})</b></color>");
            }
        }

        private void InitializeAudioSources()
        {
            // 1. Music AudioSource
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
            if (musicGroup != null) _musicSource.outputAudioMixerGroup = musicGroup;

            // 2. Ambience AudioSource
            _ambienceSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource.playOnAwake = false;
            _ambienceSource.loop = true;
            _ambienceSource.spatialBlend = 0f;
            if (musicGroup != null) _ambienceSource.outputAudioMixerGroup = musicGroup;

            // 3. SFX AudioSource Pool (đa kênh để âm thanh không bị ngắt quãng khi phát dồn dập)
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                var sfxSrc = gameObject.AddComponent<AudioSource>();
                sfxSrc.playOnAwake = false;
                sfxSrc.spatialBlend = 0f;
                if (sfxGroup != null) sfxSrc.outputAudioMixerGroup = sfxGroup;
                _sfxPool.Add(sfxSrc);
            }
        }

        // ==========================================
        // VOLUME CONTROL (AudioMixer & Direct Fallback)
        // ==========================================
        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Vol_Master", masterVolume);
            ApplyMixerVolume("MasterVolume", masterVolume);
        }

        public void SetSFXVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Vol_SFX", sfxVolume);
            ApplyMixerVolume("SFXVolume", sfxVolume);
            foreach (var s in _sfxPool)
            {
                if (s != null) s.volume = sfxVolume;
            }
        }

        public void SetMusicVolume(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Vol_Music", musicVolume);
            ApplyMixerVolume("MusicVolume", musicVolume);
            if (_musicSource != null) _musicSource.volume = musicVolume;
            if (_ambienceSource != null) _ambienceSource.volume = musicVolume;
        }

        public void SetVoiceVolume(float vol)
        {
            voiceVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Vol_Voice", voiceVolume);
            ApplyMixerVolume("VoiceVolume", voiceVolume);
            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance != null && PuzzleOnline.Network.PuzzleNetworkManager.Instance.Voice != null)
            {
                PuzzleOnline.Network.PuzzleNetworkManager.Instance.Voice.Volume = voiceVolume;
            }
        }

        private void ApplyVolumes()
        {
            SetMasterVolume(masterVolume);
            SetSFXVolume(sfxVolume);
            SetMusicVolume(musicVolume);
            SetVoiceVolume(voiceVolume);
        }

        private readonly HashSet<string> _missingMixerParams = new HashSet<string>();

        private void ApplyMixerVolume(string paramName, float linearVol)
        {
            if (audioMixer == null || _missingMixerParams.Contains(paramName)) return;

            // Kiểm tra parameter có tồn tại trên Mixer hay không trước khi gọi SetFloat để tránh warning của Unity
            if (!audioMixer.GetFloat(paramName, out _))
            {
                _missingMixerParams.Add(paramName);
                return;
            }

            // Chuyển đổi tuyến tính 0..1 sang Decibels (-80dB..0dB)
            float db = linearVol > 0.0001f ? Mathf.Log10(linearVol) * 20f : -80f;
            audioMixer.SetFloat(paramName, db);
        }

        // ==========================================
        // SFX PLAYBACK METHODS
        // ==========================================
        private AudioSource GetNextSFXSource()
        {
            if (_sfxPool.Count == 0) return null;
            var src = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Count;
            return src;
        }

        public void PlayOneShot(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (!sfxEnabled || clip == null) return;
            var src = GetNextSFXSource();
            if (src != null)
            {
                src.pitch = pitch;
                src.PlayOneShot(clip, volumeScale * sfxVolume);
            }
        }

        public void PlayClick()
        {
            if (catalog == null) return;
            AudioClip clip = catalog.GetClickClip();
            PlayOneShot(clip, catalog.clickVolume, UnityEngine.Random.Range(0.95f, 1.05f));
        }

        public void PlayPick()
        {
            if (catalog == null) return;
            AudioClip clip = catalog.GetPickClip();
            PlayOneShot(clip, catalog.piecePickVolume, UnityEngine.Random.Range(0.96f, 1.04f));
        }

        public void PlayDrop()
        {
            if (catalog == null) return;
            AudioClip clip = catalog.GetDropClip();
            PlayOneShot(clip, catalog.pieceDropVolume, UnityEngine.Random.Range(0.92f, 1.0f));
        }

        public void PlayRotate()
        {
            if (catalog == null) return;
            AudioClip clip = catalog.GetRotateClip();
            PlayOneShot(clip, catalog.pieceRotateVolume, UnityEngine.Random.Range(0.98f, 1.06f));
        }

        public void PlaySnap()
        {
            if (catalog == null) return;
            AudioClip clip = catalog.GetSnapClip();
            PlayOneShot(clip, catalog.pieceSnapVolume, 1.0f);
        }

        public void PlayComplete()
        {
            if (catalog == null) return;
            AudioClip clip = catalog.GetCompleteClip();
            PlayOneShot(clip, catalog.puzzleCompleteVolume, 1.0f);
        }

        public void PlayError()
        {
            if (catalog != null && catalog.errorSound != null)
            {
                PlayOneShot(catalog.errorSound, catalog.errorVolume, 1.0f);
            }
            else
            {
                PlayClick();
            }
        }

        // ==========================================
        // MUSIC & AMBIENCE
        // ==========================================
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (!musicEnabled || clip == null || _musicSource == null) return;
            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = musicVolume;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource != null) _musicSource.Stop();
        }

        public void PlayAmbience(AudioClip clip)
        {
            if (!musicEnabled || clip == null || _ambienceSource == null) return;
            _ambienceSource.clip = clip;
            _ambienceSource.volume = musicVolume * 0.75f;
            _ambienceSource.Play();
        }

        public void StopAmbience()
        {
            if (_ambienceSource != null) _ambienceSource.Stop();
        }
    }
}
