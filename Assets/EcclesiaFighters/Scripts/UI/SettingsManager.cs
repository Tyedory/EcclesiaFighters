using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace EcclesiaFighters
{
    /// <summary>
    /// Settings panel logic. Controls edit *pending* values (audio is previewed live);
    /// APLICAR commits + saves to PlayerPrefs, RESET restores defaults (still pending until APLICAR),
    /// VOLTAR (Discard) reverts to the last applied values.
    /// Saved settings are also applied automatically on startup / every scene load (see SettingsStartup).
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        [Header("Graphics")]
        public Button[] qualityButtons;
        public Toggle   vsyncToggle;
        public Toggle   fullscreenToggle;

        [Header("Resolution")]
        public Button prevResolutionButton;
        public Button nextResolutionButton;
        public Text   resolutionLabel;

        [Header("Volume")]
        public AudioMixer audioMixer;       // optional: exposed params Vol_Master / Vol_Music / Vol_SFX / Vol_Voice (dB)
        public Slider masterSlider, musicSlider, sfxSlider, voiceSlider;

        // pending (edited) values
        int   pQuality, pRes;
        bool  pVsync, pFull;
        float pMaster = 1f, pMusic = 1f, pSfx = 1f, pVoice = 1f;

        List<Vector2Int> resolutions;
        bool refreshing;

        // ─────────────────────────── lifecycle ───────────────────────────
        void OnEnable()
        {
            BuildResolutionList();
            LoadAppliedIntoPending();
            RefreshUI();
            PreviewAudio();
        }

        // ─────────────────────────── public UI callbacks ───────────────────────────
        public void SetQuality(int v)     { pQuality = Mathf.Clamp(v, 0, QualitySettings.names.Length - 1); RefreshUI(); }
        public void SetVSync(bool v)      { if (!refreshing) pVsync = v; }
        public void SetFullscreen(bool v) { if (!refreshing) pFull  = v; }

        public void NextResolution() { CycleResolution(1); }
        public void PrevResolution() { CycleResolution(-1); }

        public void SetMasterVol(float v) { if (refreshing) return; pMaster = v; PreviewAudio(); }
        public void SetMusicVol(float v)  { if (refreshing) return; pMusic  = v; PreviewAudio(); }
        public void SetSFXVol(float v)    { if (refreshing) return; pSfx    = v; PreviewAudio(); }
        public void SetVoiceVol(float v)  { if (refreshing) return; pVoice  = v; PreviewAudio(); }

        /// <summary>APLICAR — commit everything and save.</summary>
        public void Apply()
        {
            QualitySettings.SetQualityLevel(pQuality, true);
            QualitySettings.vSyncCount = pVsync ? 1 : 0;      // after quality: presets override vSync

            Vector2Int r = CurrentRes();
            FullScreenMode mode = pFull ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(r.x, r.y, mode);

            PlayerPrefs.SetInt(SettingsStore.KQuality, pQuality);
            PlayerPrefs.SetInt(SettingsStore.KVSync,   pVsync ? 1 : 0);
            PlayerPrefs.SetInt(SettingsStore.KFull,    pFull ? 1 : 0);
            PlayerPrefs.SetInt(SettingsStore.KResW,    r.x);
            PlayerPrefs.SetInt(SettingsStore.KResH,    r.y);
            PlayerPrefs.SetFloat(SettingsStore.KMaster, pMaster);
            PlayerPrefs.SetFloat(SettingsStore.KMusic,  pMusic);
            PlayerPrefs.SetFloat(SettingsStore.KSfx,    pSfx);
            PlayerPrefs.SetFloat(SettingsStore.KVoice,  pVoice);
            PlayerPrefs.Save();

            PreviewAudio();
            RefreshUI();
        }

        /// <summary>RESET — restore defaults into the widgets (commit with APLICAR).</summary>
        public void ResetToDefaults()
        {
            pQuality = SettingsStore.DefaultQuality();
            pVsync   = true;
            pFull    = true;
            pRes     = NativeResIndex();
            pMaster = pMusic = pSfx = pVoice = 1f;
            RefreshUI();
            PreviewAudio();
        }

        /// <summary>VOLTAR — drop unapplied changes (the Back button then closes the panel).</summary>
        public void Discard()
        {
            LoadAppliedIntoPending();
            RefreshUI();
            PreviewAudio();
        }

        /// <summary>Kept for compatibility with older scenes.</summary>
        public void SaveAndClose() { Apply(); gameObject.SetActive(false); }

        // ─────────────────────────── internals ───────────────────────────
        void BuildResolutionList()
        {
            resolutions = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)          // ascending; duplicates differ only by refresh rate
            {
                var v = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(v)) resolutions.Add(v);
            }
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
        }

        Vector2Int CurrentRes()
        {
            if (resolutions == null || resolutions.Count == 0) BuildResolutionList();
            return resolutions[Mathf.Clamp(pRes, 0, resolutions.Count - 1)];
        }

        int IndexOfRes(int w, int h)
        {
            for (int i = 0; i < resolutions.Count; i++)
                if (resolutions[i].x == w && resolutions[i].y == h) return i;
            return -1;
        }

        int NativeResIndex()
        {
            int i = IndexOfRes(Display.main.systemWidth, Display.main.systemHeight);
            return i >= 0 ? i : resolutions.Count - 1;
        }

        void CycleResolution(int dir)
        {
            if (resolutions == null || resolutions.Count == 0) return;
            pRes = (pRes + dir + resolutions.Count) % resolutions.Count;
            UpdateResolutionLabel();
        }

        void UpdateResolutionLabel()
        {
            if (!resolutionLabel) return;
            Vector2Int r = CurrentRes();
            resolutionLabel.text = r.x + " x " + r.y;
        }

        void LoadAppliedIntoPending()
        {
            pQuality = PlayerPrefs.HasKey(SettingsStore.KQuality) ? PlayerPrefs.GetInt(SettingsStore.KQuality) : QualitySettings.GetQualityLevel();
            pQuality = Mathf.Clamp(pQuality, 0, QualitySettings.names.Length - 1);
            pVsync   = PlayerPrefs.HasKey(SettingsStore.KVSync) ? PlayerPrefs.GetInt(SettingsStore.KVSync) == 1 : QualitySettings.vSyncCount > 0;
            pFull    = PlayerPrefs.HasKey(SettingsStore.KFull)  ? PlayerPrefs.GetInt(SettingsStore.KFull)  == 1 : Screen.fullScreen;

            int w = PlayerPrefs.GetInt(SettingsStore.KResW, Screen.width);
            int h = PlayerPrefs.GetInt(SettingsStore.KResH, Screen.height);
            pRes = IndexOfRes(w, h);
            if (pRes < 0) pRes = IndexOfRes(Screen.width, Screen.height);
            if (pRes < 0) pRes = resolutions.Count - 1;

            pMaster = PlayerPrefs.GetFloat(SettingsStore.KMaster, 1f);
            pMusic  = PlayerPrefs.GetFloat(SettingsStore.KMusic,  1f);
            pSfx    = PlayerPrefs.GetFloat(SettingsStore.KSfx,    1f);
            pVoice  = PlayerPrefs.GetFloat(SettingsStore.KVoice,  1f);
        }

        void RefreshUI()
        {
            refreshing = true;
            if (vsyncToggle)      vsyncToggle.isOn      = pVsync;
            if (fullscreenToggle) fullscreenToggle.isOn = pFull;
            if (masterSlider) masterSlider.value = pMaster;
            if (musicSlider)  musicSlider.value  = pMusic;
            if (sfxSlider)    sfxSlider.value    = pSfx;
            if (voiceSlider)  voiceSlider.value  = pVoice;
            refreshing = false;

            UpdateResolutionLabel();
            bool multi = resolutions != null && resolutions.Count > 1;
            if (prevResolutionButton) prevResolutionButton.interactable = multi;
            if (nextResolutionButton) nextResolutionButton.interactable = multi;

            HighlightQuality();
        }

        void HighlightQuality()
        {
            if (qualityButtons == null) return;
            for (int i = 0; i < qualityButtons.Length; i++)
            {
                Button b = qualityButtons[i];
                if (!b) continue;
                bool sel = i == pQuality;
                var juice = b.GetComponent<UIButtonJuice>();
                if (juice)
                {
                    juice.SetSelected(sel);
                    juice.ApplyImmediate();
                    continue;
                }
                var img = b.GetComponent<Image>();
                if (img) img.color = sel ? new Color(0.88f, 0.70f, 0.24f, 1f) : new Color(0.06f, 0.05f, 0.09f, 0.94f);
                var t = b.GetComponentInChildren<Text>();
                if (t) t.color = sel ? new Color(0.05f, 0.03f, 0.02f, 1f) : new Color(0.96f, 0.93f, 0.86f, 1f);
                var o = b.GetComponent<Outline>();
                if (o) o.effectDistance = sel ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
            }
        }

        void PreviewAudio()
        {
            if (audioMixer)
            {
                audioMixer.SetFloat("Vol_Master", SettingsStore.ToDb(pMaster));
                audioMixer.SetFloat("Vol_Music",  SettingsStore.ToDb(pMusic));
                audioMixer.SetFloat("Vol_SFX",    SettingsStore.ToDb(pSfx));
                audioMixer.SetFloat("Vol_Voice",  SettingsStore.ToDb(pVoice));
            }
            else
            {
                SettingsStore.ApplyAudioFallback(pMaster, pMusic);
            }
        }
    }

    /// <summary>Shared keys + startup loader (no scene object needed).</summary>
    public static class SettingsStore
    {
        public const string KQuality = "Settings_Quality", KVSync = "Settings_VSync", KFull = "Settings_Fullscreen",
                            KResW = "Settings_ResW", KResH = "Settings_ResH",
                            KMaster = "Settings_Vol_Master", KMusic = "Settings_Vol_Music",
                            KSfx = "Settings_Vol_SFX", KVoice = "Settings_Vol_Voice";

        public static float ToDb(float lin) { return lin > 0.001f ? 20f * Mathf.Log10(lin) : -80f; }

        /// <summary>"High" if it exists, else "Medium", else the middle tier.</summary>
        public static int DefaultQuality()
        {
            string[] n = QualitySettings.names;
            for (int i = 0; i < n.Length; i++) if (n[i].ToLower() == "high") return i;
            for (int i = 0; i < n.Length; i++) if (n[i].ToLower() == "medium") return i;
            return n.Length / 2;
        }

        /// <summary>Master → AudioListener.volume, Música → every BGM AudioSource.</summary>
        public static void ApplyAudioFallback(float master, float music)
        {
            AudioListener.volume = Mathf.Clamp01(master);
            foreach (AudioSource s in Object.FindObjectsOfType<AudioSource>())
                if (s != null && s.gameObject.name == "BGM") s.volume = Mathf.Clamp01(music);
        }

        public static void ApplySaved()
        {
            if (PlayerPrefs.HasKey(KQuality))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(KQuality), 0, QualitySettings.names.Length - 1), true);
            if (PlayerPrefs.HasKey(KVSync))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(KVSync) == 1 ? 1 : 0;

            if (PlayerPrefs.HasKey(KResW) && PlayerPrefs.HasKey(KResH))
            {
                bool full = PlayerPrefs.GetInt(KFull, 1) == 1;
                Screen.SetResolution(PlayerPrefs.GetInt(KResW), PlayerPrefs.GetInt(KResH),
                                     full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }

            ApplyAudioFallback(PlayerPrefs.GetFloat(KMaster, 1f), PlayerPrefs.GetFloat(KMusic, 1f));
        }
    }

    /// <summary>Applies saved settings when the game starts (TitleScreen) and after each scene load.</summary>
    static class SettingsStartup
    {
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SettingsStore.ApplySaved();
            if (hooked) return;
            hooked = true;
            SceneManager.sceneLoaded += (s, m) => SettingsStore.ApplyAudioFallback(PlayerPrefs.GetFloat(SettingsStore.KMaster, 1f), PlayerPrefs.GetFloat(SettingsStore.KMusic, 1f));
        }
    }
}

