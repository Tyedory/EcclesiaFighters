using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    public enum MapID { Exterior, Interior }

    public class MapSelectManager : MonoBehaviour
    {
        [Header("Map Cards")]
        public Image cardExterior;
        public Image cardInterior;
        public Sprite bgExterior;       // BG_MapExterior.jpg
        public Sprite bgInterior;       // BG_MapInterior.jpg

        [Header("VS Bar")]
        public Text nameA;
        public Text nameB;

        [Header("Match Rules")]
        public GameObject   matchRulesPanel;
        public CanvasGroup  aiDifficultyGroup;
        public Text         aiRuleNoteText;
        public Button[]     difficultyButtons;   // [0]=Fácil, [1]=Normal, [2]=Difícil
        public Button       prevDurationButton;
        public Button       nextDurationButton;
        public Text         durationLabel;
        public Button[]     hpButtons;           // [0]=Baixo, [1]=Médio, [2]=Alto

        [Header("Fight Intro Overlay")]
        public CanvasGroup fightOverlay;
        public Text fightMapText;
        public Text fightNamesText;
        public Image fadeOverlay;

        [Header("Confirm")]
        public Button goBtn;

        [Header("Back")]
        public TitleScreenManager titleManager;
        [Tooltip("Scene loaded by OnBack when titleManager is not in this scene.")]
        public string backScene = "CharSelect";

        private MapID selectedMap;
        private bool  mapChosen = false;

        // Colors
        private readonly Color normalBorder   = new Color(0.16f, 0.13f, 0.08f);
        private readonly Color selectedBorder = new Color(0.94f, 0.75f, 0.15f);

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;

            if (cardExterior != null)
            {
                if (bgExterior != null) cardExterior.sprite = bgExterior;
                cardExterior.color = Color.white;
                var jExt = cardExterior.GetComponent<UIButtonJuice>();
                if (jExt != null) jExt.SetSelected(false);
            }
            if (cardInterior != null)
            {
                if (bgInterior != null) cardInterior.sprite = bgInterior;
                cardInterior.color = Color.white;
                var jInt = cardInterior.GetComponent<UIButtonJuice>();
                if (jInt != null) jInt.SetSelected(false);
            }

            mapChosen = false;
            if (goBtn != null) goBtn.interactable = false;

            EnsureMatchRulesUI();
            ApplyLocalization();
            RefreshMatchRulesUI();
            EnforceOverlayLayering();
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        void OnLanguageChanged(GameLanguage _)
        {
            ApplyLocalization();
            RefreshMatchRulesUI();
        }

        public void ApplyLocalization()
        {
            int idxA = PlayerPrefs.GetInt("SelectedCharA", 0);
            int idxB = PlayerPrefs.GetInt("SelectedCharB", 1);
            if (nameA != null) nameA.text = LocalizationManager.GetFighterName(idxA).ToUpper();
            if (nameB != null) nameB.text = LocalizationManager.GetFighterName(idxB).ToUpper();

            Transform rootTf = cardExterior != null ? cardExterior.transform.parent : null;
            if (rootTf != null)
            {
                SetTextChild(rootTf, "HeaderBanner/Header", LocalizationManager.Pick("ESCOLHA O CAMPO DE BATALHA", "CHOOSE THE BATTLEGROUND"));
                SetButtonLabel(rootTf, "Btn_Voltar", LocalizationManager.Pick("VOLTAR", "BACK"));
            }

            if (goBtn != null)
            {
                Text goTxt = goBtn.GetComponentInChildren<Text>(true);
                if (goTxt != null) goTxt.text = LocalizationManager.Pick("INICIAR LUTA", "START FIGHT");
            }

            if (cardExterior != null)
            {
                SetTextChild(cardExterior.transform, "LabelBar/Label",    LocalizationManager.GetStageName(false).ToUpper());
                SetTextChild(cardExterior.transform, "LabelBar/Subtitle", LocalizationManager.GetStageSubtitle(false));
            }
            if (cardInterior != null)
            {
                SetTextChild(cardInterior.transform, "LabelBar/Label",    LocalizationManager.GetStageName(true).ToUpper());
                SetTextChild(cardInterior.transform, "LabelBar/Subtitle", LocalizationManager.GetStageSubtitle(true));
            }

            if (matchRulesPanel != null)
            {
                Transform mr = matchRulesPanel.transform;
                SetTextChild(mr, "RulesHeader",                       LocalizationManager.Pick("✦  REGRAS DA PARTIDA  ✦", "✦  MATCH RULES  ✦"));
                SetTextChild(mr, "RulesBody/Col_AIDifficulty/Label",  LocalizationManager.Pick("DIFICULDADE IA", "AI DIFFICULTY"));
                SetTextChild(mr, "RulesBody/Col_Duration/Label",      LocalizationManager.Pick("TEMPO DO ROUND", "ROUND TIME"));
                SetTextChild(mr, "RulesBody/Col_HPPreset/Label",      LocalizationManager.Pick("VIDA (HP)", "HEALTH (HP)"));
            }

            if (difficultyButtons != null)
            {
                SetBtnLabel(difficultyButtons.Length > 0 ? difficultyButtons[0] : null, LocalizationManager.Pick("FÁCIL",   "EASY"));
                SetBtnLabel(difficultyButtons.Length > 1 ? difficultyButtons[1] : null, "NORMAL");
                SetBtnLabel(difficultyButtons.Length > 2 ? difficultyButtons[2] : null, LocalizationManager.Pick("DIFÍCIL", "HARD"));
            }

            if (hpButtons != null)
            {
                SetBtnLabel(hpButtons.Length > 0 ? hpButtons[0] : null, LocalizationManager.Pick("BAIXO", "LOW"));
                SetBtnLabel(hpButtons.Length > 1 ? hpButtons[1] : null, LocalizationManager.Pick("MÉDIO", "MEDIUM"));
                SetBtnLabel(hpButtons.Length > 2 ? hpButtons[2] : null, LocalizationManager.Pick("ALTO",  "HIGH"));
            }

            if (fightOverlay != null)
                SetTextChild(fightOverlay.transform, "IntroFrame/Prepare", LocalizationManager.Pick("PREPARE-SE PARA A LUTA", "PREPARE FOR BATTLE"));
        }

        static void SetTextChild(Transform parent, string path, string text)
        {
            if (parent == null) return;
            Transform tf = parent.Find(path);
            if (tf == null) return;
            Text t = tf.GetComponent<Text>();
            if (t != null) t.text = text;
        }

        static void SetButtonLabel(Transform parent, string path, string text)
        {
            if (parent == null) return;
            Transform tf = parent.Find(path);
            if (tf == null) return;
            Text t = tf.GetComponentInChildren<Text>(true);
            if (t != null) t.text = text;
        }

        static void SetBtnLabel(Button btn, string text)
        {
            if (btn == null) return;
            Text t = btn.GetComponentInChildren<Text>(true);
            if (t != null) t.text = text;
        }

        // ══════════════════════════════════════════════════════
        //  Stage Cards & Scene Navigation
        // ══════════════════════════════════════════════════════
        public void SelectExterior()
        {
            AudioManager.PlayUIClick();
            selectedMap = MapID.Exterior;
            mapChosen = true;
            if (goBtn != null) goBtn.interactable = true;
            HighlightCard(cardExterior, cardInterior);
        }

        public void SelectInterior()
        {
            AudioManager.PlayUIClick();
            selectedMap = MapID.Interior;
            mapChosen = true;
            if (goBtn != null) goBtn.interactable = true;
            HighlightCard(cardInterior, cardExterior);
        }

        void HighlightCard(Image on, Image off)
        {
            // Full brightness on selected stage; lightly dim the unselected card
            if (on != null)
            {
                on.color = Color.white;
                var juiceOn = on.GetComponent<UIButtonJuice>();
                if (juiceOn != null) juiceOn.SetSelected(true);
                var o = on.GetComponent<Outline>();
                if (o != null) { o.effectColor = selectedBorder; o.effectDistance = new Vector2(4f, -4f); }
            }
            if (off != null)
            {
                off.color = new Color(0.76f, 0.76f, 0.80f, 1f);
                var juiceOff = off.GetComponent<UIButtonJuice>();
                if (juiceOff != null) juiceOff.SetSelected(false);
                var o = off.GetComponent<Outline>();
                if (o != null) { o.effectColor = normalBorder; o.effectDistance = new Vector2(2f, -2f); }
            }
        }

        public void OnConfirm()
        {
            if (!mapChosen) return;
            AudioManager.PlayUIConfirm();
            PlayerPrefs.SetString("SelectedMap", selectedMap.ToString());
            MatchConfig.Save();
            StartCoroutine(FightIntro());
        }

        IEnumerator FightIntro()
        {
            EnforceOverlayLayering();
            // Show fight overlay
            fightOverlay.alpha = 0; fightOverlay.gameObject.SetActive(true);
            if (fightMapText != null)   fightMapText.text   = LocalizationManager.GetStageName(selectedMap == MapID.Interior);
            if (fightNamesText != null) fightNamesText.text = nameA.text + " · " + nameB.text;
            // Fade in overlay
            float t = 0;
            while (t < 0.35f) { t += Time.deltaTime; fightOverlay.alpha = t / 0.35f; yield return null; }
            fightOverlay.alpha = 1;
            yield return new WaitForSeconds(2.2f);
            // Load fight scene
            SceneManager.LoadScene("FightScene");
        }

        public void OnBack()
        {
            AudioManager.PlayUIBack();
            MatchConfig.Save();
            if (titleManager != null) titleManager.ReturnToCharSel();
            else SceneManager.LoadScene(backScene);
        }

        // ══════════════════════════════════════════════════════
        //  Match Rules Callbacks & Sync
        // ══════════════════════════════════════════════════════
        public void SetDifficultyEasy()   => SetDifficulty(0);
        public void SetDifficultyNormal() => SetDifficulty(1);
        public void SetDifficultyHard()   => SetDifficulty(2);

        public void SetDifficulty(int idx)
        {
            MatchConfig.Difficulty = (AIDifficulty)Mathf.Clamp(idx, 0, 2);
            ClearFocus();
            RefreshMatchRulesUI();
        }

        public void PrevDuration()
        {
            MatchConfig.StepDuration(-1);
            ClearFocus();
            RefreshMatchRulesUI();
        }

        public void NextDuration()
        {
            MatchConfig.StepDuration(+1);
            ClearFocus();
            RefreshMatchRulesUI();
        }

        public void SetHPLow()    => SetHPPreset(0);
        public void SetHPMedium() => SetHPPreset(1);
        public void SetHPHigh()   => SetHPPreset(2);

        public void SetHPPreset(int idx)
        {
            MatchConfig.HP = (HPPreset)Mathf.Clamp(idx, 0, 2);
            ClearFocus();
            RefreshMatchRulesUI();
        }

        public void RefreshMatchRulesUI()
        {
            int diffIdx = (int)MatchConfig.Difficulty;
            if (difficultyButtons != null)
            {
                for (int i = 0; i < difficultyButtons.Length; i++)
                    ApplyToggleHighlight(difficultyButtons[i], i == diffIdx);
            }

            if (durationLabel != null)
                durationLabel.text = $"{MatchConfig.MatchDurationSeconds}s";

            int hpIdx = (int)MatchConfig.HP;
            if (hpButtons != null)
            {
                for (int i = 0; i < hpButtons.Length; i++)
                    ApplyToggleHighlight(hpButtons[i], i == hpIdx);
            }

            bool p1AI = PlayerPrefs.GetInt("P1IsAI", 0) == 1;
            bool p2AI = PlayerPrefs.GetInt("P2IsAI", 1) == 1;
            bool bothHuman = !p1AI && !p2AI;

            if (aiDifficultyGroup != null)
                aiDifficultyGroup.alpha = bothHuman ? 0.62f : 1.0f;

            if (aiRuleNoteText != null)
            {
                aiRuleNoteText.gameObject.SetActive(bothHuman);
                if (bothHuman)
                    aiRuleNoteText.text = LocalizationManager.Pick("MODO 2P LOCAL · IA DESATIVADA", "2P LOCAL MODE · AI DISABLED");
            }
        }

        static void ApplyToggleHighlight(Button btn, bool active)
        {
            if (btn == null) return;
            var juice = btn.GetComponent<UIButtonJuice>();
            if (juice != null)
            {
                juice.SetSelected(active);
                juice.ApplyImmediate();
                return;
            }
            if (btn.image != null)
                btn.image.color = active ? new Color(0.88f, 0.70f, 0.24f, 1f) : new Color(0.06f, 0.05f, 0.09f, 0.94f);
            Text lbl = btn.GetComponentInChildren<Text>();
            if (lbl != null)
                lbl.color = active ? new Color(0.05f, 0.03f, 0.02f, 1f) : new Color(0.96f, 0.93f, 0.86f, 1f);
        }

        static void ClearFocus()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // ══════════════════════════════════════════════════════
        //  Runtime Fallback Builder (for pre-built MapSelect.unity)
        // ══════════════════════════════════════════════════════
        void EnsureMatchRulesUI()
        {
            if (matchRulesPanel != null && difficultyButtons != null && difficultyButtons.Length == 3 &&
                hpButtons != null && hpButtons.Length == 3 && durationLabel != null)
                return;

            if (cardExterior == null) return;
            Transform root = cardExterior.transform.parent;
            if (root == null) return;

            // Adjust MapSelect vertical layout cleanly so cards + Match Rules + bottom CTA buttons never overlap
            Transform hdrTf = root.Find("HeaderBanner");
            if (hdrTf != null)
                SetRegion(hdrTf.GetComponent<RectTransform>(), 0.14f, 0.885f, 0.86f, 0.972f);

            Transform vsTf = root.Find("VSBar");
            if (vsTf != null)
                SetRegion(vsTf.GetComponent<RectTransform>(), 0.22f, 0.790f, 0.78f, 0.868f);

            SetRegion(cardExterior.rectTransform, 0.06f, 0.325f, 0.48f, 0.772f);
            if (cardInterior != null)
                SetRegion(cardInterior.rectTransform, 0.52f, 0.325f, 0.94f, 0.772f);

            Sprite panelSpr = null;
            Sprite frameSpr = null;
            if (vsTf != null)
            {
                var vsImg = vsTf.GetComponent<Image>();
                if (vsImg != null) panelSpr = vsImg.sprite;
                Transform fr = vsTf.Find("Frame");
                if (fr != null)
                {
                    var frImg = fr.GetComponent<Image>();
                    if (frImg != null) frameSpr = frImg.sprite;
                }
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject panelGo = CreateSlicedBox("MatchRulesPanel", root,
                new Color(0.06f, 0.05f, 0.09f, 0.94f), new Color(0.84f, 0.67f, 0.27f, 1f),
                panelSpr, frameSpr, raycast: true);
            SetRegion(panelGo.GetComponent<RectTransform>(), 0.06f, 0.148f, 0.94f, 0.305f);
            matchRulesPanel = panelGo;

            // Header strip
            Text titleTxt = CreateText(panelGo.transform, "RulesHeader", "✦  REGRAS DA PARTIDA  ✦",
                19, new Color(1.00f, 0.85f, 0.36f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold, font);
            SetRegion(titleTxt.rectTransform, 0.02f, 0.74f, 0.98f, 0.96f);

            aiRuleNoteText = CreateText(panelGo.transform, "AIRuleNote", "MODO 2P LOCAL · IA DESATIVADA",
                13, new Color(0.76f, 0.72f, 0.65f, 0.95f), TextAnchor.MiddleRight, FontStyle.Italic, font);
            SetRegion(aiRuleNoteText.rectTransform, 0.66f, 0.74f, 0.975f, 0.96f);

            // Three-column body
            var colsGo = new GameObject("RulesBody", typeof(RectTransform));
            colsGo.transform.SetParent(panelGo.transform, false);
            var colsRt = colsGo.GetComponent<RectTransform>();
            SetRegion(colsRt, 0.015f, 0.08f, 0.985f, 0.72f);

            var hlg = colsGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding                = new RectOffset(14, 14, 2, 6);
            hlg.spacing                = 22f;
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;

            // Column 1: AI Difficulty
            Transform colDiff = CreateRuleColumn(colsRt, "Col_AIDifficulty", "DIFICULDADE IA", 1.15f, font, out aiDifficultyGroup);
            Transform diffRow = CreateRuleRow(colDiff, "DifficultyRow", 8f);
            difficultyButtons = new[]
            {
                CreateRuleButton(diffRow, "Btn_DiffEasy",   "FÁCIL",   16, panelSpr, frameSpr, font, SetDifficultyEasy),
                CreateRuleButton(diffRow, "Btn_DiffNormal", "NORMAL",  16, panelSpr, frameSpr, font, SetDifficultyNormal),
                CreateRuleButton(diffRow, "Btn_DiffHard",   "DIFÍCIL", 16, panelSpr, frameSpr, font, SetDifficultyHard)
            };

            // Column 2: Round Duration Stepper
            Transform colDur = CreateRuleColumn(colsRt, "Col_Duration", "TEMPO DO ROUND", 0.85f, font, out _);
            Transform durRow = CreateRuleRow(colDur, "DurationRow", 10f);
            prevDurationButton = CreateRuleButton(durRow, "Btn_DurPrev", "<", 22, panelSpr, frameSpr, font, PrevDuration);
            SetLayoutElem(prevDurationButton.gameObject, preferredWidth: 54f, flexibleWidth: 0f);

            GameObject valBox = CreateSlicedBox("DurationValueBox", durRow,
                new Color(0.03f, 0.02f, 0.05f, 0.96f), new Color(0.56f, 0.44f, 0.18f, 0.92f),
                panelSpr, frameSpr, raycast: false);
            SetLayoutElem(valBox, flexibleWidth: 1f);
            durationLabel = CreateText(valBox.transform, "DurationLabel", "99s",
                24, new Color(1.00f, 0.85f, 0.36f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold, font);
            durationLabel.resizeTextForBestFit = true;
            durationLabel.resizeTextMinSize    = 16;
            durationLabel.resizeTextMaxSize    = 26;
            SetRegion(durationLabel.rectTransform, 0f, 0f, 1f, 1f);

            nextDurationButton = CreateRuleButton(durRow, "Btn_DurNext", ">", 22, panelSpr, frameSpr, font, NextDuration);
            SetLayoutElem(nextDurationButton.gameObject, preferredWidth: 54f, flexibleWidth: 0f);

            // Column 3: Fighter HP Preset
            Transform colHP = CreateRuleColumn(colsRt, "Col_HPPreset", "VIDA (HP)", 1.15f, font, out _);
            Transform hpRow = CreateRuleRow(colHP, "HPRow", 8f);
            hpButtons = new[]
            {
                CreateRuleButton(hpRow, "Btn_HPLow",    "BAIXO", 16, panelSpr, frameSpr, font, SetHPLow),
                CreateRuleButton(hpRow, "Btn_HPMedium", "MÉDIO", 16, panelSpr, frameSpr, font, SetHPMedium),
                CreateRuleButton(hpRow, "Btn_HPHigh",   "ALTO",  16, panelSpr, frameSpr, font, SetHPHigh)
            };

            EnforceOverlayLayering();
        }

        void EnforceOverlayLayering()
        {
            if (fightOverlay != null) fightOverlay.transform.SetAsLastSibling();
            if (fadeOverlay != null)  fadeOverlay.rectTransform.SetAsLastSibling();
        }

        static Transform CreateRuleColumn(Transform parent, string name, string title, float flexWidth, Font font, out CanvasGroup group)
        {
            var col = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            col.transform.SetParent(parent, false);
            group = col.GetComponent<CanvasGroup>();
            SetLayoutElem(col, flexibleWidth: flexWidth);

            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing                = 6f;
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            Text lbl = CreateText(col.transform, "Label", title, 15, new Color(0.84f, 0.67f, 0.27f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold, font);
            SetLayoutElem(lbl.gameObject, preferredHeight: 22f);

            return col.transform;
        }

        static Transform CreateRuleRow(Transform parent, string name, float spacing)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetLayoutElem(row, preferredHeight: 44f, flexibleHeight: 1f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = spacing;
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            return row.transform;
        }

        static Button CreateRuleButton(Transform parent, string name, string label, int fontSize,
            Sprite panelSpr, Sprite frameSpr, Font font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreateSlicedBox(name, parent,
                new Color(0.06f, 0.05f, 0.09f, 0.94f), new Color(0.56f, 0.44f, 0.18f, 0.92f),
                panelSpr, frameSpr, raycast: true);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.transition    = Selectable.Transition.None;
            btn.navigation    = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) btn.onClick.AddListener(onClick);

            Text txt = CreateText(go.transform, "Label", label, fontSize, new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold, font);
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize    = 12;
            txt.resizeTextMaxSize    = fontSize;
            SetRegion(txt.rectTransform, 0.06f, 0.08f, 0.94f, 0.92f);

            Transform frTf = go.transform.Find("Frame");
            var juice = go.AddComponent<UIButtonJuice>();
            juice.targetBackground = btn.targetGraphic;
            juice.targetFrame      = frTf != null ? frTf.GetComponent<Image>() : null;
            juice.targetLabel      = txt;
            juice.normalBg         = new Color(0.06f, 0.05f, 0.09f, 0.94f);
            juice.hoverBg          = new Color(0.15f, 0.11f, 0.20f, 0.98f);
            juice.pressedBg        = new Color(0.13f, 0.10f, 0.18f, 0.98f);
            juice.selectedBg       = new Color(0.88f, 0.70f, 0.24f, 1.00f);
            juice.normalFrame      = new Color(0.56f, 0.44f, 0.18f, 0.92f);
            juice.hoverFrame       = new Color(1.00f, 0.85f, 0.36f, 1.00f);
            juice.selectedFrame    = new Color(1.00f, 0.85f, 0.36f, 1.00f);
            juice.normalText       = new Color(0.96f, 0.93f, 0.86f, 1.00f);
            juice.hoverText        = new Color(1.00f, 0.85f, 0.36f, 1.00f);
            juice.selectedText     = new Color(0.05f, 0.03f, 0.02f, 1.00f);
            juice.hoverScale       = 1.02f;
            juice.selectedScale    = 1.02f;
            juice.pressedScale     = 0.98f;
            juice.clickSoundRole   = UIButtonJuice.UISoundRole.StandardClick;

            return btn;
        }

        static GameObject CreateSlicedBox(string name, Transform parent, Color fillColor, Color frameColor,
            Sprite panelSpr, Sprite frameSpr, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite        = panelSpr;
            img.type          = panelSpr != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color         = fillColor;
            img.raycastTarget = raycast;

            var fr = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            fr.transform.SetParent(go.transform, false);
            var frRt = fr.GetComponent<RectTransform>();
            SetRegion(frRt, 0f, 0f, 1f, 1f);
            var frImg = fr.GetComponent<Image>();
            frImg.sprite        = frameSpr;
            frImg.type          = frameSpr != null ? Image.Type.Sliced : Image.Type.Simple;
            frImg.color         = frameColor;
            frImg.raycastTarget = false;
            fr.GetComponent<LayoutElement>().ignoreLayout = true;

            if (frameSpr == null)
            {
                frImg.enabled = false;
                var outline = go.AddComponent<Outline>();
                outline.effectColor    = frameColor;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return go;
        }

        static Text CreateText(Transform parent, string name, string label, int size, Color color,
            TextAnchor align, FontStyle style, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font          = font;
            t.text          = label;
            t.fontSize      = size;
            t.fontStyle     = style;
            t.color         = color;
            t.alignment     = align;
            t.raycastTarget = false;
            return t;
        }

        static void SetRegion(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static LayoutElement SetLayoutElem(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f,
            float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (preferredWidth  >= 0f) le.preferredWidth  = preferredWidth;
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
            if (flexibleWidth   >= 0f) le.flexibleWidth   = flexibleWidth;
            if (flexibleHeight  >= 0f) le.flexibleHeight  = flexibleHeight;
            return le;
        }
    }
}
