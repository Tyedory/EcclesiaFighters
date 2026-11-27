using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    /// <summary>
    /// Self-contained Sacred Arcade overlay controller for FightScene:
    /// 1) Adaptive Pre-Fight Controls Splash (before Round 1 only), reading live KeybindManager keys.
    /// 2) In-Fight Pause Menu (Esc) with FightDevMode-safe Time.timeScale save/restore,
    ///    quick-access audio sliders synced to SettingsStore/PlayerPrefs, and clean scene actions.
    /// Auto-attached and initialized by FightSceneManager.
    /// </summary>
    public class FightOverlayController : MonoBehaviour
    {
        // ── Shared Static Modal & TimeScale State ─────────────
        public static bool  IsPaused             { get; private set; }
        public static bool  IsPreFightOpen       { get; private set; }
        public static bool  IsAnyModalOpen       => IsPaused || IsPreFightOpen;
        public static float SavedResumeTimeScale => activeInstance ? activeInstance.savedTimeScale : 1f;

        static FightOverlayController activeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticFlags()
        {
            IsPaused       = false;
            IsPreFightOpen = false;
            activeInstance = null;
        }

        public static void SetResumeTimeScale(float scale)
        {
            if (activeInstance)
                activeInstance.savedTimeScale = Mathf.Clamp(scale, 0.05f, 4f);
        }

        // ── Sacred Arcade Palette ─────────────────────────────
        static readonly Color ColBackdrop     = new Color(0.02f, 0.015f, 0.04f, 0.78f);
        static readonly Color ColObsidian     = new Color(0.06f, 0.05f,  0.09f, 0.96f);
        static readonly Color ColCardSurface  = new Color(0.09f, 0.07f,  0.13f, 0.94f);
        static readonly Color ColSlotDark     = new Color(0.03f, 0.02f,  0.05f, 0.98f);
        static readonly Color ColBurgundy     = new Color(0.24f, 0.06f,  0.10f, 0.96f);
        static readonly Color ColCrimsonDark  = new Color(0.34f, 0.07f,  0.11f, 0.96f);
        static readonly Color ColAntiqueGold  = new Color(0.84f, 0.67f,  0.27f, 1.00f);
        static readonly Color ColBrightGold   = new Color(1.00f, 0.85f,  0.36f, 1.00f);
        static readonly Color ColMutedGold    = new Color(0.56f, 0.44f,  0.18f, 0.92f);
        static readonly Color ColIvory        = new Color(0.96f, 0.93f,  0.86f, 1.00f);
        static readonly Color ColIvoryMuted   = new Color(0.76f, 0.72f,  0.65f, 0.95f);
        static readonly Color ColInkDark      = new Color(0.03f, 0.02f,  0.04f, 1.00f);
        static readonly Color ColFaithBlue    = new Color(0.56f, 0.84f,  1.00f, 1.00f);
        static readonly Color ColIdeoCrimson  = new Color(0.95f, 0.38f,  0.40f, 1.00f);

        static readonly string[] FighterNamesFallback = { "Santo Agostinho", "Karl Marx" };

        // ── Runtime State ─────────────────────────────────────
        FightSceneManager mgr;
        Font              uiFont;
        GameObject        canvasGO;
        GameObject        preFightRoot;
        GameObject        pauseRoot;

        float savedTimeScale = 1f;
        bool  preFightDismissed;
        int   preFightOpenedFrame = -1;

        Slider masterSlider, musicSlider, sfxSlider, voiceSlider;
        Text   masterPctTxt, musicPctTxt, sfxPctTxt, voicePctTxt;
        bool   syncingAudio;
        bool   audioDirty;

        // ══════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════
        public void Initialize(FightSceneManager sceneManager)
        {
            mgr = sceneManager;
        }

        void Awake()
        {
            if (activeInstance && activeInstance != this)
                Destroy(activeInstance);

            activeInstance = this;
            IsPaused       = false;
            IsPreFightOpen = false;
            savedTimeScale = 1f;

            if (!mgr) mgr = GetComponent<FightSceneManager>();
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        void Update()
        {
            if (IsPreFightOpen)
            {
                if (Time.frameCount > preFightOpenedFrame &&
                    (Input.GetKeyDown(KeyCode.Return) ||
                     Input.GetKeyDown(KeyCode.KeypadEnter) ||
                     Input.GetKeyDown(KeyCode.Space)))
                {
                    DismissPreFightSplash();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsPaused)
                    Resume();
                else if (mgr && mgr.CanPauseMatch)
                    Pause();
            }
        }

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        void OnLanguageChanged(GameLanguage _)
        {
            if (IsPreFightOpen && preFightRoot != null)
                BuildOrRebuildPreFightSplash();
            if (pauseRoot != null)
            {
                bool wasOpen = IsPaused && pauseRoot.activeSelf;
                Destroy(pauseRoot);
                pauseRoot = null;
                EnsurePauseMenuBuilt();
                SyncAudioSlidersFromPrefs();
                if (pauseRoot != null) pauseRoot.SetActive(wasOpen);
            }
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
            if (audioDirty)
            {
                PlayerPrefs.Save();
                audioDirty = false;
            }
            if (activeInstance == this)
            {
                IsPaused       = false;
                IsPreFightOpen = false;
            }
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (audioDirty)
            {
                PlayerPrefs.Save();
                audioDirty = false;
            }
            if (activeInstance == this)
            {
                IsPaused       = false;
                IsPreFightOpen = false;
                activeInstance = null;
            }
            Time.timeScale = 1f;
            if (canvasGO)
            {
                Destroy(canvasGO);
                canvasGO = null;
            }
        }

        // ══════════════════════════════════════════════════════
        //  Pre-Fight Controls Splash (Before Round 1 Only)
        // ══════════════════════════════════════════════════════
        public IEnumerator ShowPreFightSplashAndWait()
        {
            EnsureOverlayCanvas();

            if (pauseRoot) pauseRoot.SetActive(false);
            IsPaused = false;

            BuildOrRebuildPreFightSplash();

            IsPreFightOpen      = true;
            preFightDismissed   = false;
            preFightOpenedFrame = Time.frameCount;

            if (preFightRoot) preFightRoot.SetActive(true);
            ClearEventSystemSelection();

            while (!preFightDismissed)
                yield return null;

            // Consume one extra frame so Enter/Space never bleeds into gameplay or animations.
            yield return null;
        }

        public void DismissPreFightSplash()
        {
            if (!IsPreFightOpen && preFightDismissed) return;
            AudioManager.PlayUIConfirm();
            IsPreFightOpen    = false;
            preFightDismissed = true;
            if (preFightRoot) preFightRoot.SetActive(false);
            ClearEventSystemSelection();
        }

        // ══════════════════════════════════════════════════════
        //  In-Fight Pause Menu (Esc)
        // ══════════════════════════════════════════════════════
        public void Pause()
        {
            if (IsPaused || IsPreFightOpen) return;
            if (!mgr || !mgr.CanPauseMatch) return;

            EnsureOverlayCanvas();
            EnsurePauseMenuBuilt();

            savedTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            IsPaused       = true;

            AudioManager.PlayUIClick();
            SyncAudioSlidersFromPrefs();
            if (pauseRoot) pauseRoot.SetActive(true);
            ClearEventSystemSelection();
        }

        public void Resume()
        {
            if (!IsPaused) return;

            if (audioDirty)
            {
                PlayerPrefs.Save();
                audioDirty = false;
            }

            AudioManager.PlayUIBack();
            if (pauseRoot) pauseRoot.SetActive(false);
            IsPaused       = false;
            Time.timeScale = savedTimeScale > 0.001f ? savedTimeScale : 1f;
            ClearEventSystemSelection();
        }

        public void ForceClosePauseForEndScreen()
        {
            if (!IsPaused) return;

            if (audioDirty)
            {
                PlayerPrefs.Save();
                audioDirty = false;
            }

            if (pauseRoot) pauseRoot.SetActive(false);
            IsPaused       = false;
            Time.timeScale = savedTimeScale > 0.001f ? savedTimeScale : 1f;
            ClearEventSystemSelection();
        }

        void LoadCleanScene(string sceneName)
        {
            IsPaused       = false;
            IsPreFightOpen = false;
            savedTimeScale = 1f;
            Time.timeScale = 1f;

            if (audioDirty) audioDirty = false;
            PlayerPrefs.Save();
            ClearEventSystemSelection();
            SceneManager.LoadScene(sceneName);
        }

        // ══════════════════════════════════════════════════════
        //  Canvas & Pre-Fight Splash Builder
        // ══════════════════════════════════════════════════════
        void EnsureOverlayCanvas()
        {
            if (!FindObjectOfType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            if (canvasGO) return;

            canvasGO = new GameObject("FightOverlayCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        void BuildOrRebuildPreFightSplash()
        {
            if (preFightRoot)
            {
                Destroy(preFightRoot);
                preFightRoot = null;
            }

            bool p1IsAI = PlayerPrefs.GetInt("P1IsAI", 0) == 1;
            bool p2IsAI = PlayerPrefs.GetInt("P2IsAI", 1) == 1;

            int idxA = Mathf.Clamp(PlayerPrefs.GetInt("SelectedCharA", 0), 0, 1);
            int idxB = Mathf.Clamp(PlayerPrefs.GetInt("SelectedCharB", 1), 0, 1);
            string nameA = LocalizationManager.GetFighterName(idxA);
            string nameB = LocalizationManager.GetFighterName(idxB);

            bool isAiVsAi = p1IsAI && p2IsAI;
            bool is2P     = !p1IsAI && !p2IsAI;

            preFightRoot = CreatePanel("PreFightSplashRoot", canvasGO.transform, ColBackdrop, raycastTarget: true);
            Stretch(preFightRoot.GetComponent<RectTransform>());

            Vector2 cardSize = is2P
                ? new Vector2(1080f, 796f)
                : (isAiVsAi ? new Vector2(680f, 440f) : new Vector2(680f, 796f));

            GameObject card = CreateFramedBox("PreFightCard", preFightRoot.transform, ColObsidian, ColAntiqueGold, 2f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRt.pivot            = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = cardSize;

            var cardVlg = card.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding                = new RectOffset(32, 32, 24, 24);
            cardVlg.spacing                = 12f;
            cardVlg.childAlignment         = TextAnchor.UpperCenter;
            cardVlg.childControlWidth      = true;
            cardVlg.childControlHeight     = true;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;

            string titleText;
            string subtitleText;
            string aiWord = LocalizationManager.Pick("IA", "AI");
            if (isAiVsAi)
            {
                titleText    = LocalizationManager.Pick("MODO ESPECTADOR — IA vs IA", "SPECTATOR MODE — AI vs AI");
                subtitleText = $"{nameA.ToUpper()}  VS  {nameB.ToUpper()}";
            }
            else if (is2P)
            {
                titleText    = LocalizationManager.Pick("CONTROLES DA LUTA", "FIGHT CONTROLS");
                subtitleText = $"{LocalizationManager.Pick("DUELO LOCAL 2P", "2P LOCAL DUEL")}  ·  {nameA.ToUpper()}  VS  {nameB.ToUpper()}";
            }
            else if (!p1IsAI)
            {
                titleText    = LocalizationManager.Pick("CONTROLES DA LUTA", "FIGHT CONTROLS");
                subtitleText = $"{LocalizationManager.Pick("JOGADOR 1", "PLAYER 1")}  ·  {nameA.ToUpper()}  VS  {aiWord}";
            }
            else
            {
                titleText    = LocalizationManager.Pick("CONTROLES DA LUTA", "FIGHT CONTROLS");
                subtitleText = $"{aiWord}  VS  {LocalizationManager.Pick("JOGADOR 2", "PLAYER 2")}  ·  {nameB.ToUpper()}";
            }

            Text title = CreateLabel("Title", card.transform, titleText, 30, FontStyle.Bold, ColBrightGold, TextAnchor.MiddleCenter);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize    = 20;
            title.resizeTextMaxSize    = 32;
            SetLayoutHeight(title.gameObject, 40f);

            Text sub = CreateLabel("Subtitle", card.transform, subtitleText, 16, FontStyle.Bold, ColIvory, TextAnchor.MiddleCenter);
            sub.resizeTextForBestFit = true;
            sub.resizeTextMinSize    = 12;
            sub.resizeTextMaxSize    = 17;
            SetLayoutHeight(sub.gameObject, 24f);

            CreateDivider(card.transform);

            if (isAiVsAi)
            {
                GameObject box = CreateFramedBox("SpectatorBody", card.transform, ColCardSurface, ColMutedGold, 1.5f);
                var boxLe = box.AddComponent<LayoutElement>();
                boxLe.flexibleHeight = 1f;
                boxLe.minHeight      = 170f;

                var boxVlg = box.AddComponent<VerticalLayoutGroup>();
                boxVlg.padding                = new RectOffset(26, 26, 20, 20);
                boxVlg.spacing                = 14f;
                boxVlg.childAlignment         = TextAnchor.MiddleCenter;
                boxVlg.childControlWidth      = true;
                boxVlg.childControlHeight     = true;
                boxVlg.childForceExpandWidth  = true;
                boxVlg.childForceExpandHeight = false;

                Text note = CreateLabel("SpectatorNote", box.transform,
                    LocalizationManager.Pick(
                        "Ambos os lutadores são controlados pela Inteligência Artificial.",
                        "Both fighters are controlled by Artificial Intelligence."),
                    18, FontStyle.Bold, ColIvory, TextAnchor.MiddleCenter);
                note.horizontalOverflow   = HorizontalWrapMode.Wrap;
                note.verticalOverflow     = VerticalWrapMode.Truncate;
                note.resizeTextForBestFit = true;
                note.resizeTextMinSize    = 13;
                note.resizeTextMaxSize    = 18;
                SetLayoutHeight(note.gameObject, 48f);

                CreateKeyRow(box.transform, LocalizationManager.Pick("Pausar / Opções", "Pause / Options"), "ESC", 180f);
                CreateKeyRow(box.transform, LocalizationManager.Pick("Ocultar / Exibir Dicas", "Hide / Show Hints"), "TAB", 180f);
            }
            else if (is2P)
            {
                var cols = new GameObject("Columns", typeof(RectTransform));
                cols.transform.SetParent(card.transform, false);
                var colsLe = cols.AddComponent<LayoutElement>();
                colsLe.flexibleHeight = 1f;
                colsLe.minHeight      = 472f;

                var hlg = cols.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing                = 24f;
                hlg.childControlWidth      = true;
                hlg.childControlHeight     = true;
                hlg.childForceExpandWidth  = true;
                hlg.childForceExpandHeight = true;

                BuildPlayerControlsColumn(cols.transform, 0, $"{LocalizationManager.Pick("JOGADOR 1", "PLAYER 1")}  ·  {nameA.ToUpper()}", ColFaithBlue,  210f, 16);
                BuildPlayerControlsColumn(cols.transform, 1, $"{LocalizationManager.Pick("JOGADOR 2", "PLAYER 2")}  ·  {nameB.ToUpper()}", ColIdeoCrimson, 210f, 16);
            }
            else
            {
                int humanIdx   = !p1IsAI ? 0 : 1;
                string pLabel  = humanIdx == 0
                    ? $"{LocalizationManager.Pick("JOGADOR 1", "PLAYER 1")}  ·  {nameA.ToUpper()}"
                    : $"{LocalizationManager.Pick("JOGADOR 2", "PLAYER 2")}  ·  {nameB.ToUpper()}";
                Color colHdr   = humanIdx == 0 ? ColFaithBlue : ColIdeoCrimson;

                GameObject col = BuildPlayerControlsColumn(card.transform, humanIdx, pLabel, colHdr, 220f, 17);
                var colLe = col.GetComponent<LayoutElement>() ?? col.AddComponent<LayoutElement>();
                colLe.flexibleHeight = 1f;
                colLe.minHeight      = 472f;
            }

            if (!isAiVsAi)
            {
                Text footer = CreateLabel("FooterHint", card.transform,
                    LocalizationManager.Pick(
                        "ESC: Pausar   ·   TAB: Ocultar Dicas   ·   ENTER ou ESPAÇO para iniciar",
                        "ESC: Pause   ·   TAB: Hide Hints   ·   ENTER or SPACE to start"),
                    14, FontStyle.Bold, ColIvoryMuted, TextAnchor.MiddleCenter);
                footer.resizeTextForBestFit = true;
                footer.resizeTextMinSize    = 11;
                footer.resizeTextMaxSize    = 14;
                SetLayoutHeight(footer.gameObject, 24f);
            }
            else
            {
                Text footer = CreateLabel("FooterHint", card.transform,
                    LocalizationManager.Pick("Pressione ENTER ou ESPAÇO para iniciar", "Press ENTER or SPACE to start"),
                    14, FontStyle.Bold, ColIvoryMuted, TextAnchor.MiddleCenter);
                SetLayoutHeight(footer.gameObject, 22f);
            }

            var btnRow = new GameObject("StartButtonRow", typeof(RectTransform));
            btnRow.transform.SetParent(card.transform, false);
            SetLayoutHeight(btnRow, 56f);
            var brHlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            brHlg.childAlignment         = TextAnchor.MiddleCenter;
            brHlg.childControlWidth      = false;
            brHlg.childControlHeight     = false;
            brHlg.childForceExpandWidth  = false;
            brHlg.childForceExpandHeight = false;

            Button startBtn = CreateActionButton(btnRow.transform,
                LocalizationManager.Pick("ENTENDI / INICIAR LUTA", "GOT IT / START FIGHT"),
                ColAntiqueGold, ColBrightGold, ColInkDark, 400f, 56f, 19, DismissPreFightSplash);
            startBtn.GetComponent<LayoutElement>().preferredWidth = 400f;
        }

        GameObject BuildPlayerControlsColumn(Transform parent, int playerIndex, string headerTitle, Color headerColor,
            float badgeWidth, int badgeMaxFont)
        {
            GameObject panel = CreateFramedBox($"ControlsCol_P{playerIndex + 1}", parent, ColCardSurface, ColMutedGold, 1.5f);
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding                = new RectOffset(20, 20, 14, 14);
            vlg.spacing                = 8f;
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            Text hdr = CreateLabel("ColHeader", panel.transform, headerTitle, 18, FontStyle.Bold, headerColor, TextAnchor.MiddleCenter);
            hdr.resizeTextForBestFit = true;
            hdr.resizeTextMinSize    = 13;
            hdr.resizeTextMaxSize    = 19;
            SetLayoutHeight(hdr.gameObject, 30f);

            CreateDivider(panel.transform);

            string leftKey   = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.MoveLeft));
            string rightKey  = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.MoveRight));
            string crouchKey = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Crouch));
            string blockKey  = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Block));

            CreateKeyRow(panel.transform, LocalizationManager.Pick("Mover", "Move"),                   $"{leftKey} / {rightKey}",    badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.Pick("Correr (2x Esq/Dir)", "Sprint (2x Left/Right)"), $"2x {leftKey} / {rightKey}", badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.GetActionLabel(FighterAction.Jump),       KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Jump)),       badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.GetActionLabel(FighterAction.Crouch),     crouchKey, badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.GetActionLabel(FighterAction.Block),      blockKey,  badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.GetActionLabel(FighterAction.LightPunch), KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.LightPunch)), badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.GetActionLabel(FighterAction.HeavyPunch), KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.HeavyPunch)), badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.GetActionLabel(FighterAction.Kick),       KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Kick)),       badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.GetActionLabel(FighterAction.Special),    KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Special)),    badgeWidth, badgeMaxFont);
            CreateKeyRow(panel.transform, LocalizationManager.Pick("Carregar Especial", "Charge Special"), $"{blockKey} + {crouchKey}", badgeWidth, badgeMaxFont);

            return panel;
        }

        void CreateKeyRow(Transform parent, string actionLabel, string keyDisplay, float badgeWidth, int badgeMaxFont = 16)
        {
            var row = new GameObject("Row_" + actionLabel, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 35f;
            rowLe.flexibleHeight  = 1f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 12f;
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;

            Text lbl = CreateLabel("ActionLabel", row.transform, actionLabel, 17, FontStyle.Bold, ColIvory, TextAnchor.MiddleLeft);
            lbl.resizeTextForBestFit = true;
            lbl.resizeTextMinSize    = 13;
            lbl.resizeTextMaxSize    = 17;
            var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
            lblLe.flexibleWidth = 1f;

            GameObject badge = CreateFramedBox("KeyBadge", row.transform, ColSlotDark, ColMutedGold, 1.5f);
            var badgeLe = badge.AddComponent<LayoutElement>();
            badgeLe.preferredWidth = badgeWidth;
            badgeLe.minWidth       = badgeWidth;

            Text keyTxt = CreateLabel("KeyText", badge.transform, keyDisplay, badgeMaxFont, FontStyle.Bold, ColBrightGold, TextAnchor.MiddleCenter);
            keyTxt.horizontalOverflow   = HorizontalWrapMode.Wrap;
            keyTxt.verticalOverflow     = VerticalWrapMode.Truncate;
            keyTxt.resizeTextForBestFit = true;
            keyTxt.resizeTextMinSize    = 12;
            keyTxt.resizeTextMaxSize    = badgeMaxFont;
            StretchWithPadding(keyTxt.GetComponent<RectTransform>(), 8f, 2f);
        }

        // ══════════════════════════════════════════════════════
        //  Pause Menu Builder & Audio Sync
        // ══════════════════════════════════════════════════════
        void EnsurePauseMenuBuilt()
        {
            if (pauseRoot) return;

            pauseRoot = CreatePanel("PauseMenuRoot", canvasGO.transform, ColBackdrop, raycastTarget: true);
            Stretch(pauseRoot.GetComponent<RectTransform>());

            GameObject card = CreateFramedBox("PauseCard", pauseRoot.transform, ColObsidian, ColAntiqueGold, 2f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRt.pivot            = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = new Vector2(580f, 690f);

            var cardVlg = card.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding                = new RectOffset(32, 32, 24, 24);
            cardVlg.spacing                = 11f;
            cardVlg.childAlignment         = TextAnchor.UpperCenter;
            cardVlg.childControlWidth      = true;
            cardVlg.childControlHeight     = true;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;

            Text title = CreateLabel("PauseTitle", card.transform, LocalizationManager.Pick("PAUSADO", "PAUSED"), 34, FontStyle.Bold, ColBrightGold, TextAnchor.MiddleCenter);
            SetLayoutHeight(title.gameObject, 42f);

            Text sub = CreateLabel("PauseSub", card.transform, LocalizationManager.Pick("ESC para retomar o combate", "ESC to resume combat"), 14, FontStyle.Bold, ColIvoryMuted, TextAnchor.MiddleCenter);
            SetLayoutHeight(sub.gameObject, 22f);

            CreateDivider(card.transform);

            // Quick-Access Volume Section
            GameObject audioBox = CreateFramedBox("AudioSection", card.transform, ColCardSurface, ColMutedGold, 1.5f);
            SetLayoutHeight(audioBox, 214f);

            var audioVlg = audioBox.AddComponent<VerticalLayoutGroup>();
            audioVlg.padding                = new RectOffset(18, 18, 12, 14);
            audioVlg.spacing                = 9f;
            audioVlg.childAlignment         = TextAnchor.UpperCenter;
            audioVlg.childControlWidth      = true;
            audioVlg.childControlHeight     = true;
            audioVlg.childForceExpandWidth  = true;
            audioVlg.childForceExpandHeight = false;

            Text audioTitle = CreateLabel("AudioHeader", audioBox.transform, LocalizationManager.Pick("ÁUDIO", "AUDIO"), 15, FontStyle.Bold, ColBrightGold, TextAnchor.MiddleLeft);
            SetLayoutHeight(audioTitle.gameObject, 22f);

            (masterSlider, masterPctTxt) = CreateVolumeRow(audioBox.transform, LocalizationManager.Pick("Geral",   "Master"), SettingsStore.KMaster);
            (musicSlider,  musicPctTxt)  = CreateVolumeRow(audioBox.transform, LocalizationManager.Pick("Música",  "Music"),  SettingsStore.KMusic);
            (sfxSlider,    sfxPctTxt)    = CreateVolumeRow(audioBox.transform, LocalizationManager.Pick("Efeitos", "SFX"),    SettingsStore.KSfx);
            (voiceSlider,  voicePctTxt)  = CreateVolumeRow(audioBox.transform, LocalizationManager.Pick("Vozes",   "Voices"), SettingsStore.KVoice);

            // Spacer between audio box and action buttons
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(card.transform, false);
            var spLe = spacer.AddComponent<LayoutElement>();
            spLe.preferredHeight = 4f;
            spLe.flexibleHeight  = 1f;

            // Four Action Buttons
            CreateActionButton(card.transform, LocalizationManager.Pick("CONTINUAR", "RESUME"),
                ColAntiqueGold, ColBrightGold, ColInkDark, 0f, 50f, 18, Resume);

            CreateActionButton(card.transform, LocalizationManager.Pick("REINICIAR LUTA", "RESTART FIGHT"),
                ColBurgundy, ColAntiqueGold, ColIvory, 0f, 50f, 17, () => { AudioManager.PlayUIConfirm(); LoadCleanScene("FightScene"); });

            CreateActionButton(card.transform, LocalizationManager.Pick("SELEÇÃO DE PERSONAGENS", "CHARACTER SELECT"),
                ColCardSurface, ColAntiqueGold, ColIvory, 0f, 50f, 17, () => { AudioManager.PlayUIBack(); LoadCleanScene("CharSelect"); });

            CreateActionButton(card.transform, LocalizationManager.Pick("MENU PRINCIPAL", "MAIN MENU"),
                ColCrimsonDark, ColMutedGold, ColIvory, 0f, 50f, 17, () => { AudioManager.PlayUIBack(); LoadCleanScene("TitleScreen"); });

            pauseRoot.SetActive(false);
        }

        (Slider, Text) CreateVolumeRow(Transform parent, string labelText, string prefKey)
        {
            var row = new GameObject("Vol_" + labelText, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetLayoutHeight(row, 32f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 12f;
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;

            Text lbl = CreateLabel("Label", row.transform, labelText, 16, FontStyle.Bold, ColIvory, TextAnchor.MiddleLeft);
            var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
            lblLe.preferredWidth = 96f;
            lblLe.minWidth       = 96f;

            // Slider Container
            var sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderGO.transform.SetParent(row.transform, false);
            var hitImg = sliderGO.GetComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0.001f); // ensures full row-height drag hit area
            var sliderLe = sliderGO.AddComponent<LayoutElement>();
            sliderLe.flexibleWidth = 1f;

            GameObject track = CreateFramedBox("Background", sliderGO.transform, ColSlotDark, ColMutedGold, 1f);
            var trackRt = track.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0f, 0.28f);
            trackRt.anchorMax = new Vector2(1f, 0.72f);
            trackRt.offsetMin = Vector2.zero;
            trackRt.offsetMax = Vector2.zero;
            track.GetComponent<Image>().raycastTarget = false;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.28f);
            faRt.anchorMax = new Vector2(1f, 0.72f);
            faRt.offsetMin = new Vector2(2f, 2f);
            faRt.offsetMax = new Vector2(-2f, -2f);

            GameObject fill = CreatePanel("Fill", fillArea.transform, ColAntiqueGold, raycastTarget: false);
            Stretch(fill.GetComponent<RectTransform>());

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGO.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(9f, 0f);
            haRt.offsetMax = new Vector2(-9f, 0f);

            GameObject handle = CreateFramedBox("Handle", handleArea.transform, ColBrightGold, ColInkDark, 1f);
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(18f, 24f);
            var hImg = handle.GetComponent<Image>();
            hImg.raycastTarget = true;

            var slider = sliderGO.GetComponent<Slider>();
            slider.navigation   = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect     = fill.GetComponent<RectTransform>();
            slider.handleRect   = hRt;
            slider.targetGraphic = hImg;
            slider.direction    = Slider.Direction.LeftToRight;
            slider.minValue     = 0f;
            slider.maxValue     = 1f;

            Text pct = CreateLabel("Pct", row.transform, "100%", 14, FontStyle.Bold, ColBrightGold, TextAnchor.MiddleRight);
            var pctLe = pct.gameObject.AddComponent<LayoutElement>();
            pctLe.preferredWidth = 52f;
            pctLe.minWidth       = 52f;

            slider.onValueChanged.AddListener(val => OnVolumeSliderChanged(prefKey, val, pct));
            return (slider, pct);
        }

        void SyncAudioSlidersFromPrefs()
        {
            syncingAudio = true;

            float master = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsStore.KMaster, 1f));
            float music  = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsStore.KMusic,  1f));
            float sfx    = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsStore.KSfx,    1f));
            float voice  = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsStore.KVoice,  1f));

            ApplySliderDisplay(masterSlider, masterPctTxt, master);
            ApplySliderDisplay(musicSlider,  musicPctTxt,  music);
            ApplySliderDisplay(sfxSlider,    sfxPctTxt,    sfx);
            ApplySliderDisplay(voiceSlider,  voicePctTxt,  voice);

            syncingAudio = false;
        }

        static void ApplySliderDisplay(Slider s, Text pctTxt, float value)
        {
            if (s) s.SetValueWithoutNotify(value);
            if (pctTxt) pctTxt.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        void OnVolumeSliderChanged(string prefKey, float value, Text pctTxt)
        {
            float clamped = Mathf.Clamp01(value);
            if (pctTxt) pctTxt.text = Mathf.RoundToInt(clamped * 100f) + "%";
            if (syncingAudio) return;

            PlayerPrefs.SetFloat(prefKey, clamped);
            audioDirty = true;

            float master = masterSlider ? masterSlider.value : PlayerPrefs.GetFloat(SettingsStore.KMaster, 1f);
            float music  = musicSlider  ? musicSlider.value  : PlayerPrefs.GetFloat(SettingsStore.KMusic,  1f);
            float sfx    = sfxSlider    ? sfxSlider.value    : PlayerPrefs.GetFloat(SettingsStore.KSfx,    1f);
            float voice  = voiceSlider  ? voiceSlider.value  : PlayerPrefs.GetFloat(SettingsStore.KVoice,  1f);

            if (AudioManager.Instance)
                AudioManager.Instance.SetVolumes(master, music, sfx, voice);
            else
                SettingsStore.ApplyAudioFallback(master, music);
        }

        // ══════════════════════════════════════════════════════
        //  UI Factory Helpers
        // ══════════════════════════════════════════════════════
        static GameObject CreatePanel(string name, Transform parent, Color color, bool raycastTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color         = color;
            img.raycastTarget = raycastTarget;
            return go;
        }

        static GameObject CreateFramedBox(string name, Transform parent, Color fill, Color border, float borderPx)
        {
            var go = CreatePanel(name, parent, fill, raycastTarget: true);
            var outline = go.AddComponent<Outline>();
            outline.effectColor    = border;
            outline.effectDistance = new Vector2(borderPx, -borderPx);
            return go;
        }

        void CreateDivider(Transform parent)
        {
            var div = CreatePanel("GoldDivider", parent, ColAntiqueGold, raycastTarget: false);
            SetLayoutHeight(div, 2f);
        }

        Text CreateLabel(string name, Transform parent, string text, int size, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font          = uiFont;
            t.text          = text;
            t.fontSize      = size;
            t.fontStyle     = style;
            t.color         = color;
            t.alignment     = align;
            t.raycastTarget = false;
            return t;
        }

        Button CreateActionButton(
            Transform parent, string label, Color bg, Color border, Color textColor,
            float width, float height, int fontSize, Action onClick)
        {
            var go = CreateFramedBox("Btn_" + label, parent, bg, border, 1.5f);
            var rt = go.GetComponent<RectTransform>();
            if (width > 0f) rt.sizeDelta = new Vector2(width, height);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            if (width > 0f) le.preferredWidth = width;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.navigation    = new Navigation { mode = Navigation.Mode.None };

            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.16f, 1.12f, 1.05f, 1f);
            colors.pressedColor     = new Color(0.82f, 0.80f, 0.78f, 1f);
            colors.selectedColor    = Color.white;
            btn.colors = colors;

            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            go.AddComponent<OverlayButtonHoverSfx>();

            Text txt = CreateLabel("Label", go.transform, label, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize    = 12;
            txt.resizeTextMaxSize    = fontSize;
            StretchWithPadding(txt.GetComponent<RectTransform>(), 14f, 4f);

            return btn;
        }

        sealed class OverlayButtonHoverSfx : MonoBehaviour, IPointerEnterHandler
        {
            public void OnPointerEnter(PointerEventData eventData) => AudioManager.PlayUIHover();
        }

        static void SetLayoutHeight(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight       = height;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void StretchWithPadding(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        static void ClearEventSystemSelection()
        {
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
