using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EcclesiaFighters
{
    /// <summary>
    /// Persistent (DontDestroyOnLoad) centralized audio manager using build-safe Resources AudioLibrary.
    /// Maintains 3 dedicated channels (BGM, SFX, Announcer Voice) with real-time Master * Category * clipScale
    /// volume updates driven by Configurações and Pause Menu sliders.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        static AudioManager instance;
        static bool         quitting;

        public static AudioManager Instance
        {
            get
            {
                if (quitting) return null;
                if (!instance && Application.isPlaying)
                    Bootstrap();
                return instance;
            }
        }

        public static bool HasInstance => instance != null && !quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (quitting || instance != null) return;
            var existing = FindObjectOfType<AudioManager>();
            if (existing)
            {
                instance = existing;
                DontDestroyOnLoad(existing.gameObject);
                existing.InitializeIfNeeded();
                return;
            }

            var go = new GameObject("EcclesiaAudioManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioManager>();
        }

        [Header("Library (Auto-loaded from Resources/EcclesiaAudioLibrary)")]
        [SerializeField] AudioLibrary library;

        AudioSource bgmSource;
        AudioSource sfxSource;
        AudioSource voiceSource;

        float masterVol = 1f;
        float musicVol  = 1f;
        float sfxVol    = 1f;
        float voiceVol  = 1f;

        float currentBgmScale   = 1f;
        float currentVoiceScale = 1f;
        float bgmDuckMultiplier = 1f;

        bool      initialized;
        Coroutine duckRoutine;

        int   lastUiSfxFrame   = -1;
        int   lastUiPriority   = 0;
        float lastHoverTime    = -10f;

        // ══════════════════════════════════════════════════════
        //  Lifecycle & Setup
        // ══════════════════════════════════════════════════════
        void Awake()
        {
            quitting = false;
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeIfNeeded();
        }

        void Start()
        {
            SilenceLegacySceneBgmSources();
            RefreshVolumesFromPrefs();
            RouteSceneMusic(SceneManager.GetActiveScene().name);
        }

        void OnApplicationQuit() => quitting = true;

        void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }

        public void InitializeIfNeeded()
        {
            if (initialized) return;
            initialized = true;

            if (!library)
                library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);

            EnsureChannels();
            RefreshVolumesFromPrefs();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void AssignLibrary(AudioLibrary lib)
        {
            if (lib != null) library = lib;
            if (Application.isPlaying)
                RouteSceneMusic(SceneManager.GetActiveScene().name);
        }

        void EnsureChannels()
        {
            if (bgmSource == null)   bgmSource   = CreateChannel("BGM_Channel",   loop: true);
            if (sfxSource == null)   sfxSource   = CreateChannel("SFX_Channel",   loop: false);
            if (voiceSource == null) voiceSource = CreateChannel("Voice_Channel", loop: false);
        }

        AudioSource CreateChannel(string childName, bool loop)
        {
            Transform existing = transform.Find(childName);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
                go = new GameObject(childName);

            go.transform.SetParent(transform, false);

            AudioSource src = go.GetComponent<AudioSource>();
            if (src == null)
                src = go.AddComponent<AudioSource>();

            src.playOnAwake  = false;
            src.loop         = loop;
            src.spatialBlend = 0f;
            return src;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SilenceLegacySceneBgmSources();
            RefreshVolumesFromPrefs();
            RouteSceneMusic(scene.name);
        }

        /// <summary>
        /// Stops and disables any legacy standalone BGM AudioSource components left in scenes
        /// so tracks never double-play or apply conflicting scene attenuation.
        /// </summary>
        public void SilenceLegacySceneBgmSources()
        {
            foreach (AudioSource src in FindObjectsOfType<AudioSource>(true))
            {
                if (!src || src.transform.IsChildOf(transform)) continue;
                if (src.gameObject.name == "BGM" || src.loop)
                {
                    src.Stop();
                    src.playOnAwake = false;
                    src.enabled     = false;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        //  Volume Calculation (Master * Category * clipScale)
        // ══════════════════════════════════════════════════════
        public void RefreshVolumesFromPrefs()
        {
            float m = PlayerPrefs.GetFloat(SettingsStore.KMaster, 1f);
            float b = PlayerPrefs.GetFloat(SettingsStore.KMusic,  1f);
            float s = PlayerPrefs.GetFloat(SettingsStore.KSfx,    1f);
            float v = PlayerPrefs.GetFloat(SettingsStore.KVoice,  1f);
            SetVolumes(m, b, s, v);
        }

        public void SetVolumes(float master, float music, float sfx, float voice)
        {
            masterVol = Mathf.Clamp01(master);
            musicVol  = Mathf.Clamp01(music);
            sfxVol    = Mathf.Clamp01(sfx);
            voiceVol  = Mathf.Clamp01(voice);
            RecalculateSourceVolumes();
        }

        void RecalculateSourceVolumes()
        {
            EnsureChannels();
            AudioListener.volume = 1f;

            if (bgmSource)   bgmSource.volume   = masterVol * musicVol  * currentBgmScale   * bgmDuckMultiplier;
            if (sfxSource)   sfxSource.volume   = masterVol * sfxVol;
            if (voiceSource) voiceSource.volume = masterVol * voiceVol  * currentVoiceScale;
        }

        // ══════════════════════════════════════════════════════
        //  Scene Music Routing & Ducking
        // ══════════════════════════════════════════════════════
        public void RouteSceneMusic(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (sceneName == "TitleScreen")
            {
                PlayTitleBGMInternal();
            }
            else if (sceneName == "CharSelect" || sceneName == "MapSelect")
            {
                // Seamless continuation across CharSelect <-> MapSelect without restarting
                PlayCharSelectBGMInternal();
            }
            else if (sceneName == "FightScene")
            {
                string map = PlayerPrefs.GetString("SelectedMap", "Exterior");
                int stageIdx = (map == "Interior" || map == "1") ? 1 : 0;
                PlayStageBGMInternal(stageIdx);
            }
        }

        public void PlayBGM(AudioCue cue, bool restartIfSame = false)
        {
            if (!cue.IsValid) return;
            EnsureChannels();

            if (duckRoutine != null)
            {
                StopCoroutine(duckRoutine);
                duckRoutine = null;
            }
            bgmDuckMultiplier = 1f;
            currentBgmScale   = cue.EffectiveScale;
            RecalculateSourceVolumes();

            if (!restartIfSame && bgmSource.isPlaying && bgmSource.clip == cue.clip)
                return;

            bgmSource.clip = cue.clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public void DuckBGM(float duckedMultiplier, float fadeDownDur = 0.18f, float holdDur = -1f, float fadeUpDur = 0.45f)
        {
            if (!bgmSource || !bgmSource.isPlaying) return;
            if (duckRoutine != null) StopCoroutine(duckRoutine);
            duckRoutine = StartCoroutine(DuckRoutine(Mathf.Clamp01(duckedMultiplier), fadeDownDur, holdDur, fadeUpDur));
        }

        IEnumerator DuckRoutine(float targetMul, float fadeDownDur, float holdDur, float fadeUpDur)
        {
            float startMul = bgmDuckMultiplier;
            float t = 0f;
            float downDur = Mathf.Max(0.01f, fadeDownDur);
            while (t < downDur)
            {
                t += Time.unscaledDeltaTime;
                bgmDuckMultiplier = Mathf.Lerp(startMul, targetMul, t / downDur);
                RecalculateSourceVolumes();
                yield return null;
            }
            bgmDuckMultiplier = targetMul;
            RecalculateSourceVolumes();

            if (holdDur < 0f)
            {
                duckRoutine = null;
                yield break;
            }

            if (holdDur > 0f)
                yield return new WaitForSecondsRealtime(holdDur);

            t = 0f;
            float upDur = Mathf.Max(0.01f, fadeUpDur);
            while (t < upDur)
            {
                t += Time.unscaledDeltaTime;
                bgmDuckMultiplier = Mathf.Lerp(targetMul, 1f, t / upDur);
                RecalculateSourceVolumes();
                yield return null;
            }
            bgmDuckMultiplier = 1f;
            RecalculateSourceVolumes();
            duckRoutine = null;
        }

        // ══════════════════════════════════════════════════════
        //  Channel Playback Primitives
        // ══════════════════════════════════════════════════════
        AudioLibrary GetLib()
        {
            if (!library)
                library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            return library;
        }

        void PlayTitleBGMInternal()
        {
            var lib = GetLib();
            if (lib != null) PlayBGM(lib.bgmTitleScreen, restartIfSame: false);
        }

        void PlayCharSelectBGMInternal()
        {
            var lib = GetLib();
            if (lib != null) PlayBGM(lib.bgmCharSelect, restartIfSame: false);
        }

        void PlayStageBGMInternal(int stageIndex)
        {
            var lib = GetLib();
            if (lib == null) return;
            AudioCue cue = stageIndex == 1 ? lib.battleTheme2 : lib.battleTheme1;
            PlayBGM(cue, restartIfSame: true);
        }

        void PlaySFXInternal(AudioCue cue)
        {
            if (!cue.IsValid) return;
            EnsureChannels();
            RecalculateSourceVolumes();
            sfxSource.PlayOneShot(cue.clip, cue.EffectiveScale);
        }

        void PlayUISfxPrioritized(AudioCue cue, int priority)
        {
            if (!cue.IsValid) return;
            int frame = Time.frameCount;
            if (lastUiSfxFrame == frame && priority <= lastUiPriority)
                return;

            lastUiSfxFrame = frame;
            lastUiPriority = priority;
            PlaySFXInternal(cue);
        }

        void PlayVoiceInternal(AudioCue cue)
        {
            if (!cue.IsValid) return;
            EnsureChannels();
            currentVoiceScale = cue.EffectiveScale;
            RecalculateSourceVolumes();
            voiceSource.Stop();
            voiceSource.clip = cue.clip;
            voiceSource.Play();
        }

        // ══════════════════════════════════════════════════════
        //  Public Static Convenience API
        // ══════════════════════════════════════════════════════
        // ── Music ─────────────────────────────────────────────
        public static void PlayTitleBGM()               { var am = Instance; if (am != null) am.PlayTitleBGMInternal(); }
        public static void PlayCharSelectBGM()          { var am = Instance; if (am != null) am.PlayCharSelectBGMInternal(); }
        public static void PlayStageBGM(int stageIndex) { var am = Instance; if (am != null) am.PlayStageBGMInternal(stageIndex); }

        // ── UI SFX ────────────────────────────────────────────
        public static void PlayUIHover()
        {
            var am = Instance;
            AudioLibrary lib = am != null ? am.GetLib() : null;
            if (am == null || lib == null) return;
            if (Time.unscaledTime - am.lastHoverTime < 0.035f) return;
            am.lastHoverTime = Time.unscaledTime;
            am.PlaySFXInternal(lib.sfxMenuHover);
        }

        public static void PlayUIClick()
        {
            var am = Instance;
            AudioLibrary lib = am != null ? am.GetLib() : null;
            if (am != null && lib != null) am.PlayUISfxPrioritized(lib.sfxMenuClick, priority: 1);
        }

        public static void PlayUIConfirm()
        {
            var am = Instance;
            AudioLibrary lib = am != null ? am.GetLib() : null;
            if (am != null && lib != null) am.PlayUISfxPrioritized(lib.sfxMenuConfirm, priority: 2);
        }

        public static void PlayUIBack()
        {
            var am = Instance;
            AudioLibrary lib = am != null ? am.GetLib() : null;
            if (am != null && lib != null) am.PlayUISfxPrioritized(lib.sfxMenuBack, priority: 2);
        }

        // ── Combat & Movement SFX ─────────────────────────────
        public static void PlayPunchLight()    { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxPunchLight); }
        public static void PlayPunchHeavy()    { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxPunchHeavy); }
        public static void PlayHitLight()      { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxHitLight); }
        public static void PlayHitHeavy()      { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxHitHeavy); }
        public static void PlayBlock()         { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxBlock); }
        public static void PlaySpecialLaunch() { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxSpecialLaunch); }
        public static void PlaySpecialImpact() { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxSpecialImpact); }
        public static void PlayJump()          { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxJump); }
        public static void PlayLand()          { var am = Instance; AudioLibrary l = am != null ? am.GetLib() : null; if (am != null && l != null) am.PlaySFXInternal(l.sfxLand); }

        public static void PlaySpecialReadyCue()
        {
            var am = Instance;
            if (am == null) return;
            AudioLibrary l = am.GetLib();
            if (l == null) return;
            AudioCue cue = l.sfxMenuConfirm.IsValid ? l.sfxMenuConfirm : l.sfxMenuClick;
            if (!cue.IsValid) return;
            am.EnsureChannels();
            am.RecalculateSourceVolumes();
            if (am.sfxSource != null)
                am.sfxSource.PlayOneShot(cue.clip, cue.EffectiveScale * 0.35f);
        }

        // ── Match Flow & Announcer Voices ─────────────────────
        public static void PlayRoundIntroVoice(int roundNumber, bool isFinalRound)
        {
            var am = Instance;
            AudioLibrary lib = am != null ? am.GetLib() : null;
            if (am == null || lib == null) return;

            AudioCue cue;
            if (isFinalRound || roundNumber >= 3) cue = lib.voFinalRound;
            else if (roundNumber == 2)            cue = lib.voRound2;
            else                                  cue = lib.voRound1;

            am.PlayVoiceInternal(cue);
        }

        public static void PlayFightVoice()
        {
            var am = Instance;
            AudioLibrary lib = am != null ? am.GetLib() : null;
            if (am != null && lib != null) am.PlayVoiceInternal(lib.voFight);
        }

        public static void PlayKO()
        {
            var am = Instance;
            AudioLibrary lib = am != null ? am.GetLib() : null;
            if (am == null || lib == null) return;

            am.DuckBGM(0.35f, fadeDownDur: 0.14f, holdDur: 2.30f, fadeUpDur: 0.55f);
            am.PlaySFXInternal(lib.sfxBellKo);
            am.PlayVoiceInternal(lib.voKo);
        }

        public static void PlayMatchEnd(bool victory)
        {
            var am = Instance;
            AudioLibrary lib = am != null ? am.GetLib() : null;
            if (am == null || lib == null) return;

            am.DuckBGM(0.22f, fadeDownDur: 0.25f, holdDur: -1f);
            am.PlaySFXInternal(victory ? lib.sfxVictory : lib.sfxDefeat);
            am.PlayVoiceInternal(victory ? lib.voVictory : lib.voDefeat);
        }
    }
}
