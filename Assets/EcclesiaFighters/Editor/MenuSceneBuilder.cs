using System.Collections.Generic;
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
    /// Generates the missing UI (Canvas, EventSystem, panels, buttons, texts, audio) for the three menu
    /// scenes and wires every serialized field of <see cref="TitleScreenManager"/>,
    /// <see cref="CharacterSelectManager"/> and <see cref="MapSelectManager"/> (+ <see cref="SettingsManager"/>).
    ///
    /// Menu:  EcclesiaFighters ▸ Menus ▸ Build All Menu Scenes   (or each scene individually)
    /// CLI:   Unity -batchmode -quit -projectPath . -executeMethod EcclesiaFighters.EditorTools.MenuSceneBuilder.BuildAllBatch
    ///
    /// The builder is idempotent: the generated roots ("Canvas", "EventSystem", "BGM") are deleted and rebuilt
    /// on every run; the manager GameObjects and the Camera are kept.
    /// </summary>
    public static class MenuSceneBuilder
    {
        const string Root      = "Assets/EcclesiaFighters/";
        const string ScenesDir = Root + "Scenes/";
        const string SpritesDir= Root + "Sprites/";
        const string AudioDir  = Root + "Audio/";

        // ── Palette (matches the colours hard-coded in the manager scripts) ──
        static readonly Color Gold      = new Color(0.94f, 0.75f, 0.15f, 1f);
        static readonly Color Parchment = new Color(0.94f, 0.91f, 0.85f, 1f);
        static readonly Color PanelDark = new Color(0.05f, 0.04f, 0.07f, 0.88f);
        static readonly Color ButtonBg  = new Color(0.10f, 0.07f, 0.12f, 0.92f);
        static readonly Color SliderBg  = new Color(0.16f, 0.13f, 0.08f, 1f);
        static readonly Color FaithBlue = new Color(0.5f, 0.8f, 1f, 1f);
        static readonly Color IdeoRed   = new Color(1f, 0.5f, 0.5f, 1f);

        static Font UIFont { get { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } }

        // ═════════════════════════════════════════════════════
        //  Menu entries
        // ═════════════════════════════════════════════════════
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

        /// <summary>No dialogs: usable from -executeMethod.</summary>
        public static void BuildAllBatch()
        {
            BuildTitle();
            BuildCharSelect();
            BuildMapSelect();
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuSceneBuilder] TitleScreen, CharSelect and MapSelect rebuilt.");
        }

        // ═════════════════════════════════════════════════════
        //  TitleScreen
        // ═════════════════════════════════════════════════════
        static void BuildTitle()
        {
            Scene scene = OpenScene("TitleScreen");
            ClearGenerated(scene);
            SetupCamera();
            var mgr = GetOrCreateManager<TitleScreenManager>("TitleScreenManager");
            RectTransform root = CreateCanvas();

            // ── BGM source (TitleScreenManager.bgmSource) ──
            var bgmGo = new GameObject("BGM");
            var bgm = bgmGo.AddComponent<AudioSource>();
            bgm.playOnAwake = false; bgm.loop = true; bgm.spatialBlend = 0f;

            // ── Title panel ──
            RectTransform titlePanel = NewRect("TitlePanel", root); Stretch(titlePanel);
            Image bg = AddImage(titlePanel, "Background", Color.white, LoadSprite("Backgrounds/BG_TitleScreen.jpg"), false);
            Stretch(bg.rectTransform);
            Image shade = AddImage(titlePanel, "Shade", new Color(0f, 0f, 0f, 0.35f), null, false);
            Stretch(shade.rectTransform);

            RectTransform logo = NewRect("TitleLogo", titlePanel);
            Region(logo, 0.12f, 0.60f, 0.88f, 0.93f);
            var logoGroup = logo.gameObject.AddComponent<CanvasGroup>();
            logoGroup.alpha = 0f; logoGroup.interactable = false; logoGroup.blocksRaycasts = false;
            Text title = AddText(logo, "Title", "ECCLESIA FIGHTERS", 120, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(title.rectTransform, 0f, 0.42f, 1f, 1f); AddShadow(title);
            Text sub = AddText(logo, "Subtitle", "FÉ  ×  IDEOLOGIA", 46, Parchment, TextAnchor.MiddleCenter, FontStyle.Italic);
            Region(sub.rectTransform, 0f, 0.08f, 1f, 0.42f); AddShadow(sub);

            RectTransform menu = NewRect("MenuGroup", titlePanel);
            Region(menu, 0.35f, 0.08f, 0.65f, 0.50f);
            var menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
            menuGroup.alpha = 0f;

            string[] labels = { "INICIAR BATALHA", "CONFIGURAÇÕES", "SAIR" };
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                buttons[i] = AddButton(menu, "Btn_" + labels[i].Replace(" ", ""), labels[i], 40);
                float yMax = 1f - i * 0.34f;
                Region(buttons[i].GetComponent<RectTransform>(), 0f, yMax - 0.27f, 1f, yMax);
                AddHoverSelect(buttons[i], mgr, i);
            }
            UnityEventTools.AddPersistentListener(buttons[0].onClick, mgr.GotoCharSelect);
            UnityEventTools.AddPersistentListener(buttons[1].onClick, mgr.GotoSettings);
            UnityEventTools.AddPersistentListener(buttons[2].onClick, mgr.QuitGame);

            // ── Settings panel (hidden at start, SettingsManager lives on it) ──
            GameObject settingsPanel = BuildSettingsPanel(root, mgr);

            // ── Overlays (always last = always on top) ──
            Image flash = AddImage(root, "LightningFlash", new Color(1f, 1f, 1f, 0f), null, false);
            Stretch(flash.rectTransform);
            Image fade = AddImage(root, "FadeOverlay", new Color(0f, 0f, 0f, 0f), null, false);
            Stretch(fade.rectTransform);

            // ── Wire TitleScreenManager ──
            mgr.titlePanel      = titlePanel.gameObject;
            mgr.charSelectPanel = null;           // separate scene → manager falls back to loading "CharSelect"
            mgr.mapSelectPanel  = null;           // separate scene → manager falls back to loading "MapSelect"
            mgr.settingsPanel   = settingsPanel;
            mgr.fadeOverlay     = fade;
            mgr.lightningFlash  = flash;
            mgr.titleLogoGroup  = logoGroup;
            mgr.menuGroup       = menuGroup;
            mgr.bgmSource       = bgm;
            mgr.bgmTitleClip    = LoadClip("BGM_TitleScreen.mp3");
            mgr.bgmCharClip     = LoadClip("BGM_CharSelect_JesuJoy.mp3");
            mgr.menuButtons     = buttons;
            mgr.charSelectScene = "CharSelect";
            mgr.mapSelectScene  = "MapSelect";
            EditorUtility.SetDirty(mgr);

            SaveScene(scene);
            Debug.Log("[MenuSceneBuilder] TitleScreen built.");
        }

        static GameObject BuildSettingsPanel(RectTransform root, TitleScreenManager title)
        {
            RectTransform panel = NewRect("SettingsPanel", root); Stretch(panel);
            var settings = panel.gameObject.AddComponent<SettingsManager>();

            Image bg = AddImage(panel, "Background", Color.white, LoadSprite("Backgrounds/BG_TitleScreen_Dark.jpg"), true);
            Stretch(bg.rectTransform);

            Image box = AddImage(panel, "Box", PanelDark);
            Region(box.rectTransform, 0.22f, 0.07f, 0.78f, 0.93f);
            AddOutline(box, Gold, 3f);

            Text head = AddText(box.rectTransform, "Heading", "CONFIGURAÇÕES", 56, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(head.rectTransform, 0.05f, 0.90f, 0.95f, 0.98f); AddShadow(head);

            // Graphics — one button per Quality level defined in the project
            Text gl = AddText(box.rectTransform, "GraphicsLabel", "QUALIDADE GRÁFICA", 28, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            Region(gl.rectTransform, 0.06f, 0.82f, 0.60f, 0.88f);
            string[] qn = QualitySettings.names;
            var qBtns = new Button[qn.Length];
            float qw = 0.88f / Mathf.Max(1, qn.Length);
            for (int i = 0; i < qn.Length; i++)
            {
                qBtns[i] = AddButton(box.rectTransform, "Quality_" + qn[i], qn[i].ToUpper(), 24);
                float x0 = 0.06f + i * qw;
                Region(qBtns[i].GetComponent<RectTransform>(), x0 + 0.005f, 0.72f, x0 + qw - 0.005f, 0.81f);
                UnityEventTools.AddIntPersistentListener(qBtns[i].onClick, settings.SetQuality, i);
            }

            Toggle vsync = AddToggle(box.rectTransform, "VSyncToggle", "V-SYNC");
            Region(vsync.GetComponent<RectTransform>(), 0.06f, 0.60f, 0.48f, 0.69f);
            UnityEventTools.AddPersistentListener<bool>(vsync.onValueChanged, settings.SetVSync);
            Toggle full = AddToggle(box.rectTransform, "FullscreenToggle", "TELA CHEIA");
            Region(full.GetComponent<RectTransform>(), 0.52f, 0.60f, 0.94f, 0.69f);
            UnityEventTools.AddPersistentListener<bool>(full.onValueChanged, settings.SetFullscreen);

            // Volume sliders
            Text vl = AddText(box.rectTransform, "VolumeLabel", "VOLUME", 28, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            Region(vl.rectTransform, 0.06f, 0.52f, 0.60f, 0.58f);
            string[] names = { "MASTER", "MÚSICA", "EFEITOS", "VOZES" };
            var sliders = new Slider[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                float y = 0.43f - i * 0.095f;
                Text l = AddText(box.rectTransform, names[i] + "Label", names[i], 26, Parchment, TextAnchor.MiddleLeft);
                Region(l.rectTransform, 0.06f, y, 0.30f, y + 0.07f);
                sliders[i] = AddSlider(box.rectTransform, names[i] + "Slider");
                Region(sliders[i].GetComponent<RectTransform>(), 0.32f, y + 0.01f, 0.94f, y + 0.06f);
            }
            UnityEventTools.AddPersistentListener<float>(sliders[0].onValueChanged, settings.SetMasterVol);
            UnityEventTools.AddPersistentListener<float>(sliders[1].onValueChanged, settings.SetMusicVol);
            UnityEventTools.AddPersistentListener<float>(sliders[2].onValueChanged, settings.SetSFXVol);
            UnityEventTools.AddPersistentListener<float>(sliders[3].onValueChanged, settings.SetVoiceVol);

            Button back = AddButton(box.rectTransform, "Btn_Voltar", "VOLTAR", 34);
            Region(back.GetComponent<RectTransform>(), 0.35f, 0.03f, 0.65f, 0.11f);
            UnityEventTools.AddPersistentListener(back.onClick, title.ReturnToTitle);

            // SettingsManager wiring (audioMixer stays empty: assign one with exposed Vol_Master/Music/SFX/Voice)
            settings.qualityButtons  = qBtns;
            settings.vsyncToggle     = vsync;
            settings.fullscreenToggle= full;
            settings.masterSlider    = sliders[0];
            settings.musicSlider     = sliders[1];
            settings.sfxSlider       = sliders[2];
            settings.voiceSlider     = sliders[3];
            EditorUtility.SetDirty(settings);

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // ═════════════════════════════════════════════════════
        //  CharSelect
        // ═════════════════════════════════════════════════════
        static void BuildCharSelect()
        {
            Scene scene = OpenScene("CharSelect");
            ClearGenerated(scene);
            SetupCamera();
            var mgr = GetOrCreateManager<CharacterSelectManager>("CharacterSelectManager");
            RectTransform root = CreateCanvas();
            AddBgm(AudioDir + "BGM_CharSelect_JesuJoy.mp3");

            Image bg = AddImage(root, "Background", Color.white, LoadSprite("Backgrounds/BG_CharSelect.jpg"), false);
            Stretch(bg.rectTransform);
            Image dim = AddImage(root, "Dim", new Color(0f, 0f, 0f, 0.45f), null, false);
            Stretch(dim.rectTransform);

            Text header = AddText(root, "Header", "ESCOLHA SEU LUTADOR", 64, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(header.rectTransform, 0.1f, 0.89f, 0.9f, 0.99f); AddShadow(header);

            // Roster (index 0 = Agostinho, 1 = Marx — same order FightSceneManager.characterPrefabs expects)
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

            BuildFighterCard(root, "PlayerCardA", "JOGADOR 1", 0.06f, 0.44f, FaithBlue, mgr, true,
                out mgr.portraitA, out mgr.nameA, out mgr.descA);
            BuildFighterCard(root, "PlayerCardB", "CPU", 0.56f, 0.94f, IdeoRed, mgr, false,
                out mgr.portraitB, out mgr.nameB, out mgr.descB);

            Text vs = AddText(root, "VS", "VS", 90, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(vs.rectTransform, 0.44f, 0.42f, 0.56f, 0.60f); AddShadow(vs);

            mgr.confirmText = AddText(root, "ConfirmText", "Selecione os lutadores e confirme", 30, Gold, TextAnchor.MiddleCenter, FontStyle.Italic);
            Region(mgr.confirmText.rectTransform, 0.2f, 0.115f, 0.8f, 0.165f); AddShadow(mgr.confirmText);

            Button confirm = AddButton(root, "Btn_Confirmar", "CONFIRMAR", 40);
            Region(confirm.GetComponent<RectTransform>(), 0.38f, 0.03f, 0.62f, 0.11f);
            UnityEventTools.AddPersistentListener(confirm.onClick, mgr.OnConfirm);

            Button back = AddButton(root, "Btn_Voltar", "VOLTAR", 32);
            Region(back.GetComponent<RectTransform>(), 0.03f, 0.03f, 0.15f, 0.10f);
            UnityEventTools.AddPersistentListener(back.onClick, mgr.OnBack);

            mgr.titleManager = null;     // TitleScreenManager lives in another scene → OnBack/OnConfirm use scene loading
            mgr.nextScene = "MapSelect";
            mgr.backScene = "TitleScreen";
            EditorUtility.SetDirty(mgr);

            SaveScene(scene);
            Debug.Log("[MenuSceneBuilder] CharSelect built.");
        }

        static void BuildFighterCard(RectTransform root, string name, string tag,
            float x0, float x1, Color accent, CharacterSelectManager mgr, bool isA,
            out Image portrait, out Text nameText, out Text descText)
        {
            Image card = AddImage(root, name, PanelDark);
            Region(card.rectTransform, x0, 0.17f, x1, 0.86f);
            AddOutline(card, accent, 3f);

            Text tagText = AddText(card.rectTransform, "Tag", tag, 30, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(tagText.rectTransform, 0.05f, 0.92f, 0.95f, 0.99f);

            portrait = AddImage(card.rectTransform, "Portrait", Color.white, null, false);
            Region(portrait.rectTransform, 0.08f, 0.38f, 0.92f, 0.91f);
            portrait.preserveAspect = true;

            nameText = AddText(card.rectTransform, "Name", "—", 46, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(nameText.rectTransform, 0.05f, 0.29f, 0.95f, 0.38f);
            nameText.resizeTextForBestFit = true; nameText.resizeTextMinSize = 20; nameText.resizeTextMaxSize = 46;
            AddShadow(nameText);

            descText = AddText(card.rectTransform, "Description", "", 26, Parchment, TextAnchor.UpperCenter);
            Region(descText.rectTransform, 0.07f, 0.14f, 0.93f, 0.29f);

            // One small roster button per character → SelectA(i) / SelectB(i)
            int n = mgr.characters.Count;
            float bw = 0.90f / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                Button b = AddButton(card.rectTransform, "Pick_" + mgr.characters[i].characterName.Replace(" ", ""),
                                     mgr.characters[i].characterName.ToUpper(), 24);
                float bx = 0.05f + i * bw;
                Region(b.GetComponent<RectTransform>(), bx + 0.008f, 0.02f, bx + bw - 0.008f, 0.12f);
                if (isA) UnityEventTools.AddIntPersistentListener(b.onClick, mgr.SelectA, i);
                else     UnityEventTools.AddIntPersistentListener(b.onClick, mgr.SelectB, i);
            }
        }

        // ═════════════════════════════════════════════════════
        //  MapSelect
        // ═════════════════════════════════════════════════════
        static void BuildMapSelect()
        {
            Scene scene = OpenScene("MapSelect");
            ClearGenerated(scene);
            SetupCamera();
            var mgr = GetOrCreateManager<MapSelectManager>("MapSelectManager");
            RectTransform root = CreateCanvas();
            AddBgm(AudioDir + "BGM_CharSelect_JesuJoy.mp3");

            Image bg = AddImage(root, "Background", Color.white, LoadSprite("Backgrounds/BG_CharSelect_Dark.jpg"), false);
            Stretch(bg.rectTransform);
            Image dim = AddImage(root, "Dim", new Color(0f, 0f, 0f, 0.35f), null, false);
            Stretch(dim.rectTransform);

            Text header = AddText(root, "Header", "ESCOLHA O CAMPO DE BATALHA", 60, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(header.rectTransform, 0.1f, 0.89f, 0.9f, 0.99f); AddShadow(header);

            // ── VS bar ──
            Image vsBar = AddImage(root, "VSBar", PanelDark, null, false);
            Region(vsBar.rectTransform, 0.25f, 0.78f, 0.75f, 0.87f);
            AddOutline(vsBar, Gold, 2f);
            mgr.nameA = AddText(vsBar.rectTransform, "NameA", "JOGADOR 1", 40, FaithBlue, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(mgr.nameA.rectTransform, 0.02f, 0f, 0.40f, 1f);
            Text vs = AddText(vsBar.rectTransform, "VS", "VS", 46, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(vs.rectTransform, 0.40f, 0f, 0.60f, 1f);
            mgr.nameB = AddText(vsBar.rectTransform, "NameB", "JOGADOR 2", 40, IdeoRed, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(mgr.nameB.rectTransform, 0.60f, 0f, 0.98f, 1f);
            mgr.nameA.resizeTextForBestFit = true; mgr.nameA.resizeTextMinSize = 18; mgr.nameA.resizeTextMaxSize = 40;
            mgr.nameB.resizeTextForBestFit = true; mgr.nameB.resizeTextMinSize = 18; mgr.nameB.resizeTextMaxSize = 40;

            // ── Map cards ──
            mgr.bgExterior = LoadSprite("Backgrounds/BG_MapExterior.jpg");
            mgr.bgInterior = LoadSprite("Backgrounds/BG_MapInterior.jpg");
            Button ext = BuildMapCard(root, "CardExterior", "FACHADA DA IGREJA", mgr.bgExterior, 0.07f, 0.48f, out mgr.cardExterior);
            Button inn = BuildMapCard(root, "CardInterior", "INTERIOR SAGRADO",   mgr.bgInterior, 0.52f, 0.93f, out mgr.cardInterior);
            UnityEventTools.AddPersistentListener(ext.onClick, mgr.SelectExterior);
            UnityEventTools.AddPersistentListener(inn.onClick, mgr.SelectInterior);

            // ── Go / Back ──
            mgr.goBtn = AddButton(root, "Btn_IniciarLuta", "INICIAR LUTA", 40);
            Region(mgr.goBtn.GetComponent<RectTransform>(), 0.38f, 0.05f, 0.62f, 0.14f);
            UnityEventTools.AddPersistentListener(mgr.goBtn.onClick, mgr.OnConfirm);

            Button back = AddButton(root, "Btn_Voltar", "VOLTAR", 32);
            Region(back.GetComponent<RectTransform>(), 0.03f, 0.05f, 0.15f, 0.12f);
            UnityEventTools.AddPersistentListener(back.onClick, mgr.OnBack);

            // ── Fight intro overlay (activated by MapSelectManager.FightIntro) ──
            RectTransform fo = NewRect("FightOverlay", root); Stretch(fo);
            mgr.fightOverlay = fo.gameObject.AddComponent<CanvasGroup>();
            mgr.fightOverlay.alpha = 0f;
            Image foBg = AddImage(fo, "Background", new Color(0f, 0f, 0f, 0.95f), null, true);
            Stretch(foBg.rectTransform);
            Text pre = AddText(fo, "Prepare", "PREPARE-SE PARA A LUTA", 36, Parchment, TextAnchor.MiddleCenter, FontStyle.Italic);
            Region(pre.rectTransform, 0.1f, 0.66f, 0.9f, 0.74f);
            mgr.fightMapText = AddText(fo, "MapText", "", 84, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(mgr.fightMapText.rectTransform, 0.05f, 0.50f, 0.95f, 0.66f); AddShadow(mgr.fightMapText);
            mgr.fightNamesText = AddText(fo, "NamesText", "", 52, Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            Region(mgr.fightNamesText.rectTransform, 0.05f, 0.36f, 0.95f, 0.50f); AddShadow(mgr.fightNamesText);
            fo.gameObject.SetActive(false);

            Image fade = AddImage(root, "FadeOverlay", new Color(0f, 0f, 0f, 0f), null, false);
            Stretch(fade.rectTransform);
            mgr.fadeOverlay = fade;

            mgr.titleManager = null;      // TitleScreenManager lives in another scene → OnBack loads backScene
            mgr.backScene = "CharSelect";
            EditorUtility.SetDirty(mgr);

            SaveScene(scene);
            Debug.Log("[MenuSceneBuilder] MapSelect built.");
        }

        static Button BuildMapCard(RectTransform root, string name, string label, Sprite sprite,
            float x0, float x1, out Image cardImage)
        {
            cardImage = AddImage(root, name, Color.white, sprite, true);
            Region(cardImage.rectTransform, x0, 0.20f, x1, 0.74f);
            AddOutline(cardImage, Gold, 3f);

            var btn = cardImage.gameObject.AddComponent<Button>();
            btn.targetGraphic = cardImage;
            btn.transition = Selectable.Transition.None;      // MapSelectManager.HighlightCard tints the Image itself
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            Image strip = AddImage(cardImage.rectTransform, "LabelBar", new Color(0f, 0f, 0f, 0.72f), null, false);
            Region(strip.rectTransform, 0f, 0f, 1f, 0.16f);
            Text t = AddText(strip.rectTransform, "Label", label, 40, Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform); AddShadow(t);
            return btn;
        }

        // ═════════════════════════════════════════════════════
        //  Scene helpers
        // ═════════════════════════════════════════════════════
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
                    Object.DestroyImmediate(go);
        }

        static void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindObjectOfType<Camera>();
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
            T m = Object.FindObjectOfType<T>();
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

        // ═════════════════════════════════════════════════════
        //  UI factory helpers
        // ═════════════════════════════════════════════════════
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

        /// <summary>Anchors the rect to a normalised region of its parent (zero offsets).</summary>
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

        static Image AddImage(RectTransform parent, string name, Color color, Sprite sprite = null, bool raycast = true)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
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
            return t;
        }

        static void AddShadow(Graphic g)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.8f);
            s.effectDistance = new Vector2(3f, -3f);
        }

        static void AddOutline(Graphic g, Color color, float distance)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(distance, -distance);
        }

        static Button AddButton(RectTransform parent, string name, string label, int fontSize)
        {
            Image img = AddImage(parent, name, ButtonBg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1f, 0.92f, 0.65f, 1f);
            cb.pressedColor     = new Color(0.75f, 0.60f, 0.25f, 1f);
            cb.selectedColor    = Color.white;
            cb.disabledColor    = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;
            AddOutline(img, Gold, 2f);

            Text t = AddText(img.rectTransform, "Label", label, fontSize, Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = fontSize;
            Stretch(t.rectTransform);
            return btn;
        }

        /// <summary>Mouse hover → TitleScreenManager.UpdateSel(index), so mouse and keyboard highlight agree.</summary>
        static void AddHoverSelect(Button btn, TitleScreenManager mgr, int index)
        {
            var trigger = btn.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            UnityEventTools.AddIntPersistentListener(entry.callback, mgr.UpdateSel, index);
            trigger.triggers.Add(entry);
        }

        static Toggle AddToggle(RectTransform parent, string name, string label)
        {
            RectTransform root = NewRect(name, parent);
            var hit = root.gameObject.AddComponent<Image>();      // invisible hit area so the whole row is clickable
            hit.color = new Color(0f, 0f, 0f, 0f);

            Image box = AddImage(root, "Background", ButtonBg);
            Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            AddOutline(box, Gold, 2f);
            Image check = AddImage(box.rectTransform, "Checkmark", Gold, null, false);
            check.rectTransform.anchorMin = Vector2.zero; check.rectTransform.anchorMax = Vector2.one;
            check.rectTransform.offsetMin = new Vector2(9f, 9f); check.rectTransform.offsetMax = new Vector2(-9f, -9f);

            Text lbl = AddText(root, "Label", label, 28, Parchment, TextAnchor.MiddleLeft);
            Region(lbl.rectTransform, 0.14f, 0f, 1f, 1f);

            var tg = root.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = box;
            tg.graphic = check;
            tg.isOn = true;
            tg.navigation = new Navigation { mode = Navigation.Mode.None };
            return tg;
        }

        static Slider AddSlider(RectTransform parent, string name)
        {
            RectTransform root = NewRect(name, parent);

            Image bg = AddImage(root, "Background", SliderBg);
            bg.rectTransform.anchorMin = new Vector2(0f, 0.25f); bg.rectTransform.anchorMax = new Vector2(1f, 0.75f);
            bg.rectTransform.offsetMin = Vector2.zero; bg.rectTransform.offsetMax = Vector2.zero;

            RectTransform fillArea = NewRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.25f); fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(5f, 0f);    fillArea.offsetMax = new Vector2(-15f, 0f);
            Image fill = AddImage(fillArea, "Fill", Gold, null, false);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

            RectTransform handleArea = NewRect("Handle Slide Area", root);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(10f, 0f); handleArea.offsetMax = new Vector2(-10f, 0f);
            Image handle = AddImage(handleArea, "Handle", Parchment);
            handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(22f, 0f);
            AddOutline(handle, Gold, 2f);

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
