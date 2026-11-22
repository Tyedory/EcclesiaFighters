using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace EcclesiaFighters
{
    /// <summary>
    /// Tabbed Settings panel logic (GERAL + CONTROLES).
    /// Controls edit pending values; APLICAR commits + saves to PlayerPrefs & KeybindManager,
    /// RESET restores defaults into pending widgets (keybinds on CONTROLES tab, video/audio on GERAL),
    /// VOLTAR (Discard) drops unapplied edits.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        [Header("Tabs (GERAL / CONTROLES)")]
        public Button     tabGeneralButton;
        public Button     tabControlsButton;
        public GameObject generalTabRoot;
        public GameObject controlsTabRoot;
        public Text       subHeadingLabel;

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

        [Header("Keybindings (CONTROLES Tab — 9 FighterActions per Player)")]
        public Button[] p1KeyButtons = new Button[KeybindManager.ActionCount];
        public Button[] p2KeyButtons = new Button[KeybindManager.ActionCount];

        public static readonly string[] ActionLabelsPT =
        {
            "Esquerda",
            "Direita",
            "Pular",
            "Agachar",
            "Defender",
            "Soco Leve",
            "Soco Forte",
            "Chute",
            "Especial"
        };

        // pending (edited) values
        int   pQuality, pRes;
        bool  pVsync, pFull;
        float pMaster = 1f, pMusic = 1f, pSfx = 1f, pVoice = 1f;
        readonly KeyCode[,] pKeys = new KeyCode[KeybindManager.PlayerCount, KeybindManager.ActionCount];

        int  activeTab; // 0 = GERAL, 1 = CONTROLES
        bool isListening;
        int  listenPlayer = -1, listenAction = -1, listenStartFrame = -1;

        List<Vector2Int> resolutions;
        bool refreshing;

        static readonly KeyCode[] RebindableKeys = BuildRebindableKeyTable();

        static KeyCode[] BuildRebindableKeyTable()
        {
            var list = new List<KeyCode>();
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                int code = (int)k;
                if (code <= 0 || code >= (int)KeyCode.Mouse0) continue; // ignore None, mouse, joystick
                if (k == KeyCode.Escape || k == KeyCode.F1 || k == KeyCode.Tab) continue;
                list.Add(k);
            }
            return list.ToArray();
        }

        // ─────────────────────────── lifecycle ───────────────────────────
        void OnEnable()
        {
            EnsureControlsTabUI();
            BuildResolutionList();
            LoadAppliedIntoPending();
            SetActiveTab(0);
            PreviewAudio();
        }

        void OnDisable()
        {
            CancelListening();
        }

        void Update()
        {
            if (!isListening || Time.frameCount <= listenStartFrame) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                AudioManager.PlayUIBack();
                CancelListening();
                ClearFocus();
                RefreshKeyButtons();
                return;
            }

            for (int i = 0; i < RebindableKeys.Length; i++)
            {
                KeyCode candidate = RebindableKeys[i];
                if (!Input.GetKeyDown(candidate)) continue;

                CommitPendingRebind(candidate);
                return;
            }
        }

        // ─────────────────────────── public UI callbacks ───────────────────────────
        public void ShowGeneralTab()  { SetActiveTab(0); ClearFocus(); }
        public void ShowControlsTab() { SetActiveTab(1); ClearFocus(); }

        public void SetActiveTab(int tabIndex)
        {
            CancelListening();
            activeTab = tabIndex == 1 ? 1 : 0;
            if (generalTabRoot)  generalTabRoot.SetActive(activeTab == 0);
            if (controlsTabRoot) controlsTabRoot.SetActive(activeTab == 1);

            if (subHeadingLabel)
                subHeadingLabel.text = activeTab == 1
                    ? "REMAPEAMENTO DE TECLAS  ·  JOGADOR 1 E JOGADOR 2"
                    : "VÍDEO, EXIBIÇÃO E ÁUDIO";

            RefreshUI();
        }

        public void StartRebindP1(int actionIndex) => StartRebind(0, actionIndex);
        public void StartRebindP2(int actionIndex) => StartRebind(1, actionIndex);

        public void StartRebind(int playerIndex, int actionIndex)
        {
            if (playerIndex < 0 || playerIndex >= KeybindManager.PlayerCount) return;
            if (actionIndex < 0 || actionIndex >= KeybindManager.ActionCount) return;

            isListening      = true;
            listenPlayer     = playerIndex;
            listenAction     = actionIndex;
            listenStartFrame = Time.frameCount;

            ClearFocus();
            RefreshKeyButtons();
        }

        public void SetQuality(int v)     { pQuality = Mathf.Clamp(v, 0, QualitySettings.names.Length - 1); RefreshUI(); }
        public void SetVSync(bool v)      { if (!refreshing) pVsync = v; }
        public void SetFullscreen(bool v) { if (!refreshing) pFull  = v; }

        public void NextResolution() { CycleResolution(1); }
        public void PrevResolution() { CycleResolution(-1); }

        public void SetMasterVol(float v) { if (refreshing) return; pMaster = v; PreviewAudio(); }
        public void SetMusicVol(float v)  { if (refreshing) return; pMusic  = v; PreviewAudio(); }
        public void SetSFXVol(float v)    { if (refreshing) return; pSfx    = v; PreviewAudio(); }
        public void SetVoiceVol(float v)  { if (refreshing) return; pVoice  = v; PreviewAudio(); }

        /// <summary>APLICAR — commit video, audio, and pending keybindings and save.</summary>
        public void Apply()
        {
            AudioManager.PlayUIConfirm();
            CancelListening();

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

            for (int p = 0; p < KeybindManager.PlayerCount; p++)
            {
                for (int a = 0; a < KeybindManager.ActionCount; a++)
                    KeybindManager.SetKey(p, (FighterAction)a, pKeys[p, a], saveImmediate: false);
            }
            KeybindManager.Save();
            PlayerPrefs.Save();

            PreviewAudio();
            RefreshUI();
            ClearFocus();
        }

        /// <summary>RESET — restore defaults on the active tab (CONTROLES resets keybinds; GERAL resets video/audio).</summary>
        public void ResetToDefaults()
        {
            CancelListening();

            if (activeTab == 1)
            {
                ResetPendingKeybindsToDefaults();
            }
            else
            {
                pQuality = SettingsStore.DefaultQuality();
                pVsync   = true;
                pFull    = true;
                pRes     = NativeResIndex();
                pMaster = pMusic = pSfx = pVoice = 1f;
                if (!controlsTabRoot) ResetPendingKeybindsToDefaults();
                PreviewAudio();
            }

            RefreshUI();
            ClearFocus();
        }

        /// <summary>VOLTAR — drop unapplied changes across both tabs.</summary>
        public void Discard()
        {
            AudioManager.PlayUIBack();
            CancelListening();
            LoadAppliedIntoPending();
            RefreshUI();
            PreviewAudio();
            ClearFocus();
        }

        /// <summary>Kept for compatibility with older scenes.</summary>
        public void SaveAndClose() { Apply(); gameObject.SetActive(false); }

        // ─────────────────────────── keybind internals ───────────────────────────
        void CancelListening()
        {
            isListening  = false;
            listenPlayer = -1;
            listenAction = -1;
        }

        void CommitPendingRebind(KeyCode newKey)
        {
            if (listenPlayer < 0 || listenAction < 0) { CancelListening(); return; }

            KeyCode oldKey = pKeys[listenPlayer, listenAction];
            if (oldKey != newKey)
            {
                // Auto-swap if another action on P1 or P2 already uses newKey
                for (int p = 0; p < KeybindManager.PlayerCount; p++)
                {
                    for (int a = 0; a < KeybindManager.ActionCount; a++)
                    {
                        if (p == listenPlayer && a == listenAction) continue;
                        if (pKeys[p, a] == newKey) pKeys[p, a] = oldKey;
                    }
                }
                pKeys[listenPlayer, listenAction] = newKey;
            }

            CancelListening();
            ClearFocus();
            RefreshKeyButtons();
        }

        void ResetPendingKeybindsToDefaults()
        {
            for (int p = 0; p < KeybindManager.PlayerCount; p++)
                for (int a = 0; a < KeybindManager.ActionCount; a++)
                    pKeys[p, a] = KeybindManager.GetDefaultKey(p, (FighterAction)a);
        }

        static void ClearFocus()
        {
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

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

            for (int p = 0; p < KeybindManager.PlayerCount; p++)
                for (int a = 0; a < KeybindManager.ActionCount; a++)
                    pKeys[p, a] = KeybindManager.GetKey(p, (FighterAction)a);
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
            ApplyButtonHighlight(tabGeneralButton,  activeTab == 0);
            ApplyButtonHighlight(tabControlsButton, activeTab == 1);
            RefreshKeyButtons();
        }

        void HighlightQuality()
        {
            if (qualityButtons == null) return;
            for (int i = 0; i < qualityButtons.Length; i++)
                ApplyButtonHighlight(qualityButtons[i], i == pQuality);
        }

        void RefreshKeyButtons()
        {
            RefreshPlayerKeyColumn(0, p1KeyButtons);
            RefreshPlayerKeyColumn(1, p2KeyButtons);
        }

        void RefreshPlayerKeyColumn(int playerIdx, Button[] buttons)
        {
            if (buttons == null) return;
            for (int a = 0; a < buttons.Length && a < KeybindManager.ActionCount; a++)
            {
                Button btn = buttons[a];
                if (!btn) continue;

                bool listeningThis = isListening && listenPlayer == playerIdx && listenAction == a;
                Text lbl = btn.GetComponentInChildren<Text>();
                if (lbl)
                {
                    lbl.text = listeningThis
                        ? "PRESSIONE UMA TECLA..."
                        : KeybindManager.GetPrettyKeyName(pKeys[playerIdx, a]);
                }
                ApplyButtonHighlight(btn, listeningThis);
            }
        }

        static void ApplyButtonHighlight(Button b, bool sel)
        {
            if (!b) return;
            var juice = b.GetComponent<UIButtonJuice>();
            if (juice)
            {
                juice.SetSelected(sel);
                juice.ApplyImmediate();
                return;
            }
            var img = b.GetComponent<Image>();
            if (img) img.color = sel ? new Color(0.88f, 0.70f, 0.24f, 1f) : new Color(0.06f, 0.05f, 0.09f, 0.94f);
            var t = b.GetComponentInChildren<Text>();
            if (t) t.color = sel ? new Color(0.05f, 0.03f, 0.02f, 1f) : new Color(0.96f, 0.93f, 0.86f, 1f);
            var o = b.GetComponent<Outline>();
            if (o) o.effectDistance = sel ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
        }

        // ─────────────────────────── Runtime Fallback Builder (un-rebuilt scenes) ───────────────────────────
        void EnsureControlsTabUI()
        {
            if (controlsTabRoot != null) return;

            Transform boxTf = transform.Find("Box");
            Transform bodyTf = boxTf ? boxTf.Find("BodyColumns") : null;
            if (!boxTf || !bodyTf) return;

            if (!generalTabRoot) generalTabRoot = bodyTf.gameObject;

            Transform headerTf = boxTf.Find("HeaderBar");
            if (headerTf)
            {
                var hrt = headerTf.GetComponent<RectTransform>();
                if (hrt) { hrt.anchorMin = new Vector2(0.025f, 0.878f); hrt.anchorMax = new Vector2(0.975f, 0.978f); }
                if (!subHeadingLabel) subHeadingLabel = headerTf.Find("SubHeading")?.GetComponent<Text>();
            }

            var brt = bodyTf.GetComponent<RectTransform>();
            if (brt) { brt.anchorMin = new Vector2(0.025f, 0.148f); brt.anchorMax = new Vector2(0.975f, 0.802f); }

            Button templateBtn = qualityButtons != null && qualityButtons.Length > 0 ? qualityButtons[0] : null;
            Sprite panelSpr = templateBtn && templateBtn.image ? templateBtn.image.sprite : null;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Tab bar (GERAL | CONTROLES)
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(boxTf, false);
            var trt = tabBar.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.26f, 0.812f); trt.anchorMax = new Vector2(0.74f, 0.868f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var thlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 16f;
            thlg.childControlWidth = true; thlg.childControlHeight = true;
            thlg.childForceExpandWidth = true; thlg.childForceExpandHeight = true;

            tabGeneralButton  = CreateRuntimeButton(tabBar.transform, "Tab_Geral",     "GERAL",     22, templateBtn, panelSpr, font, ShowGeneralTab);
            tabControlsButton = CreateRuntimeButton(tabBar.transform, "Tab_Controles", "CONTROLES", 22, templateBtn, panelSpr, font, ShowControlsTab);

            // Controls body two-column panel
            var ctrlGo = new GameObject("ControlsColumns", typeof(RectTransform));
            ctrlGo.transform.SetParent(boxTf, false);
            var crt = ctrlGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.025f, 0.148f); crt.anchorMax = new Vector2(0.975f, 0.802f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            var chlg = ctrlGo.AddComponent<HorizontalLayoutGroup>();
            chlg.padding = new RectOffset(8, 8, 6, 6); chlg.spacing = 24f;
            chlg.childControlWidth = true; chlg.childControlHeight = true;
            chlg.childForceExpandWidth = true; chlg.childForceExpandHeight = true;

            p1KeyButtons = BuildRuntimeKeyColumn(ctrlGo.transform, "P1ControlsColumn", "JOGADOR 1 (P1)", new Color(0.56f, 0.84f, 1f), 0, templateBtn, panelSpr, font);
            p2KeyButtons = BuildRuntimeKeyColumn(ctrlGo.transform, "P2ControlsColumn", "JOGADOR 2 (P2)", new Color(0.95f, 0.38f, 0.40f), 1, templateBtn, panelSpr, font);

            controlsTabRoot = ctrlGo;
            controlsTabRoot.SetActive(false);
        }

        Button[] BuildRuntimeKeyColumn(Transform parent, string name, string title, Color accent, int playerIdx, Button templateBtn, Sprite panelSpr, Font font)
        {
            var colGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            colGo.transform.SetParent(parent, false);
            var img = colGo.GetComponent<Image>();
            img.sprite = panelSpr;
            img.type   = panelSpr ? Image.Type.Sliced : Image.Type.Simple;
            img.color  = new Color(0.08f, 0.06f, 0.11f, 0.94f);

            var vlg = colGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 14, 14);
            vlg.spacing = 6f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(colGo.transform, false);
            var txt = titleGo.GetComponent<Text>();
            txt.font = font; txt.text = title; txt.fontSize = 23; txt.fontStyle = FontStyle.Bold; txt.color = accent;
            titleGo.AddComponent<LayoutElement>().preferredHeight = 30f;

            var btns = new Button[KeybindManager.ActionCount];
            for (int a = 0; a < KeybindManager.ActionCount; a++)
            {
                int actionIdx = a;
                var rowGo = new GameObject("BindRow_" + (FighterAction)a, typeof(RectTransform), typeof(Image));
                rowGo.transform.SetParent(colGo.transform, false);
                rowGo.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.92f);
                rowGo.AddComponent<LayoutElement>().preferredHeight = 46f;

                var rhlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                rhlg.padding = new RectOffset(14, 10, 4, 4); rhlg.spacing = 12f;
                rhlg.childControlWidth = true; rhlg.childControlHeight = true;
                rhlg.childForceExpandWidth = false; rhlg.childForceExpandHeight = true;

                var lblGo = new GameObject("ActionLabel", typeof(RectTransform), typeof(Text));
                lblGo.transform.SetParent(rowGo.transform, false);
                var ltxt = lblGo.GetComponent<Text>();
                ltxt.font = font; ltxt.text = ActionLabelsPT[a]; ltxt.fontSize = 19; ltxt.fontStyle = FontStyle.Bold;
                ltxt.color = new Color(0.96f, 0.93f, 0.86f, 1f); ltxt.alignment = TextAnchor.MiddleLeft;
                var lLe = lblGo.AddComponent<LayoutElement>();
                lLe.flexibleWidth = 1f;

                Button b = CreateRuntimeButton(rowGo.transform, "KeyBtn", "KEY", 18, templateBtn, panelSpr, font,
                    () => StartRebind(playerIdx, actionIdx));
                b.gameObject.AddComponent<LayoutElement>().preferredWidth = 225f;
                btns[a] = b;
            }
            return btns;
        }

        static Button CreateRuntimeButton(Transform parent, string name, string label, int fontSize, Button template, Sprite fallbackSpr, Font font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go;
            if (template)
            {
                go = Instantiate(template.gameObject, parent, false);
                go.name = name;
                var le = go.GetComponent<LayoutElement>();
                if (le) Destroy(le);
                var btn = go.GetComponent<Button>();
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(onClick);
                var txt = go.GetComponentInChildren<Text>(true);
                if (txt)
                {
                    txt.text = label;
                    txt.fontSize = fontSize;
                    txt.resizeTextForBestFit = true;
                    txt.resizeTextMinSize = 12;
                    txt.resizeTextMaxSize = fontSize;
                }
                return btn;
            }

            go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = fallbackSpr;
            img.type   = fallbackSpr ? Image.Type.Sliced : Image.Type.Simple;
            var b = go.GetComponent<Button>();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(onClick);

            var lGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            lGo.transform.SetParent(go.transform, false);
            var lrt = lGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(6f, 2f); lrt.offsetMax = new Vector2(-6f, -2f);
            var t = lGo.GetComponent<Text>();
            t.font = font; t.text = label; t.fontSize = fontSize; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = fontSize;
            return b;
        }

        void PreviewAudio()
        {
            if (AudioManager.Instance)
                AudioManager.Instance.SetVolumes(pMaster, pMusic, pSfx, pVoice);

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

        /// <summary>Master / Music / SFX / Voice routed cleanly through AudioManager (without double AudioListener attenuation).</summary>
        public static void ApplyAudioFallback(float master, float music)
        {
            var am = AudioManager.Instance;
            if (am != null)
            {
                AudioListener.volume = 1f;
                am.SetVolumes(
                    master,
                    music,
                    PlayerPrefs.GetFloat(KSfx,   1f),
                    PlayerPrefs.GetFloat(KVoice, 1f));
                return;
            }

            AudioListener.volume = Mathf.Clamp01(master);
            foreach (AudioSource s in UnityEngine.Object.FindObjectsOfType<AudioSource>())
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

