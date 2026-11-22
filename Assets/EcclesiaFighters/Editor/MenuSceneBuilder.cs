using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EcclesiaFighters.EditorTools
{
    /// <summary>
    /// Generates and restyles UI (Canvas, EventSystem, procedural Sacred Arcade sprites,
    /// panels, buttons, layouts, and audio) for TitleScreen, CharSelect, MapSelect, and FightScene HUD.
    ///
    /// Menus:
    ///   EcclesiaFighters ▸ Menus ▸ Build All Menu Scenes
    ///   EcclesiaFighters ▸ Menus ▸ Rebuild Fight HUD
    /// CLI:
    ///   Unity -batchmode -quit -projectPath . -executeMethod EcclesiaFighters.EditorTools.MenuSceneBuilder.BuildAllBatch
    ///   Unity -batchmode -quit -projectPath . -executeMethod EcclesiaFighters.EditorTools.MenuSceneBuilder.RebuildFightHudBatch
    /// </summary>
    public static class MenuSceneBuilder
    {
        const string Root       = "Assets/EcclesiaFighters/";
        const string ScenesDir  = Root + "Scenes/";
        const string SpritesDir = Root + "Sprites/";
        const string UISpritesDir = SpritesDir + "UI/";
        const string AudioDir   = Root + "Audio/";

        // ── "Sacred Arcade / Baroque Cathedral" Palette ───────────────────────
        static readonly Color ObsidianBg    = new Color(0.04f, 0.03f, 0.06f, 0.94f);
        static readonly Color ObsidianPanel = new Color(0.06f, 0.05f, 0.09f, 0.92f);
        static readonly Color CardSurface   = new Color(0.08f, 0.06f, 0.11f, 0.94f);
        static readonly Color SlotDark      = new Color(0.03f, 0.02f, 0.05f, 0.96f);
        static readonly Color BurgundyPanel = new Color(0.22f, 0.05f, 0.09f, 0.93f);
        static readonly Color CrimsonDark   = new Color(0.32f, 0.06f, 0.10f, 0.96f);
        static readonly Color Crimson       = new Color(0.68f, 0.12f, 0.17f, 1.00f);
        static readonly Color CrimsonBright = new Color(0.88f, 0.20f, 0.24f, 1.00f);
        static readonly Color AntiqueGold   = new Color(0.84f, 0.67f, 0.27f, 1.00f);
        static readonly Color BrightGold    = new Color(1.00f, 0.85f, 0.36f, 1.00f);
        static readonly Color MutedGold     = new Color(0.56f, 0.44f, 0.18f, 0.92f);
        static readonly Color Ivory         = new Color(0.96f, 0.93f, 0.86f, 1.00f);
        static readonly Color IvoryMuted    = new Color(0.76f, 0.72f, 0.65f, 0.95f);
        static readonly Color FaithBlue     = new Color(0.56f, 0.84f, 1.00f, 1.00f);
        static readonly Color IdeoCrimson   = new Color(0.95f, 0.38f, 0.40f, 1.00f);
        static readonly Color InkDark       = new Color(0.02f, 0.01f, 0.03f, 0.92f);

        enum ButtonVariant { Default, PrimaryCrimson, SecondaryDanger, Compact }

        static Sprite sprPanel, sprFrameOrnate, sprPill, sprKnob, sprSheen, sprDivider;

        static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ═════════════════════════════════════════════════════════════════════
        //  Menu Entries
        // ═════════════════════════════════════════════════════════════════════
        [MenuItem("EcclesiaFighters/Menus/Build All Menu Scenes")]
        public static void BuildAll()
        {
            if (!EditorUtility.DisplayDialog("Build menu scenes",
                    "This rebuilds Canvas / EventSystem / BGM in TitleScreen, CharSelect and MapSelect.\n" +
                    "Any existing 'Canvas' object in those scenes is replaced.",
                    "Rebuild", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAllBatch();
            EditorUtility.DisplayDialog("Build menu scenes", "TitleScreen, CharSelect and MapSelect were rebuilt and saved.", "OK");
        }

        [MenuItem("EcclesiaFighters/Menus/Build TitleScreen")]
        public static void MenuTitle()      { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildTitle(); }

        [MenuItem("EcclesiaFighters/Menus/Build CharSelect")]
        public static void MenuCharSelect() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildCharSelect(); }

        [MenuItem("EcclesiaFighters/Menus/Build MapSelect")]
        public static void MenuMapSelect()  { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildMapSelect(); }

        [MenuItem("EcclesiaFighters/Menus/Rebuild Fight HUD")]
        public static void MenuFightHud()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Fight HUD",
                    "This restyles the FightScene HUD Canvas and sets up the 3D Church Fight Arena while preserving FightSceneManager gameplay, spawn, and stage references.\nContinue?",
                    "Rebuild HUD", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RebuildFightHudBatch();
            EditorUtility.DisplayDialog("Rebuild Fight HUD", "FightScene HUD & 3D Church Arena were rebuilt and saved.", "OK");
        }

        [MenuItem("EcclesiaFighters/Menus/Setup 3D Church Fight Arena")]
        public static void MenuSetupFightArena3D()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupFightArena3DBatch();
            EditorUtility.DisplayDialog("Setup 3D Church Fight Arena",
                "ChurchStageController, FighterCamera, igreja de novo 8 (2).fbx, and invisible Floor/Wall colliders are ready in FightScene.", "OK");
        }

        /// <summary>No dialogs: usable from -executeMethod.</summary>
        public static void BuildAllBatch()
        {
            EnsureUISprites();
            BuildTitle();
            BuildCharSelect();
            BuildMapSelect();
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuSceneBuilder] TitleScreen, CharSelect and MapSelect rebuilt.");
        }

        /// <summary>No dialogs: usable from -executeMethod.</summary>
        public static void RebuildFightHudBatch()
        {
            EnsureUISprites();
            BuildFightHud();
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuSceneBuilder] FightScene HUD & 3D Church Arena rebuilt.");
        }

        /// <summary>No dialogs: usable from -executeMethod.</summary>
        public static void SetupFightArena3DBatch()
        {
            Scene scene = OpenScene("FightScene");
            var mgr = UnityEngine.Object.FindObjectOfType<FightSceneManager>();
            if (!mgr)
            {
                Debug.LogError("[MenuSceneBuilder] FightSceneManager not found in FightScene.");
                return;
            }
            SetupFightArena3D(mgr);
            SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuSceneBuilder] FightScene 3D Church Arena configured.");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TitleScreen
        // ═════════════════════════════════════════════════════════════════════
        static void BuildTitle()
        {
            EnsureUISprites();
            Scene scene = OpenScene("TitleScreen");
            ClearGenerated(scene);
            SetupCamera();
            // BGM is managed persistently by AudioManager (no standalone scene BGM AudioSource)
            var mgr = GetOrCreateManager<TitleScreenManager>("TitleScreenManager");
            RectTransform root = CreateCanvas();

            // ── Title panel ──
            RectTransform titlePanel = NewRect("TitlePanel", root); Stretch(titlePanel);
            Image bg = AddImage(titlePanel, "Background", Color.white, LoadSprite("Backgrounds/BG_TitleScreen.jpg"), false);
            Stretch(bg.rectTransform);
            Image vignette = AddImage(titlePanel, "Shade", new Color(0.02f, 0.01f, 0.04f, 0.48f), null, false);
            Stretch(vignette.rectTransform);

            // Cathedral marquee header
            RectTransform logo = NewRect("TitleLogo", titlePanel);
            Region(logo, 0.14f, 0.56f, 0.86f, 0.92f);
            var logoGroup = logo.gameObject.AddComponent<CanvasGroup>();
            logoGroup.alpha = 0f; logoGroup.interactable = false; logoGroup.blocksRaycasts = false;

            Image crestBg = AddFramedPanel(logo, "CrestPanel", new Color(0.05f, 0.03f, 0.07f, 0.72f), AntiqueGold, false);
            Stretch(crestBg.rectTransform);

            AddDivider(logo, "TopDivider", BrightGold, 0.14f, 0.88f, 0.86f, 0.92f);

            Text title = AddText(logo, "Title", "ECCLESIA FIGHTERS", 104, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(title.rectTransform, 0.04f, 0.42f, 0.96f, 0.86f);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 48; title.resizeTextMaxSize = 110;
            AddStrongTextEffects(title, 3f, 4f);

            AddDivider(logo, "MidDivider", AntiqueGold, 0.22f, 0.36f, 0.78f, 0.41f);

            Text sub = AddText(logo, "Subtitle", "FÉ  ×  IDEOLOGIA", 40, Ivory, TextAnchor.MiddleCenter, FontStyle.Italic);
            Region(sub.rectTransform, 0.06f, 0.10f, 0.94f, 0.35f);
            AddStrongTextEffects(sub, 2f, 3f);

            // Menu group with VerticalLayoutGroup
            RectTransform menu = NewRect("MenuGroup", titlePanel);
            Region(menu, 0.34f, 0.10f, 0.66f, 0.50f);
            var menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
            menuGroup.alpha = 0f;

            Image menuTray = AddFramedPanel(menu, "MenuTray", new Color(0.04f, 0.03f, 0.06f, 0.78f), MutedGold, false);
            Stretch(menuTray.rectTransform);

            RectTransform btnList = NewRect("ButtonList", menu); Stretch(btnList);
            var vlg = btnList.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            string[] labels = { "INICIAR BATALHA", "CONFIGURAÇÕES", "SAIR" };
            ButtonVariant[] variants = { ButtonVariant.PrimaryCrimson, ButtonVariant.Default, ButtonVariant.SecondaryDanger };
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                buttons[i] = AddButton(btnList, "Btn_" + labels[i].Replace(" ", ""), labels[i], 36, variants[i]);
                AddHoverSelect(buttons[i], mgr, i);
            }
            UnityEventTools.AddPersistentListener(buttons[0].onClick, mgr.GotoCharSelect);
            UnityEventTools.AddPersistentListener(buttons[1].onClick, mgr.GotoSettings);
            UnityEventTools.AddPersistentListener(buttons[2].onClick, mgr.QuitGame);

            Text navHint = AddText(titlePanel, "NavHint", "USE ↑ ↓ E ENTER OU CLIQUE PARA NAVEGAR", 20, IvoryMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
            Region(navHint.rectTransform, 0.25f, 0.025f, 0.75f, 0.075f);

            // ── Settings panel (hidden at start, SettingsManager lives on it) ──
            GameObject settingsPanel = BuildSettingsPanel(root, mgr);

            // ── Overlays (always last = always on top) ──
            Image flash = AddImage(root, "LightningFlash", new Color(1f, 1f, 1f, 0f), null, false);
            Stretch(flash.rectTransform);
            Image fade = AddImage(root, "FadeOverlay", new Color(0f, 0f, 0f, 0f), null, false);
            Stretch(fade.rectTransform);

            // ── Wire TitleScreenManager ──
            mgr.titlePanel      = titlePanel.gameObject;
            mgr.charSelectPanel = null;
            mgr.mapSelectPanel  = null;
            mgr.settingsPanel   = settingsPanel;
            mgr.fadeOverlay     = fade;
            mgr.lightningFlash  = flash;
            mgr.titleLogoGroup  = logoGroup;
            mgr.menuGroup       = menuGroup;
            mgr.bgmSource       = null;
            mgr.bgmTitleClip    = LoadClip("BGM_TitleScreen.mp3");
            mgr.bgmCharClip     = LoadClip("BGM_CharSelect_JesuJoy.mp3");
            mgr.menuButtons     = buttons;
            mgr.charSelectScene = "CharSelect";
            mgr.mapSelectScene  = "MapSelect";
            EditorUtility.SetDirty(mgr);

            SaveScene(scene);
            Debug.Log("[MenuSceneBuilder] TitleScreen built.");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Configurações Panel
        // ═════════════════════════════════════════════════════════════════════
        static GameObject BuildSettingsPanel(RectTransform root, TitleScreenManager title)
        {
            RectTransform panel = NewRect("SettingsPanel", root); Stretch(panel);
            var settings = panel.gameObject.AddComponent<SettingsManager>();

            Image bg = AddImage(panel, "Background", Color.white, LoadSprite("Backgrounds/BG_TitleScreen_Dark.jpg"), true);
            Stretch(bg.rectTransform);
            Image shade = AddImage(panel, "BackdropShade", new Color(0.02f, 0.01f, 0.04f, 0.62f), null, false);
            Stretch(shade.rectTransform);

            // Main modal box
            Image box = AddFramedPanel(panel, "Box", ObsidianBg, AntiqueGold, true);
            Region(box.rectTransform, 0.13f, 0.06f, 0.87f, 0.94f);

            // Burgundy header bar
            Image headerBar = AddFramedPanel(box.rectTransform, "HeaderBar", BurgundyPanel, AntiqueGold, false);
            Region(headerBar.rectTransform, 0.025f, 0.878f, 0.975f, 0.978f);

            Text head = AddText(headerBar.rectTransform, "Heading", "CONFIGURAÇÕES", 44, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(head.rectTransform, 0.04f, 0.28f, 0.96f, 0.94f);
            AddStrongTextEffects(head, 2.5f, 3.5f);

            Text subHead = AddText(headerBar.rectTransform, "SubHeading", "VÍDEO, EXIBIÇÃO E ÁUDIO", 19, IvoryMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
            Region(subHead.rectTransform, 0.04f, 0.04f, 0.96f, 0.30f);

            // Top tab bar (GERAL | CONTROLES)
            RectTransform tabBar = NewRect("TabBar", box.rectTransform);
            Region(tabBar, 0.26f, 0.812f, 0.74f, 0.868f);
            var tabHlg = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 18f;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;

            Button tabGeral = AddButton(tabBar, "Tab_Geral", "GERAL", 22, ButtonVariant.Compact);
            tabGeral.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(tabGeral.onClick, settings.ShowGeneralTab);
            tabGeral.GetComponent<UIButtonJuice>()?.SetSelected(true);

            Button tabControles = AddButton(tabBar, "Tab_Controles", "CONTROLES", 22, ButtonVariant.Compact);
            tabControles.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(tabControles.onClick, settings.ShowControlsTab);
            tabControles.GetComponent<UIButtonJuice>()?.SetSelected(false);

            // Two-column body (GERAL tab)
            RectTransform body = NewRect("BodyColumns", box.rectTransform);
            Region(body, 0.025f, 0.148f, 0.975f, 0.802f);
            var bodyHlg = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyHlg.padding = new RectOffset(8, 8, 6, 6);
            bodyHlg.spacing = 24f;
            bodyHlg.childControlWidth = true;
            bodyHlg.childControlHeight = true;
            bodyHlg.childForceExpandWidth = false;
            bodyHlg.childForceExpandHeight = true;

            // Left column: Video & Graphics
            Image leftCol = AddFramedPanel(body, "LeftColumn", CardSurface, MutedGold, false);
            var leftLe = leftCol.gameObject.AddComponent<LayoutElement>();
            leftLe.flexibleWidth = 1.08f;

            var leftVlg = leftCol.gameObject.AddComponent<VerticalLayoutGroup>();
            leftVlg.padding = new RectOffset(20, 20, 14, 14);
            leftVlg.spacing = 12f;
            leftVlg.childAlignment = TextAnchor.UpperLeft;
            leftVlg.childControlWidth = true;
            leftVlg.childControlHeight = true;
            leftVlg.childForceExpandWidth = true;
            leftVlg.childForceExpandHeight = false;

            Text secVideo = AddText(leftCol.rectTransform, "VideoSection", "VÍDEO & GRÁFICOS", 25, AntiqueGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetLayoutElement(secVideo.gameObject, preferredHeight: 34f);
            AddLayoutDivider(leftCol.rectTransform, "VideoDivider", AntiqueGold);

            Text gl = AddText(leftCol.rectTransform, "GraphicsLabel", "QUALIDADE GRÁFICA", 20, IvoryMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetLayoutElement(gl.gameObject, preferredHeight: 28f);

            string[] qn = QualitySettings.names;
            int cols = Mathf.Clamp(qn.Length, 1, 3);
            int rows = Mathf.CeilToInt(qn.Length / (float)cols);
            RectTransform qGridGo = NewRect("QualityGrid", leftCol.rectTransform);
            var grid = qGridGo.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.cellSize = new Vector2(182f, 46f);
            grid.spacing = new Vector2(10f, 10f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            SetLayoutElement(qGridGo.gameObject, preferredHeight: rows * 46f + Mathf.Max(0, rows - 1) * 10f);

            var qBtns = new Button[qn.Length];
            for (int i = 0; i < qn.Length; i++)
            {
                qBtns[i] = AddButton(qGridGo, "Quality_" + qn[i], qn[i].ToUpper(), 20, ButtonVariant.Compact);
                var j = qBtns[i].GetComponent<UIButtonJuice>();
                if (j)
                {
                    j.driveLabelColor = true;
                    j.selectedBg      = new Color(0.88f, 0.70f, 0.24f, 1f);
                    j.selectedFrame   = BrightGold;
                    j.selectedText    = new Color(0.05f, 0.03f, 0.02f, 1f);
                }
                UnityEventTools.AddIntPersistentListener(qBtns[i].onClick, settings.SetQuality, i);
            }

            Text rl = AddText(leftCol.rectTransform, "ResolutionLabel", "RESOLUÇÃO", 20, IvoryMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetLayoutElement(rl.gameObject, preferredHeight: 28f);

            RectTransform resRow = NewRect("ResolutionRow", leftCol.rectTransform);
            SetLayoutElement(resRow.gameObject, preferredHeight: 54f);
            var resHlg = resRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            resHlg.spacing = 12f;
            resHlg.childControlWidth = true;
            resHlg.childControlHeight = true;
            resHlg.childForceExpandWidth = false;
            resHlg.childForceExpandHeight = true;

            Button resPrev = AddButton(resRow, "Btn_ResPrev", "<", 28, ButtonVariant.Compact);
            SetLayoutElement(resPrev.gameObject, preferredWidth: 64f);
            var prevJuice = resPrev.GetComponent<UIButtonJuice>();
            if (prevJuice) { prevJuice.hoverScale = 1.0f; prevJuice.pressedScale = 0.98f; }

            Image resBox = AddFramedPanel(resRow, "ResolutionBox", SlotDark, MutedGold, false);
            SetLayoutElement(resBox.gameObject, flexibleWidth: 1f);
            Text resText = AddText(resBox.rectTransform, "ResolutionValue", "1920 x 1080", 26, Ivory, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(resText.rectTransform);

            Button resNext = AddButton(resRow, "Btn_ResNext", ">", 28, ButtonVariant.Compact);
            SetLayoutElement(resNext.gameObject, preferredWidth: 64f);
            var nextJuice = resNext.GetComponent<UIButtonJuice>();
            if (nextJuice) { nextJuice.hoverScale = 1.0f; nextJuice.pressedScale = 0.98f; }

            UnityEventTools.AddPersistentListener(resPrev.onClick, settings.PrevResolution);
            UnityEventTools.AddPersistentListener(resNext.onClick, settings.NextResolution);

            // Toggle row (V-Sync & Tela Cheia)
            RectTransform toggleRow = NewRect("ToggleRow", leftCol.rectTransform);
            SetLayoutElement(toggleRow.gameObject, preferredHeight: 60f);
            var togHlg = toggleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            togHlg.spacing = 14f;
            togHlg.childControlWidth = true;
            togHlg.childControlHeight = true;
            togHlg.childForceExpandWidth = true;
            togHlg.childForceExpandHeight = true;

            Toggle vsync = AddToggle(toggleRow, "VSyncToggle", "V-SYNC");
            UnityEventTools.AddPersistentListener<bool>(vsync.onValueChanged, settings.SetVSync);
            Toggle full = AddToggle(toggleRow, "FullscreenToggle", "TELA CHEIA");
            UnityEventTools.AddPersistentListener<bool>(full.onValueChanged, settings.SetFullscreen);

            // Center vertical divider strip
            Image centerSep = AddImage(body, "ColumnDivider", new Color(0.84f, 0.67f, 0.27f, 0.35f), null, false);
            SetLayoutElement(centerSep.gameObject, preferredWidth: 2f);

            // Right column: Audio & Mixing
            Image rightCol = AddFramedPanel(body, "RightColumn", CardSurface, MutedGold, false);
            var rightLe = rightCol.gameObject.AddComponent<LayoutElement>();
            rightLe.flexibleWidth = 1.0f;

            var rightVlg = rightCol.gameObject.AddComponent<VerticalLayoutGroup>();
            rightVlg.padding = new RectOffset(20, 20, 14, 14);
            rightVlg.spacing = 14f;
            rightVlg.childAlignment = TextAnchor.UpperLeft;
            rightVlg.childControlWidth = true;
            rightVlg.childControlHeight = true;
            rightVlg.childForceExpandWidth = true;
            rightVlg.childForceExpandHeight = false;

            Text secAudio = AddText(rightCol.rectTransform, "AudioSection", "ÁUDIO & MIXAGEM", 25, AntiqueGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetLayoutElement(secAudio.gameObject, preferredHeight: 34f);
            AddLayoutDivider(rightCol.rectTransform, "AudioDivider", AntiqueGold);

            string[] names = { "MASTER", "MÚSICA", "EFEITOS", "VOZES" };
            var sliders = new Slider[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Image volCard = AddFramedPanel(rightCol.rectTransform, names[i] + "Row", SlotDark, new Color(0.56f, 0.44f, 0.18f, 0.55f), false);
                SetLayoutElement(volCard.gameObject, preferredHeight: 62f);

                var rowHlg = volCard.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowHlg.padding = new RectOffset(16, 16, 10, 10);
                rowHlg.spacing = 16f;
                rowHlg.childAlignment = TextAnchor.MiddleCenter;
                rowHlg.childControlWidth = true;
                rowHlg.childControlHeight = true;
                rowHlg.childForceExpandWidth = false;
                rowHlg.childForceExpandHeight = false;

                Text l = AddText(volCard.rectTransform, names[i] + "Label", names[i], 23, Ivory, TextAnchor.MiddleLeft, FontStyle.Bold);
                SetLayoutElement(l.gameObject, preferredWidth: 132f, preferredHeight: 36f);

                sliders[i] = AddSlider(volCard.rectTransform, names[i] + "Slider");
                SetLayoutElement(sliders[i].gameObject, flexibleWidth: 1f, preferredHeight: 36f);
            }
            UnityEventTools.AddPersistentListener<float>(sliders[0].onValueChanged, settings.SetMasterVol);
            UnityEventTools.AddPersistentListener<float>(sliders[1].onValueChanged, settings.SetMusicVol);
            UnityEventTools.AddPersistentListener<float>(sliders[2].onValueChanged, settings.SetSFXVol);
            UnityEventTools.AddPersistentListener<float>(sliders[3].onValueChanged, settings.SetVoiceVol);

            Text audioNote = AddText(rightCol.rectTransform, "AudioHint",
                "Ajustes de áudio são pré-visualizados em tempo real.\nClique em APLICAR para salvar permanentemente.",
                18, IvoryMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
            SetLayoutElement(audioNote.gameObject, preferredHeight: 58f);

            // Two-column CONTROLES tab (JOGADOR 1 | JOGADOR 2 — 9 rebindable actions)
            RectTransform controlsBody = NewRect("ControlsColumns", box.rectTransform);
            Region(controlsBody, 0.025f, 0.148f, 0.975f, 0.802f);
            var ctrlHlg = controlsBody.gameObject.AddComponent<HorizontalLayoutGroup>();
            ctrlHlg.padding = new RectOffset(8, 8, 6, 6);
            ctrlHlg.spacing = 24f;
            ctrlHlg.childControlWidth = true;
            ctrlHlg.childControlHeight = true;
            ctrlHlg.childForceExpandWidth = false;
            ctrlHlg.childForceExpandHeight = true;

            Button[] p1KeyBtns = BuildKeybindColumn(controlsBody, "P1ControlsColumn", "JOGADOR 1 (P1)", FaithBlue,   playerIdx: 0, settings);
            Image ctrlSep = AddImage(controlsBody, "ControlsColumnDivider", new Color(0.84f, 0.67f, 0.27f, 0.35f), null, false);
            SetLayoutElement(ctrlSep.gameObject, preferredWidth: 2f);
            Button[] p2KeyBtns = BuildKeybindColumn(controlsBody, "P2ControlsColumn", "JOGADOR 2 (P2)", IdeoCrimson, playerIdx: 1, settings);
            controlsBody.gameObject.SetActive(false);

            // Bottom divider & Action bar (RESET | VOLTAR | APLICAR)
            AddDivider(box.rectTransform, "FooterDivider", AntiqueGold, 0.04f, 0.132f, 0.96f, 0.146f);

            RectTransform footer = NewRect("FooterActions", box.rectTransform);
            Region(footer, 0.025f, 0.024f, 0.975f, 0.124f);
            var footHlg = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footHlg.padding = new RectOffset(16, 16, 6, 6);
            footHlg.spacing = 18f;
            footHlg.childAlignment = TextAnchor.MiddleCenter;
            footHlg.childControlWidth = true;
            footHlg.childControlHeight = true;
            footHlg.childForceExpandWidth = false;
            footHlg.childForceExpandHeight = true;

            Button reset = AddButton(footer, "Btn_Reset", "RESET", 26, ButtonVariant.SecondaryDanger);
            SetLayoutElement(reset.gameObject, preferredWidth: 210f);
            UnityEventTools.AddPersistentListener(reset.onClick, settings.ResetToDefaults);

            RectTransform spacer = NewRect("Spacer", footer);
            SetLayoutElement(spacer.gameObject, flexibleWidth: 1f);

            Button back = AddButton(footer, "Btn_Voltar", "VOLTAR", 26, ButtonVariant.Default);
            SetLayoutElement(back.gameObject, preferredWidth: 230f);
            UnityEventTools.AddPersistentListener(back.onClick, settings.Discard);
            UnityEventTools.AddPersistentListener(back.onClick, title.ReturnToTitle);

            Button apply = AddButton(footer, "Btn_Aplicar", "APLICAR", 28, ButtonVariant.PrimaryCrimson);
            SetLayoutElement(apply.gameObject, preferredWidth: 260f);
            UnityEventTools.AddPersistentListener(apply.onClick, settings.Apply);

            // SettingsManager wiring
            settings.tabGeneralButton     = tabGeral;
            settings.tabControlsButton    = tabControles;
            settings.generalTabRoot       = body.gameObject;
            settings.controlsTabRoot      = controlsBody.gameObject;
            settings.subHeadingLabel      = subHead;
            settings.p1KeyButtons         = p1KeyBtns;
            settings.p2KeyButtons         = p2KeyBtns;
            settings.qualityButtons       = qBtns;
            settings.vsyncToggle          = vsync;
            settings.fullscreenToggle     = full;
            settings.prevResolutionButton = resPrev;
            settings.nextResolutionButton = resNext;
            settings.resolutionLabel      = resText;
            settings.masterSlider         = sliders[0];
            settings.musicSlider          = sliders[1];
            settings.sfxSlider            = sliders[2];
            settings.voiceSlider          = sliders[3];
            EditorUtility.SetDirty(settings);

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static Button[] BuildKeybindColumn(RectTransform parent, string name, string heading, Color accent, int playerIdx, SettingsManager settings)
        {
            Image col = AddFramedPanel(parent, name, CardSurface, MutedGold, false);
            var colLe = col.gameObject.AddComponent<LayoutElement>();
            colLe.flexibleWidth = 1.0f;

            var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 14, 14);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            Text sec = AddText(col.rectTransform, "SectionHeading", heading, 23, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetLayoutElement(sec.gameObject, preferredHeight: 30f);
            AddLayoutDivider(col.rectTransform, "Divider", AntiqueGold);

            var btns = new Button[KeybindManager.ActionCount];
            for (int a = 0; a < KeybindManager.ActionCount; a++)
            {
                var action = (FighterAction)a;
                Image row = AddFramedPanel(col.rectTransform, "BindRow_" + action, SlotDark, new Color(0.56f, 0.44f, 0.18f, 0.45f), false);
                SetLayoutElement(row.gameObject, preferredHeight: 46f);

                var rhlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rhlg.padding = new RectOffset(14, 10, 4, 4);
                rhlg.spacing = 12f;
                rhlg.childAlignment = TextAnchor.MiddleCenter;
                rhlg.childControlWidth = true;
                rhlg.childControlHeight = true;
                rhlg.childForceExpandWidth = false;
                rhlg.childForceExpandHeight = true;

                Text actionLbl = AddText(row.rectTransform, "ActionLabel", SettingsManager.ActionLabelsPT[a], 19, Ivory, TextAnchor.MiddleLeft, FontStyle.Bold);
                SetLayoutElement(actionLbl.gameObject, flexibleWidth: 1f);

                string defKey = KeybindManager.GetPrettyKeyName(KeybindManager.GetDefaultKey(playerIdx, action));
                Button keyBtn = AddButton(row.rectTransform, "KeyBtn_" + action, defKey, 18, ButtonVariant.Compact);
                keyBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                SetLayoutElement(keyBtn.gameObject, preferredWidth: 230f);

                var kTxt = keyBtn.GetComponentInChildren<Text>();
                if (kTxt)
                {
                    kTxt.resizeTextForBestFit = true;
                    kTxt.resizeTextMinSize    = 12;
                    kTxt.resizeTextMaxSize    = 18;
                }

                if (playerIdx == 0) UnityEventTools.AddIntPersistentListener(keyBtn.onClick, settings.StartRebindP1, a);
                else                UnityEventTools.AddIntPersistentListener(keyBtn.onClick, settings.StartRebindP2, a);

                btns[a] = keyBtn;
            }
            return btns;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  CharSelect
        // ═════════════════════════════════════════════════════════════════════
        static void BuildCharSelect()
        {
            EnsureUISprites();
            Scene scene = OpenScene("CharSelect");
            ClearGenerated(scene);
            SetupCamera();
            var mgr = GetOrCreateManager<CharacterSelectManager>("CharacterSelectManager");
            RectTransform root = CreateCanvas();

            Image bg = AddImage(root, "Background", Color.white, LoadSprite("Backgrounds/BG_CharSelect.jpg"), false);
            Stretch(bg.rectTransform);
            Image dim = AddImage(root, "Dim", new Color(0.02f, 0.01f, 0.04f, 0.56f), null, false);
            Stretch(dim.rectTransform);

            // Framed top banner
            Image banner = AddFramedPanel(root, "HeaderBanner", BurgundyPanel, AntiqueGold, false);
            Region(banner.rectTransform, 0.14f, 0.87f, 0.86f, 0.97f);
            Text header = AddText(banner.rectTransform, "Header", "ESCOLHA SEU LUTADOR", 52, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(header.rectTransform);
            AddStrongTextEffects(header, 2.5f, 3.5f);

            // Roster (0 = Agostinho, 1 = Marx)
            mgr.characters = new List<CharacterData>
            {
                new CharacterData
                {
                    characterName = "Santo Agostinho",
                    description   = "Bispo de Hipona e Doutor da Igreja.\nLuta com a força da fé e a luz da graça divina.",
                    faction       = "Faith",
                    portrait      = LoadSprite("Characters/Char_A1_SantoAgostinho.png"),
                    unlocked      = true
                },
                new CharacterData
                {
                    characterName = "Karl Marx",
                    description   = "Filósofo e revolucionário.\nLuta com a foice e o martelo da luta de classes.",
                    faction       = "Ideology",
                    portrait      = LoadSprite("Characters/Char_B1_KarlMarx.png"),
                    unlocked      = true
                }
            };

            mgr.buttonsA = BuildFighterCard(root, "PlayerCardA", "JOGADOR 1  ·  ORDEM DA FÉ",
                "ESTILO: LUZ SAGRADA & CONTRA-ATAQUE", 0.05f, 0.44f, FaithBlue, mgr, true,
                out mgr.portraitA, out mgr.nameA, out mgr.descA, out mgr.tagTextA);
            mgr.buttonsB = BuildFighterCard(root, "PlayerCardB", "CPU 2  ·  VANGUARDA IDEOLÓGICA",
                "ESTILO: PRESSÃO & LONGO ALCANCE", 0.56f, 0.95f, IdeoCrimson, mgr, false,
                out mgr.portraitB, out mgr.nameB, out mgr.descB, out mgr.tagTextB);

            BuildControlModeBar(root, "ControlBarA", 0.05f, 0.44f, mgr, isA: true,
                out mgr.p1HumanBtn, out mgr.p1AIBtn);
            BuildControlModeBar(root, "ControlBarB", 0.56f, 0.95f, mgr, isA: false,
                out mgr.p2HumanBtn, out mgr.p2AIBtn);
            mgr.p1IsAI = false;
            mgr.p2IsAI = true;

            // Ornate VS medallion
            Image vsCrest = AddFramedPanel(root, "VSCrest", BurgundyPanel, BrightGold, false);
            Region(vsCrest.rectTransform, 0.452f, 0.43f, 0.548f, 0.58f);
            Text vs = AddText(vsCrest.rectTransform, "VS", "VS", 64, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(vs.rectTransform);
            AddStrongTextEffects(vs, 3f, 4f);

            // Status strip & buttons
            Image statusBar = AddFramedPanel(root, "StatusStrip", ObsidianPanel, MutedGold, false);
            Region(statusBar.rectTransform, 0.24f, 0.125f, 0.76f, 0.175f);
            mgr.confirmText = AddText(statusBar.rectTransform, "ConfirmText", "Selecione os lutadores e confirme", 26, BrightGold, TextAnchor.MiddleCenter, FontStyle.Italic);
            Stretch(mgr.confirmText.rectTransform);

            Button confirm = AddButton(root, "Btn_Confirmar", "CONFIRMAR", 36, ButtonVariant.PrimaryCrimson);
            Region(confirm.GetComponent<RectTransform>(), 0.37f, 0.028f, 0.63f, 0.112f);
            UnityEventTools.AddPersistentListener(confirm.onClick, mgr.OnConfirm);

            Button back = AddButton(root, "Btn_Voltar", "VOLTAR", 28, ButtonVariant.Default);
            Region(back.GetComponent<RectTransform>(), 0.045f, 0.032f, 0.185f, 0.105f);
            UnityEventTools.AddPersistentListener(back.onClick, mgr.OnBack);

            mgr.titleManager = null;
            mgr.nextScene = "MapSelect";
            mgr.backScene = "TitleScreen";
            EditorUtility.SetDirty(mgr);

            SaveScene(scene);
            Debug.Log("[MenuSceneBuilder] CharSelect built.");
        }

        static Button[] BuildFighterCard(RectTransform root, string name, string tag, string styleBadge,
            float x0, float x1, Color accent, CharacterSelectManager mgr, bool isA,
            out Image portrait, out Text nameText, out Text descText, out Text tagText)
        {
            Image card = AddFramedPanel(root, name, CardSurface, accent, true);
            Region(card.rectTransform, x0, 0.252f, x1, 0.850f);

            // Top faction header strip
            Image tagBar = AddFramedPanel(card.rectTransform, "TagBar",
                isA ? new Color(0.08f, 0.16f, 0.28f, 0.94f) : BurgundyPanel, accent, false);
            Region(tagBar.rectTransform, 0.03f, 0.90f, 0.97f, 0.975f);
            tagText = AddText(tagBar.rectTransform, "Tag", tag, 22, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(tagText.rectTransform);

            // Recessed portrait frame
            Image portraitSlot = AddFramedPanel(card.rectTransform, "PortraitFrame", SlotDark, MutedGold, false);
            Region(portraitSlot.rectTransform, 0.05f, 0.41f, 0.95f, 0.885f);
            portrait = AddImage(portraitSlot.rectTransform, "Portrait", Color.white, null, false);
            Region(portrait.rectTransform, 0.03f, 0.03f, 0.97f, 0.97f);
            portrait.preserveAspect = true;

            // Name + divider + bio
            nameText = AddText(card.rectTransform, "Name", "—", 40, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(nameText.rectTransform, 0.05f, 0.31f, 0.95f, 0.40f);
            nameText.resizeTextForBestFit = true; nameText.resizeTextMinSize = 20; nameText.resizeTextMaxSize = 42;
            AddStrongTextEffects(nameText, 2f, 3f);

            AddDivider(card.rectTransform, "CardDivider", AntiqueGold, 0.08f, 0.295f, 0.92f, 0.312f);

            descText = AddText(card.rectTransform, "Description", "", 22, Ivory, TextAnchor.UpperCenter);
            Region(descText.rectTransform, 0.06f, 0.185f, 0.94f, 0.292f);

            Text badge = AddText(card.rectTransform, "StyleBadge", styleBadge, 17, AntiqueGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(badge.rectTransform, 0.06f, 0.135f, 0.94f, 0.185f);

            // Roster buttons via HorizontalLayoutGroup
            RectTransform pickRow = NewRect("PickRow", card.rectTransform);
            Region(pickRow, 0.04f, 0.025f, 0.96f, 0.125f);
            var hlg = pickRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            int n = mgr.characters.Count;
            var pickButtons = new Button[n];
            for (int i = 0; i < n; i++)
            {
                Button b = AddButton(pickRow, "Pick_" + mgr.characters[i].characterName.Replace(" ", ""),
                    mgr.characters[i].characterName.ToUpper(), 22, ButtonVariant.Compact);
                pickButtons[i] = b;
                if (isA) UnityEventTools.AddIntPersistentListener(b.onClick, mgr.SelectA, i);
                else     UnityEventTools.AddIntPersistentListener(b.onClick, mgr.SelectB, i);
            }
            return pickButtons;
        }

        static void BuildControlModeBar(RectTransform root, string name, float x0, float x1,
            CharacterSelectManager mgr, bool isA, out Button humanBtn, out Button aiBtn)
        {
            Image bar = AddFramedPanel(root, name, ObsidianPanel, MutedGold, true);
            Region(bar.rectTransform, x0, 0.184f, x1, 0.242f);

            var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding                = new RectOffset(10, 10, 6, 6);
            hlg.spacing                = 10f;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;

            humanBtn = AddButton(bar.rectTransform, "Btn_Jogador", "JOGADOR", 20, ButtonVariant.Compact);
            aiBtn    = AddButton(bar.rectTransform, "Btn_IA",      "IA",      20, ButtonVariant.Compact);

            if (isA)
            {
                UnityEventTools.AddBoolPersistentListener(humanBtn.onClick, mgr.SetP1IsAI, false);
                UnityEventTools.AddBoolPersistentListener(aiBtn.onClick,    mgr.SetP1IsAI, true);
                humanBtn.GetComponent<UIButtonJuice>()?.SetSelected(true);
                aiBtn.GetComponent<UIButtonJuice>()?.SetSelected(false);
            }
            else
            {
                UnityEventTools.AddBoolPersistentListener(humanBtn.onClick, mgr.SetP2IsAI, false);
                UnityEventTools.AddBoolPersistentListener(aiBtn.onClick,    mgr.SetP2IsAI, true);
                humanBtn.GetComponent<UIButtonJuice>()?.SetSelected(false);
                aiBtn.GetComponent<UIButtonJuice>()?.SetSelected(true);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MapSelect
        // ═════════════════════════════════════════════════════════════════════
        static void BuildMapSelect()
        {
            EnsureUISprites();
            Scene scene = OpenScene("MapSelect");
            ClearGenerated(scene);
            SetupCamera();
            var mgr = GetOrCreateManager<MapSelectManager>("MapSelectManager");
            RectTransform root = CreateCanvas();

            Image bg = AddImage(root, "Background", Color.white, LoadSprite("Backgrounds/BG_CharSelect_Dark.jpg"), false);
            Stretch(bg.rectTransform);
            Image dim = AddImage(root, "Dim", new Color(0.02f, 0.01f, 0.04f, 0.50f), null, false);
            Stretch(dim.rectTransform);

            // Top banner
            Image banner = AddFramedPanel(root, "HeaderBanner", BurgundyPanel, AntiqueGold, false);
            Region(banner.rectTransform, 0.14f, 0.87f, 0.86f, 0.97f);
            Text header = AddText(banner.rectTransform, "Header", "ESCOLHA O CAMPO DE BATALHA", 48, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(header.rectTransform);
            AddStrongTextEffects(header, 2.5f, 3.5f);

            // VS matchup bar
            Image vsBar = AddFramedPanel(root, "VSBar", ObsidianPanel, AntiqueGold, false);
            Region(vsBar.rectTransform, 0.22f, 0.76f, 0.78f, 0.845f);

            mgr.nameA = AddText(vsBar.rectTransform, "NameA", "JOGADOR 1", 36, FaithBlue, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(mgr.nameA.rectTransform, 0.03f, 0f, 0.42f, 1f);
            Text vs = AddText(vsBar.rectTransform, "VS", "VS", 42, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(vs.rectTransform, 0.42f, 0f, 0.58f, 1f);
            mgr.nameB = AddText(vsBar.rectTransform, "NameB", "JOGADOR 2", 36, IdeoCrimson, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(mgr.nameB.rectTransform, 0.58f, 0f, 0.97f, 1f);
            mgr.nameA.resizeTextForBestFit = true; mgr.nameA.resizeTextMinSize = 18; mgr.nameA.resizeTextMaxSize = 36;
            mgr.nameB.resizeTextForBestFit = true; mgr.nameB.resizeTextMinSize = 18; mgr.nameB.resizeTextMaxSize = 36;

            // Stage cards
            mgr.bgExterior = LoadSprite("Backgrounds/BG_MapExterior.jpg");
            mgr.bgInterior = LoadSprite("Backgrounds/BG_MapInterior.jpg");
            Button ext = BuildMapCard(root, "CardExterior", "NAVE CENTRAL",  "Corredor dos Bancos",
                mgr.bgExterior, 0.06f, 0.48f, out mgr.cardExterior);
            Button inn = BuildMapCard(root, "CardInterior", "ALTAR SAGRADO", "Área do Púlpito",
                mgr.bgInterior, 0.52f, 0.94f, out mgr.cardInterior);
            UnityEventTools.AddPersistentListener(ext.onClick, mgr.SelectExterior);
            UnityEventTools.AddPersistentListener(inn.onClick, mgr.SelectInterior);

            // Go / Back
            mgr.goBtn = AddButton(root, "Btn_IniciarLuta", "INICIAR LUTA", 36, ButtonVariant.PrimaryCrimson);
            Region(mgr.goBtn.GetComponent<RectTransform>(), 0.37f, 0.032f, 0.63f, 0.125f);
            UnityEventTools.AddPersistentListener(mgr.goBtn.onClick, mgr.OnConfirm);

            Button back = AddButton(root, "Btn_Voltar", "VOLTAR", 28, ButtonVariant.Default);
            Region(back.GetComponent<RectTransform>(), 0.045f, 0.035f, 0.185f, 0.11f);
            UnityEventTools.AddPersistentListener(back.onClick, mgr.OnBack);

            // Fight intro overlay
            RectTransform fo = NewRect("FightOverlay", root); Stretch(fo);
            mgr.fightOverlay = fo.gameObject.AddComponent<CanvasGroup>();
            mgr.fightOverlay.alpha = 0f;
            Image foBg = AddImage(fo, "Background", new Color(0.02f, 0.01f, 0.04f, 0.96f), null, true);
            Stretch(foBg.rectTransform);
            Image foFrame = AddFramedPanel(fo, "IntroFrame", BurgundyPanel, BrightGold, false);
            Region(foFrame.rectTransform, 0.12f, 0.25f, 0.88f, 0.75f);
            Text pre = AddText(foFrame.rectTransform, "Prepare", "PREPARE-SE PARA A LUTA", 34, IvoryMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
            Region(pre.rectTransform, 0.06f, 0.72f, 0.94f, 0.90f);
            mgr.fightMapText = AddText(foFrame.rectTransform, "MapText", "", 76, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(mgr.fightMapText.rectTransform, 0.04f, 0.36f, 0.96f, 0.72f);
            AddStrongTextEffects(mgr.fightMapText, 3f, 4f);
            AddDivider(foFrame.rectTransform, "IntroDivider", AntiqueGold, 0.15f, 0.29f, 0.85f, 0.34f);
            mgr.fightNamesText = AddText(foFrame.rectTransform, "NamesText", "", 44, Ivory, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(mgr.fightNamesText.rectTransform, 0.05f, 0.08f, 0.95f, 0.28f);
            fo.gameObject.SetActive(false);

            Image fade = AddImage(root, "FadeOverlay", new Color(0f, 0f, 0f, 0f), null, false);
            Stretch(fade.rectTransform);
            mgr.fadeOverlay = fade;

            mgr.titleManager = null;
            mgr.backScene = "CharSelect";
            EditorUtility.SetDirty(mgr);

            SaveScene(scene);
            Debug.Log("[MenuSceneBuilder] MapSelect built.");
        }

        static Button BuildMapCard(RectTransform root, string name, string label, string subtitle, Sprite sprite,
            float x0, float x1, out Image cardImage)
        {
            cardImage = AddImage(root, name, Color.white, sprite, true);
            Region(cardImage.rectTransform, x0, 0.175f, x1, 0.735f);
            AddOutline(cardImage, MutedGold, 2f);

            // Ornate border frame overlaid cleanly above the artwork
            Image frame = AddImage(cardImage.rectTransform, "OrnateFrame", MutedGold, sprFrameOrnate, false);
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform);

            var btn = cardImage.gameObject.AddComponent<Button>();
            btn.targetGraphic = cardImage;
            btn.transition = Selectable.Transition.None; // MapSelectManager.HighlightCard + UIButtonJuice handle states
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            Image strip = AddImage(cardImage.rectTransform, "LabelBar", new Color(0.04f, 0.02f, 0.06f, 0.88f), sprPanel, false);
            strip.type = Image.Type.Sliced;
            Region(strip.rectTransform, 0.02f, 0.02f, 0.98f, 0.22f);

            Text t = AddText(strip.rectTransform, "Label", label, 36, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(t.rectTransform, 0.04f, 0.36f, 0.96f, 0.95f);
            AddStrongTextEffects(t, 2f, 3f);

            Text sub = AddText(strip.rectTransform, "Subtitle", subtitle, 20, IvoryMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
            Region(sub.rectTransform, 0.04f, 0.05f, 0.96f, 0.40f);

            var juice = cardImage.gameObject.AddComponent<UIButtonJuice>();
            juice.driveBackgroundColor = false; // keep stage artwork brightness owned by MapSelectManager.HighlightCard
            juice.targetBackground     = null;
            juice.targetFrame          = frame;
            juice.targetLabel          = t;
            juice.driveLabelColor      = false;
            juice.normalFrame      = MutedGold;
            juice.hoverFrame       = AntiqueGold;
            juice.selectedFrame    = BrightGold;
            juice.hoverScale       = 1.015f;
            juice.selectedScale    = 1.025f;
            juice.pressedScale     = 0.985f;
            return btn;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FightScene HUD (EcclesiaFighters ▸ Menus ▸ Rebuild Fight HUD)
        // ═════════════════════════════════════════════════════════════════════
        static void BuildFightHud()
        {
            Scene scene = OpenScene("FightScene");
            var mgr = UnityEngine.Object.FindObjectOfType<FightSceneManager>();
            if (!mgr)
            {
                Debug.LogError("[MenuSceneBuilder] FightSceneManager not found in FightScene.");
                return;
            }

            // Rescue any non-UI scene lights living inside Canvas (e.g., Light (1)) before replacing Canvas
            Canvas oldCanvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (oldCanvas)
            {
                foreach (Light l in oldCanvas.GetComponentsInChildren<Light>(true))
                {
                    Vector3 wp = l.transform.position;
                    Quaternion wr = l.transform.rotation;
                    l.transform.SetParent(null, false);
                    l.transform.SetPositionAndRotation(wp, wr);
                    l.transform.localScale = Vector3.one;
                }
                UnityEngine.Object.DestroyImmediate(oldCanvas.gameObject);
            }

            foreach (EventSystem existingEs in UnityEngine.Object.FindObjectsOfType<EventSystem>())
                UnityEngine.Object.DestroyImmediate(existingEs.gameObject);

            RectTransform root = CreateCanvas();

            // ── HUD Container (fades in after ROUND 1 / FIGHT!) ──
            RectTransform hudRt = NewRect("HUD", root); Stretch(hudRt);
            CanvasGroup hudGroup = hudRt.gameObject.AddComponent<CanvasGroup>();

            // Top Arcade Bar
            RectTransform topBar = NewRect("TopBar", hudRt);
            Region(topBar, 0.025f, 0.815f, 0.975f, 0.975f);

            // P1 Health Cluster (Left → drains toward center timer)
            Slider hpA = BuildArcadeHealthBar(topBar, "P1Cluster", "SANTO AGOSTINHO", FaithBlue,
                new Color(0.86f, 0.68f, 0.24f, 1f), 0f, 0.435f, true, Mathf.Max(1, mgr.roundsToWin),
                out Text nameA, out Text winsA);

            // Center Timer Badge
            Image timerBadge = AddFramedPanel(topBar, "TimerBadge", BurgundyPanel, BrightGold, false);
            Region(timerBadge.rectTransform, 0.445f, 0.06f, 0.555f, 0.96f);
            Text timerSub = AddText(timerBadge.rectTransform, "TimerLabel", "TEMPO", 16, AntiqueGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(timerSub.rectTransform, 0.05f, 0.68f, 0.95f, 0.94f);
            Text timerTxt = AddText(timerBadge.rectTransform, "TimerText", Mathf.CeilToInt(mgr.roundTime).ToString(),
                54, Ivory, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(timerTxt.rectTransform, 0.05f, 0.04f, 0.95f, 0.72f);
            AddStrongTextEffects(timerTxt, 2.5f, 3f);

            // P2 Health Cluster (Right → drains toward center timer)
            Slider hpB = BuildArcadeHealthBar(topBar, "P2Cluster", "KARL MARX", IdeoCrimson,
                Crimson, 0.565f, 1f, false, Mathf.Max(1, mgr.roundsToWin),
                out Text nameB, out Text winsB);

            // Bottom-left Toggleable Controls Legend
            BuildControlsLegend(hudRt);

            // CenterText — placed as SIBLING of HUD so announcements show while HUD alpha is 0 during RoundIntro
            Text centerTxt = AddText(root, "CenterText", "ROUND 1", 112, BrightGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(centerTxt.rectTransform, 0.08f, 0.34f, 0.92f, 0.66f);
            centerTxt.resizeTextForBestFit = true; centerTxt.resizeTextMinSize = 42; centerTxt.resizeTextMaxSize = 116;
            AddStrongTextEffects(centerTxt, 4f, 5f);
            centerTxt.gameObject.SetActive(false);

            // FadeOverlay — always last, raycastTarget = false
            Image fade = AddImage(root, "FadeOverlay", new Color(0f, 0f, 0f, 0f), null, false);
            Stretch(fade.rectTransform);

            // Rewire ONLY the HUD references on FightSceneManager
            mgr.hpBarA      = hpA;
            mgr.hpBarB      = hpB;
            mgr.nameTagA    = nameA;
            mgr.nameTagB    = nameB;
            mgr.timerText   = timerTxt;
            mgr.winsA_icons = winsA;
            mgr.winsB_icons = winsB;
            mgr.centerText  = centerTxt;
            mgr.hud         = hudGroup;
            mgr.fadeOverlay = fade;

            SetupFightArena3D(mgr);
            EditorUtility.SetDirty(mgr);

            SaveScene(scene);
            Debug.Log("[MenuSceneBuilder] FightScene HUD rebuilt.");
        }

        static void SetupFightArena3D(FightSceneManager mgr)
        {
            // Hide legacy flat 2D background plane
            SpriteRenderer legacyBg = mgr.backgroundRenderer;
            if (!legacyBg)
            {
                GameObject bgGo = GameObject.Find("Background");
                if (bgGo) legacyBg = bgGo.GetComponent<SpriteRenderer>();
            }
            if (legacyBg) legacyBg.gameObject.SetActive(false);

            // Configure main camera with FighterCamera
            Camera cam = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
            if (!cam)
            {
                var camGo = new GameObject("Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.03f, 0.06f, 1f);
            cam.nearClipPlane   = 0.2f;

            FighterCamera fcam = cam.GetComponent<FighterCamera>() ?? cam.gameObject.AddComponent<FighterCamera>();

            // Create / reuse ChurchStageController
            ChurchStageController stageCtrl = mgr.stageController ?? UnityEngine.Object.FindObjectOfType<ChurchStageController>();
            if (!stageCtrl)
            {
                var stageGo = new GameObject("ChurchStageController");
                stageCtrl = stageGo.AddComponent<ChurchStageController>();
            }

            stageCtrl.churchModelAsset         = AssetDatabase.LoadAssetAtPath<GameObject>(ChurchStageController.DefaultChurchAssetPath);
            stageCtrl.legacyBackgroundRenderer = legacyBg;
            stageCtrl.fighterCamera            = fcam;
            if (mgr.spawnA) stageCtrl.spawnA   = mgr.spawnA;
            if (mgr.spawnB) stageCtrl.spawnB   = mgr.spawnB;

            stageCtrl.InitializeDefaultPresets(force: false);
            stageCtrl.EnsureArenaObjects();
            stageCtrl.ApplyPreset(stageCtrl.activePresetIndex);

            mgr.stageController = stageCtrl;
            mgr.fighterCamera   = fcam;
            if (stageCtrl.spawnA) mgr.spawnA = stageCtrl.spawnA;
            if (stageCtrl.spawnB) mgr.spawnB = stageCtrl.spawnB;

            EditorUtility.SetDirty(fcam);
            EditorUtility.SetDirty(stageCtrl);
            EditorUtility.SetDirty(mgr);
        }

        static Slider BuildArcadeHealthBar(RectTransform parent, string rootName, string defaultName,
            Color accent, Color fillColor, float x0, float x1, bool isLeft, int roundsToWin,
            out Text nameTag, out Text winsIcons)
        {
            RectTransform cluster = NewRect(rootName, parent);
            Region(cluster, x0, 0f, x1, 1f);

            // Nameplate + Win Pips row
            Image namePlate = AddFramedPanel(cluster, "NamePlate", ObsidianPanel, MutedGold, false);
            Region(namePlate.rectTransform, 0f, 0.55f, 1f, 0.96f);

            string pipsDefault = new string('○', roundsToWin);
            if (isLeft)
            {
                nameTag = AddText(namePlate.rectTransform, "NameTag", defaultName, 30, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                Region(nameTag.rectTransform, 0.03f, 0f, 0.72f, 1f);
                winsIcons = AddText(namePlate.rectTransform, "WinsIcons", pipsDefault, 30, BrightGold, TextAnchor.MiddleRight, FontStyle.Bold);
                Region(winsIcons.rectTransform, 0.72f, 0f, 0.97f, 1f);
            }
            else
            {
                winsIcons = AddText(namePlate.rectTransform, "WinsIcons", pipsDefault, 30, BrightGold, TextAnchor.MiddleLeft, FontStyle.Bold);
                Region(winsIcons.rectTransform, 0.03f, 0f, 0.28f, 1f);
                nameTag = AddText(namePlate.rectTransform, "NameTag", defaultName, 30, accent, TextAnchor.MiddleRight, FontStyle.Bold);
                Region(nameTag.rectTransform, 0.28f, 0f, 0.97f, 1f);
            }
            nameTag.resizeTextForBestFit = true; nameTag.resizeTextMinSize = 18; nameTag.resizeTextMaxSize = 32;

            // Metallic / Gold Framed Health Slider
            RectTransform barRt = NewRect(isLeft ? "HPBarA" : "HPBarB", cluster);
            Region(barRt, 0f, 0.08f, 1f, 0.50f);

            Image trough = AddImage(barRt, "Background", SlotDark, sprPanel, false);
            trough.type = Image.Type.Sliced;
            Stretch(trough.rectTransform);

            RectTransform fillArea = NewRect("Fill Area", barRt);
            fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(5f, 5f); fillArea.offsetMax = new Vector2(-5f, -5f);

            Image fill = AddImage(fillArea, "Fill", fillColor, sprPanel, false);
            fill.type = Image.Type.Sliced;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;

            Image sheen = AddImage(fill.rectTransform, "Sheen", Color.white, sprSheen, false);
            sheen.type = Image.Type.Sliced;
            Stretch(sheen.rectTransform);

            Image frame = AddImage(barRt, "MetallicFrame", AntiqueGold, sprFrameOrnate, false);
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform);

            var slider = barRt.gameObject.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fill.rectTransform;
            slider.handleRect = null;
            slider.direction = isLeft ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            return slider;
        }

        static void BuildControlsLegend(RectTransform hudRt)
        {
            RectTransform legendRt = NewRect("ControlsLegend", hudRt);
            Place(legendRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 22f), new Vector2(660f, 122f));

            var vlg = legendRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = legendRt.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header toggle button
            Button headerBtn = AddButton(legendRt, "LegendHeaderBtn", "[TAB] OCULTAR  ·  [ESC] PAUSAR", 16, ButtonVariant.Compact);
            SetLayoutElement(headerBtn.gameObject, preferredHeight: 30f);
            Text headerTxt = headerBtn.GetComponentInChildren<Text>();
            if (headerTxt)
            {
                headerTxt.resizeTextForBestFit = true;
                headerTxt.resizeTextMinSize    = 11;
                headerTxt.resizeTextMaxSize    = 16;
            }

            // Body panel with Portuguese moveset legend (synced dynamically at runtime from KeybindManager)
            Image body = AddFramedPanel(legendRt, "LegendContent", new Color(0.04f, 0.03f, 0.06f, 0.86f), MutedGold, false);
            SetLayoutElement(body.gameObject, preferredHeight: 80f);
            var bodyVlg = body.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyVlg.padding = new RectOffset(16, 16, 10, 10);
            bodyVlg.spacing = 6f;
            bodyVlg.childAlignment = TextAnchor.MiddleLeft;
            bodyVlg.childControlWidth = true;
            bodyVlg.childControlHeight = true;
            bodyVlg.childForceExpandWidth = true;
            bodyVlg.childForceExpandHeight = false;

            Text line1 = AddText(body.rectTransform, "MoveControls",
                "A/D Mover · W Pular · S Agachar · SHIFT ESQ Defender",
                15, Ivory, TextAnchor.MiddleLeft, FontStyle.Bold);
            line1.resizeTextForBestFit = true;
            line1.resizeTextMinSize    = 10;
            line1.resizeTextMaxSize    = 15;
            line1.horizontalOverflow   = HorizontalWrapMode.Wrap;
            line1.verticalOverflow     = VerticalWrapMode.Truncate;
            SetLayoutElement(line1.gameObject, preferredHeight: 26f).minHeight = 24f;

            Text line2 = AddText(body.rectTransform, "AttackControls",
                "F Soco Leve · G Soco Forte · H Chute · T Especial",
                15, BrightGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            line2.resizeTextForBestFit = true;
            line2.resizeTextMinSize    = 10;
            line2.resizeTextMaxSize    = 15;
            line2.horizontalOverflow   = HorizontalWrapMode.Wrap;
            line2.verticalOverflow     = VerticalWrapMode.Truncate;
            SetLayoutElement(line2.gameObject, preferredHeight: 26f).minHeight = 24f;

            var toggleComp = legendRt.gameObject.AddComponent<ControlsLegendToggle>();
            toggleComp.contentPanel   = body.gameObject;
            toggleComp.toggleHintText = headerTxt;
            toggleComp.toggleKey      = KeyCode.Tab;
            toggleComp.startExpanded  = true;

            UnityEventTools.AddPersistentListener(headerBtn.onClick, toggleComp.Toggle);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Procedural 9-Sliced UI Sprite Assets
        // ═════════════════════════════════════════════════════════════════════
        static void EnsureUISprites()
        {
            if (!Directory.Exists(UISpritesDir)) Directory.CreateDirectory(UISpritesDir);

            sprPanel       = CreateOrLoadSprite("UI_Panel.png",       64, 64, new Vector4(14, 14, 14, 14), DrawPanelPixel);
            sprFrameOrnate = CreateOrLoadSprite("UI_FrameOrnate.png", 64, 64, new Vector4(18, 18, 18, 18), DrawFrameOrnatePixel);
            sprPill        = CreateOrLoadSprite("UI_Pill.png",        64, 64, new Vector4(20, 20, 20, 20), DrawPillPixel);
            sprKnob        = CreateOrLoadSprite("UI_Knob.png",        48, 48, Vector4.zero,                DrawKnobPixel);
            sprSheen       = CreateOrLoadSprite("UI_Sheen.png",       32, 64, new Vector4(8, 8, 8, 8),     DrawSheenPixel);
            sprDivider     = CreateOrLoadSprite("UI_Divider.png",    128, 16, Vector4.zero,                DrawDividerPixel);
        }

        static Sprite CreateOrLoadSprite(string fileName, int w, int h, Vector4 border, Func<int, int, int, int, Color32> pixelFn)
        {
            string path = UISpritesDir + fileName;
            bool existed = File.Exists(path);

            if (!existed)
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        pixels[y * w + x] = pixelFn(x, y, w, h);
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = importer.textureType != TextureImporterType.Sprite
                          || importer.spriteImportMode != SpriteImportMode.Single
                          || importer.spriteBorder != border
                          || !importer.alphaIsTransparency
                          || importer.mipmapEnabled;
                if (dirty)
                {
                    importer.textureType         = TextureImporterType.Sprite;
                    importer.spriteImportMode    = SpriteImportMode.Single;
                    importer.spriteBorder        = border;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled       = false;
                    importer.wrapMode            = TextureWrapMode.Clamp;
                    importer.filterMode          = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static float RoundedRectSignedDist(float x, float y, float w, float h, float r)
        {
            float hx = w * 0.5f - r;
            float hy = h * 0.5f - r;
            float dx = Mathf.Max(Mathf.Abs(x - (w - 1) * 0.5f) - hx, 0f);
            float dy = Mathf.Max(Mathf.Abs(y - (h - 1) * 0.5f) - hy, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static Color32 DrawPanelPixel(int x, int y, int w, int h)
        {
            float d = RoundedRectSignedDist(x, y, w, h, 8f);
            float alpha = Mathf.Clamp01(0.5f - d);
            float grad = Mathf.Lerp(0.86f, 1.0f, (float)y / (h - 1));
            byte v = (byte)Mathf.RoundToInt(grad * 255f);
            return new Color32(v, v, v, (byte)Mathf.RoundToInt(alpha * 255f));
        }

        static Color32 DrawPillPixel(int x, int y, int w, int h)
        {
            float d = RoundedRectSignedDist(x, y, w, h, 18f);
            float alpha = Mathf.Clamp01(0.5f - d);
            float grad = Mathf.Lerp(0.88f, 1.0f, (float)y / (h - 1));
            byte v = (byte)Mathf.RoundToInt(grad * 255f);
            return new Color32(v, v, v, (byte)Mathf.RoundToInt(alpha * 255f));
        }

        static Color32 DrawFrameOrnatePixel(int x, int y, int w, int h)
        {
            float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
            float outer = (edge >= 0.5f && edge <= 2.5f) ? 1f : 0f;
            float inner = (edge >= 5.5f && edge <= 6.8f) ? 0.72f : 0f;

            // Corner filigree L-accents safely inside 18px 9-slice margin
            int cx = Mathf.Min(x, w - 1 - x);
            int cy = Mathf.Min(y, h - 1 - y);
            bool cornerBracket = (cx <= 13 && cy <= 13) && (cx <= 4 || cy <= 4) && (cx >= 1 && cy >= 1);
            bool cornerGem     = (cx + cy >= 8 && cx + cy <= 12 && cx >= 3 && cy >= 3 && cx <= 8 && cy <= 8);

            float a = Mathf.Max(Mathf.Max(outer, inner), cornerBracket ? 0.9f : (cornerGem ? 1f : 0f));
            byte c = (byte)(cornerGem || outer > 0.5f ? 255 : 225);
            return new Color32(c, c, c, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
        }

        static Color32 DrawKnobPixel(int x, int y, int w, int h)
        {
            float dx = x - (w - 1) * 0.5f;
            float dy = y - (h - 1) * 0.5f;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);
            float maxR = w * 0.5f - 2f;
            float alpha = Mathf.Clamp01(maxR - r + 0.8f);
            float shade = (r > maxR - 3.2f) ? 0.55f : Mathf.Lerp(0.82f, 1.0f, (dy + maxR) / (2f * maxR));
            byte v = (byte)Mathf.RoundToInt(shade * 255f);
            return new Color32(v, v, v, (byte)Mathf.RoundToInt(alpha * 255f));
        }

        static Color32 DrawSheenPixel(int x, int y, int w, int h)
        {
            float ny = (float)y / (h - 1);
            float a = ny > 0.5f ? Mathf.Lerp(0.02f, 0.24f, (ny - 0.5f) * 2f) : 0f;
            return new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }

        static Color32 DrawDividerPixel(int x, int y, int w, int h)
        {
            float nx = Mathf.Abs((x - (w - 1) * 0.5f) / (w * 0.5f));
            float ny = Mathf.Abs(y - (h - 1) * 0.5f);
            float lineAlpha = ny <= 1.2f ? Mathf.Clamp01(1f - nx) : 0f;

            // Center diamond ornament
            float dx = Mathf.Abs(x - (w - 1) * 0.5f);
            float dy = Mathf.Abs(y - (h - 1) * 0.5f);
            float diamond = Mathf.Clamp01(1f - (dx + dy) / 6.5f);
            float a = Mathf.Max(lineAlpha * 0.85f, diamond);
            return new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Scene Helpers
        // ═════════════════════════════════════════════════════════════════════
        static Scene OpenScene(string sceneName)
        {
            return EditorSceneManager.OpenScene(ScenesDir + sceneName + ".unity", OpenSceneMode.Single);
        }

        static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ClearGenerated(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == "Canvas" || go.name == "EventSystem" || go.name == "BGM")
                    UnityEngine.Object.DestroyImmediate(go);
        }

        static void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindObjectOfType<Camera>();
            if (cam == null)
            {
                var go = new GameObject("Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.031f, 0.02f, 0.039f, 1f);
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        }

        static T GetOrCreateManager<T>(string goName) where T : MonoBehaviour
        {
            T m = UnityEngine.Object.FindObjectOfType<T>();
            if (m == null)
            {
                var go = new GameObject(goName);
                m = go.AddComponent<T>();
            }
            return m;
        }

        static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetAsLastSibling();
            return go.GetComponent<RectTransform>();
        }

        static void AddBgm(string clipPath)
        {
            var go = new GameObject("BGM");
            var src = go.AddComponent<AudioSource>();
            src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (src.clip == null) Debug.LogWarning("[MenuSceneBuilder] Missing audio clip: " + clipPath);
            src.playOnAwake = true; src.loop = true; src.spatialBlend = 0f;
        }

        static Sprite LoadSprite(string relativePath)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + relativePath);
            if (s == null) Debug.LogWarning("[MenuSceneBuilder] Missing sprite: " + SpritesDir + relativePath);
            return s;
        }

        static AudioClip LoadClip(string fileName)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + fileName);
            if (c == null) Debug.LogWarning("[MenuSceneBuilder] Missing audio clip: " + AudioDir + fileName);
            return c;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  UI Factory Helpers
        // ═════════════════════════════════════════════════════════════════════
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Region(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static LayoutElement SetLayoutElement(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f,
            float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (preferredWidth  >= 0f) le.preferredWidth  = preferredWidth;
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
            if (flexibleWidth   >= 0f) le.flexibleWidth   = flexibleWidth;
            if (flexibleHeight  >= 0f) le.flexibleHeight  = flexibleHeight;
            return le;
        }

        static Image AddImage(RectTransform parent, string name, Color color, Sprite sprite = null, bool raycast = true)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        static Image AddFramedPanel(RectTransform parent, string name, Color fillColor, Color frameColor, bool raycast)
        {
            Image panel = AddImage(parent, name, fillColor, sprPanel, raycast);
            panel.type = Image.Type.Sliced;

            Image border = AddImage(panel.rectTransform, "Frame", frameColor, sprFrameOrnate, false);
            border.type = Image.Type.Sliced;
            Stretch(border.rectTransform);
            var le = border.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // border must hug panel perimeter and never consume LayoutGroup slots
            return panel;
        }

        static void AddDivider(RectTransform parent, string name, Color tint, float x0, float y0, float x1, float y1)
        {
            Image div = AddImage(parent, name, tint, sprDivider, false);
            Region(div.rectTransform, x0, y0, x1, y1);
        }

        static void AddLayoutDivider(RectTransform parent, string name, Color tint)
        {
            Image div = AddImage(parent, name, tint, sprDivider, false);
            SetLayoutElement(div.gameObject, preferredHeight: 12f);
        }

        static Text AddText(RectTransform parent, string name, string text, int size, Color color,
            TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UIFont;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            AddStrongTextEffects(t, 1.3f, 2f);
            return t;
        }

        static void AddShadow(Graphic g, float dist = 3f)
        {
            var s = g.GetComponent<Shadow>() ?? g.gameObject.AddComponent<Shadow>();
            s.effectColor = InkDark;
            s.effectDistance = new Vector2(dist, -dist);
        }

        static void AddOutline(Graphic g, Color color, float distance)
        {
            var o = g.GetComponent<Outline>() ?? g.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(distance, -distance);
        }

        static void AddStrongTextEffects(Text t, float outlineDist, float shadowDist)
        {
            AddOutline(t, InkDark, outlineDist);
            AddShadow(t, shadowDist);
        }

        static Button AddButton(RectTransform parent, string name, string label, int fontSize, ButtonVariant variant = ButtonVariant.Default)
        {
            Color normalBg, hoverBg, pressedBg, selectedBg, normalFrame, hoverFrame, selectedFrame, normalTxt, hoverTxt, selectedTxt;

            switch (variant)
            {
                case ButtonVariant.PrimaryCrimson:
                    normalBg      = CrimsonDark;
                    hoverBg       = Crimson;
                    pressedBg     = new Color(0.24f, 0.04f, 0.07f, 1f);
                    selectedBg    = Crimson;
                    normalFrame   = AntiqueGold;
                    hoverFrame    = BrightGold;
                    selectedFrame = BrightGold;
                    normalTxt     = Ivory;
                    hoverTxt      = BrightGold;
                    selectedTxt   = BrightGold;
                    break;

                case ButtonVariant.SecondaryDanger:
                    normalBg      = new Color(0.11f, 0.04f, 0.06f, 0.94f);
                    hoverBg       = new Color(0.24f, 0.06f, 0.09f, 0.98f);
                    pressedBg     = new Color(0.16f, 0.03f, 0.05f, 1f);
                    selectedBg    = new Color(0.24f, 0.06f, 0.09f, 0.98f);
                    normalFrame   = MutedGold;
                    hoverFrame    = IdeoCrimson;
                    selectedFrame = BrightGold;
                    normalTxt     = IvoryMuted;
                    hoverTxt      = Ivory;
                    selectedTxt   = BrightGold;
                    break;

                case ButtonVariant.Compact:
                    normalBg      = ObsidianPanel;
                    hoverBg       = new Color(0.15f, 0.11f, 0.20f, 0.98f);
                    pressedBg     = new Color(0.13f, 0.10f, 0.18f, 0.98f);
                    selectedBg    = new Color(0.88f, 0.70f, 0.24f, 1.00f);
                    normalFrame   = MutedGold;
                    hoverFrame    = BrightGold;
                    selectedFrame = BrightGold;
                    normalTxt     = Ivory;
                    hoverTxt      = BrightGold;
                    selectedTxt   = new Color(0.05f, 0.03f, 0.02f, 1f);
                    break;

                default:
                    normalBg      = ObsidianPanel;
                    hoverBg       = new Color(0.16f, 0.11f, 0.21f, 0.98f);
                    pressedBg     = BurgundyPanel;
                    selectedBg    = new Color(0.22f, 0.15f, 0.09f, 0.98f);
                    normalFrame   = MutedGold;
                    hoverFrame    = BrightGold;
                    selectedFrame = BrightGold;
                    normalTxt     = Ivory;
                    hoverTxt      = BrightGold;
                    selectedTxt   = BrightGold;
                    break;
            }

            Image img = AddImage(parent, name, normalBg, sprPanel, true);
            img.type = Image.Type.Sliced;

            Image sheen = AddImage(img.rectTransform, "Sheen", Color.white, sprSheen, false);
            sheen.type = Image.Type.Sliced;
            Stretch(sheen.rectTransform);

            Image frame = AddImage(img.rectTransform, "Frame", normalFrame, sprFrameOrnate, false);
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform);

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None; // UIButtonJuice handles scale/frame/tint smoothly
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            Text t = AddText(img.rectTransform, "Label", label, fontSize, normalTxt, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = fontSize;
            Region(t.rectTransform, 0.05f, 0.06f, 0.95f, 0.94f);

            var juice = img.gameObject.AddComponent<UIButtonJuice>();
            juice.targetBackground = img;
            juice.targetFrame      = frame;
            juice.targetLabel      = t;
            juice.normalBg         = normalBg;
            juice.hoverBg          = hoverBg;
            juice.pressedBg        = pressedBg;
            juice.selectedBg       = selectedBg;
            juice.normalFrame      = normalFrame;
            juice.hoverFrame       = hoverFrame;
            juice.selectedFrame    = selectedFrame;
            juice.normalText       = normalTxt;
            juice.hoverText        = hoverTxt;
            juice.selectedText     = selectedTxt;
            juice.hoverScale       = variant == ButtonVariant.Compact ? 1.02f  : 1.035f;
            juice.selectedScale    = variant == ButtonVariant.Compact ? 1.02f  : 1.035f;
            juice.pressedScale     = variant == ButtonVariant.Compact ? 0.985f : 0.97f;
            return btn;
        }

        static void AddHoverSelect(Button btn, TitleScreenManager mgr, int index)
        {
            var trigger = btn.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            UnityEventTools.AddIntPersistentListener(entry.callback, mgr.UpdateSel, index);
            trigger.triggers.Add(entry);
        }

        static Toggle AddToggle(RectTransform parent, string name, string label)
        {
            Image card = AddFramedPanel(parent, name, SlotDark, MutedGold, true);

            Image box = AddImage(card.rectTransform, "Checkbox", ObsidianPanel, sprPanel, false);
            box.type = Image.Type.Sliced;
            Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(34f, 34f));
            Image boxFrame = AddImage(box.rectTransform, "Frame", AntiqueGold, sprFrameOrnate, false);
            boxFrame.type = Image.Type.Sliced;
            Stretch(boxFrame.rectTransform);

            Image check = AddImage(box.rectTransform, "Checkmark", BrightGold, sprPanel, false);
            check.type = Image.Type.Sliced;
            Region(check.rectTransform, 0.22f, 0.22f, 0.78f, 0.78f);

            Text lbl = AddText(card.rectTransform, "Label", label, 22, Ivory, TextAnchor.MiddleLeft, FontStyle.Bold);
            Region(lbl.rectTransform, 0.24f, 0.05f, 0.96f, 0.95f);

            var tg = card.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = box;
            tg.graphic = check;
            tg.isOn = true;
            tg.navigation = new Navigation { mode = Navigation.Mode.None };
            return tg;
        }

        static Slider AddSlider(RectTransform parent, string name)
        {
            RectTransform root = NewRect(name, parent);

            Image bg = AddImage(root, "Background", new Color(0.11f, 0.09f, 0.15f, 1f), sprPill, false);
            bg.type = Image.Type.Sliced;
            bg.rectTransform.anchorMin = new Vector2(0f, 0.28f); bg.rectTransform.anchorMax = new Vector2(1f, 0.72f);
            bg.rectTransform.offsetMin = Vector2.zero; bg.rectTransform.offsetMax = Vector2.zero;

            RectTransform fillArea = NewRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.28f); fillArea.anchorMax = new Vector2(1f, 0.72f);
            fillArea.offsetMin = new Vector2(4f, 2f);    fillArea.offsetMax = new Vector2(-10f, -2f);

            Image fill = AddImage(fillArea, "Fill", AntiqueGold, sprPill, false);
            fill.type = Image.Type.Sliced;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

            RectTransform handleArea = NewRect("Handle Slide Area", root);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(14f, 0f); handleArea.offsetMax = new Vector2(-14f, 0f);

            Image handle = AddImage(handleArea, "Handle", BrightGold, sprKnob, true);
            handle.preserveAspect = true;
            handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            handle.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(28f, 28f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }
    }
}
