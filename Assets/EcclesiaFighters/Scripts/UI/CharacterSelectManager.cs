using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EcclesiaFighters
{
    [System.Serializable]
    public class CharacterData
    {
        public string characterName;
        [TextArea(2,4)] public string description;
        public string faction;     // "Faith" | "Ideology"
        public Sprite portrait;
        public bool   unlocked = true;
    }

    public class CharacterSelectManager : MonoBehaviour
    {
        [Header("Characters — index 0 = Agostinho, 1 = Marx")]
        public List<CharacterData> characters = new List<CharacterData>();

        [Header("Portrait UI")]
        public Image portraitA;
        public Image portraitB;
        public Text  nameA, nameB;
        public Text  descA, descB;

        [Header("Confirm")]
        public Text  confirmText;
        public TitleScreenManager titleManager;   // drag the TitleScreenManager here
        [HideInInspector] public Button[] buttonsA;
        [HideInInspector] public Button[] buttonsB;

        [Header("Control Mode (JOGADOR / IA)")]
        public bool   p1IsAI = false;   // default JOGADOR
        public bool   p2IsAI = true;    // default IA
        public Button p1HumanBtn;
        public Button p1AIBtn;
        public Button p2HumanBtn;
        public Button p2AIBtn;
        public Text   tagTextA;
        public Text   tagTextB;

        [Header("Scene Flow (used when titleManager is not in this scene)")]
        public string nextScene = "MapSelect";
        public string backScene = "TitleScreen";

        private int selectedA = -1;
        private int selectedB = -1;

        void Start()
        {
            p1IsAI = PlayerPrefs.GetInt("P1IsAI", p1IsAI ? 1 : 0) == 1;
            p2IsAI = PlayerPrefs.GetInt("P2IsAI", p2IsAI ? 1 : 0) == 1;

            EnsureControlModeUI();

            // Auto-assign: find Faith char for A, Ideology for B
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].faction == "Faith"     && selectedA < 0) SelectA(i);
                if (characters[i].faction == "Ideology"  && selectedB < 0) SelectB(i);
            }

            RefreshControlModeUI();
        }

        public void SetP1IsAI(bool isAI)
        {
            p1IsAI = isAI;
            PlayerPrefs.SetInt("P1IsAI", isAI ? 1 : 0);
            RefreshControlModeUI();
        }

        public void SetP2IsAI(bool isAI)
        {
            p2IsAI = isAI;
            PlayerPrefs.SetInt("P2IsAI", isAI ? 1 : 0);
            RefreshControlModeUI();
        }

        public void ToggleP1ControlMode() => SetP1IsAI(!p1IsAI);
        public void ToggleP2ControlMode() => SetP2IsAI(!p2IsAI);

        void RefreshControlModeUI()
        {
            UpdatePairHighlight(p1HumanBtn, p1AIBtn, !p1IsAI);
            UpdatePairHighlight(p2HumanBtn, p2AIBtn, !p2IsAI);

            if (tagTextA) tagTextA.text = (p1IsAI ? "CPU 1" : "JOGADOR 1") + "  ·  ORDEM DA FÉ";
            if (tagTextB) tagTextB.text = (p2IsAI ? "CPU 2" : "JOGADOR 2") + "  ·  VANGUARDA IDEOLÓGICA";
        }

        static void UpdatePairHighlight(Button humanBtn, Button aiBtn, bool humanActive)
        {
            ApplyToggleVisual(humanBtn, humanActive);
            ApplyToggleVisual(aiBtn,    !humanActive);
        }

        static void ApplyToggleVisual(Button btn, bool active)
        {
            if (!btn) return;
            var juice = btn.GetComponent<UIButtonJuice>();
            if (juice)
            {
                juice.SetSelected(active);
                return;
            }
            if (btn.image)
                btn.image.color = active ? new Color(0.88f, 0.70f, 0.24f, 1f) : new Color(0.06f, 0.05f, 0.09f, 0.92f);
            var lbl = btn.GetComponentInChildren<Text>();
            if (lbl)
                lbl.color = active ? new Color(0.05f, 0.03f, 0.02f, 1f) : new Color(0.96f, 0.93f, 0.86f, 1f);
        }

        void EnsureControlModeUI()
        {
            if (p1HumanBtn && p1AIBtn && p2HumanBtn && p2AIBtn) return;

            GameObject cardA = GameObject.Find("PlayerCardA");
            GameObject cardB = GameObject.Find("PlayerCardB");
            if (!cardA || !cardB) return;

            Transform root = cardA.transform.parent;
            var rtA = cardA.GetComponent<RectTransform>();
            var rtB = cardB.GetComponent<RectTransform>();
            if (rtA && rtA.anchorMin.y < 0.24f) rtA.anchorMin = new Vector2(rtA.anchorMin.x, 0.252f);
            if (rtB && rtB.anchorMin.y < 0.24f) rtB.anchorMin = new Vector2(rtB.anchorMin.x, 0.252f);

            if (!tagTextA) tagTextA = cardA.transform.Find("TagBar/Tag")?.GetComponent<Text>();
            if (!tagTextB) tagTextB = cardB.transform.Find("TagBar/Tag")?.GetComponent<Text>();

            Button templateBtn = buttonsA != null && buttonsA.Length > 0 ? buttonsA[0] : cardA.GetComponentInChildren<Button>(true);
            Sprite panelSprite = cardA.GetComponent<Image>()?.sprite;
            Sprite frameSprite = cardA.transform.Find("Frame")?.GetComponent<Image>()?.sprite;

            if (!p1HumanBtn || !p1AIBtn)
                BuildRuntimeControlBar(root, "ControlBarA", 0.05f, 0.44f, templateBtn, panelSprite, frameSprite, true, out p1HumanBtn, out p1AIBtn);
            if (!p2HumanBtn || !p2AIBtn)
                BuildRuntimeControlBar(root, "ControlBarB", 0.56f, 0.95f, templateBtn, panelSprite, frameSprite, false, out p2HumanBtn, out p2AIBtn);
        }

        void BuildRuntimeControlBar(Transform root, string barName, float x0, float x1,
            Button templateBtn, Sprite panelSprite, Sprite frameSprite, bool isP1,
            out Button humanBtn, out Button aiBtn)
        {
            var barGo = new GameObject(barName, typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(root, false);
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(x0, 0.184f);
            barRt.anchorMax = new Vector2(x1, 0.242f);
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;

            var barImg = barGo.GetComponent<Image>();
            barImg.sprite = panelSprite;
            barImg.type   = panelSprite ? Image.Type.Sliced : Image.Type.Simple;
            barImg.color  = new Color(0.06f, 0.05f, 0.09f, 0.92f);

            if (frameSprite)
            {
                var fr = new GameObject("Frame", typeof(RectTransform), typeof(Image));
                fr.transform.SetParent(barRt, false);
                var frRt = fr.GetComponent<RectTransform>();
                frRt.anchorMin = Vector2.zero; frRt.anchorMax = Vector2.one;
                frRt.offsetMin = Vector2.zero; frRt.offsetMax = Vector2.zero;
                var frImg = fr.GetComponent<Image>();
                frImg.sprite = frameSprite;
                frImg.type   = Image.Type.Sliced;
                frImg.color  = new Color(0.56f, 0.44f, 0.18f, 0.92f);
                frImg.raycastTarget = false;
                var frLe = fr.AddComponent<LayoutElement>();
                frLe.ignoreLayout = true;
            }

            var hlg = barGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding                = new RectOffset(10, 10, 6, 6);
            hlg.spacing                = 10f;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;

            humanBtn = CloneOrCreateControlButton(barRt, "Btn_Jogador", "JOGADOR", templateBtn, () => { if (isP1) SetP1IsAI(false); else SetP2IsAI(false); });
            aiBtn    = CloneOrCreateControlButton(barRt, "Btn_IA",      "IA",      templateBtn, () => { if (isP1) SetP1IsAI(true);  else SetP2IsAI(true);  });
        }

        static Button CloneOrCreateControlButton(Transform parent, string name, string label, Button template, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go;
            if (template)
            {
                go = Instantiate(template.gameObject, parent, false);
                go.name = name;
                var b = go.GetComponent<Button>();
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(onClick);
                var t = go.GetComponentInChildren<Text>(true);
                if (t) { t.text = label; t.fontSize = 20; }
                return b;
            }

            go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var btn = go.GetComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(onClick);

            var lblGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var txt = lblGo.GetComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text      = label;
            txt.fontSize  = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        public void SelectA(int idx)
        {
            selectedA = idx;
            Apply(idx, portraitA, nameA, descA, new Color(0.56f, 0.84f, 1f));
            UpdateRosterHighlight(buttonsA, idx);
        }
        public void SelectB(int idx)
        {
            selectedB = idx;
            Apply(idx, portraitB, nameB, descB, new Color(0.95f, 0.38f, 0.40f));
            UpdateRosterHighlight(buttonsB, idx);
        }

        void UpdateRosterHighlight(Button[] btns, int activeIdx)
        {
            if (btns == null) return;
            for (int i = 0; i < btns.Length; i++)
                if (btns[i]) btns[i].GetComponent<UIButtonJuice>()?.SetSelected(i == activeIdx);
        }

        void Apply(int idx, Image img, Text nm, Text dsc, Color col)
        {
            var c = characters[idx];
            if (img) img.sprite = c.portrait;
            if (nm)  { nm.text = c.characterName.ToUpper(); nm.color = col; }
            if (dsc) dsc.text = c.description;
        }

        public void OnConfirm()
        {
            if (selectedA < 0 || selectedB < 0) return;
            AudioManager.PlayUIConfirm();
            PlayerPrefs.SetInt("SelectedCharA", selectedA);
            PlayerPrefs.SetInt("SelectedCharB", selectedB);
            PlayerPrefs.SetInt("P1IsAI", p1IsAI ? 1 : 0);
            PlayerPrefs.SetInt("P2IsAI", p2IsAI ? 1 : 0);
            PlayerPrefs.Save();
            if (confirmText)
            {
                string mA = p1IsAI ? "IA" : "J1";
                string mB = p2IsAI ? "IA" : "J2";
                confirmText.text = $"✦ {characters[selectedA].characterName.ToUpper()} ({mA})  VS  {characters[selectedB].characterName.ToUpper()} ({mB}) ✦";
            }
            // Go to map select after short delay
            Invoke(nameof(GotoMap), 0.8f);
        }

        void GotoMap()
        {
            if (titleManager) titleManager.GotoMapSelect();
            else SceneManager.LoadScene(nextScene);
        }

        public void OnBack()
        {
            AudioManager.PlayUIBack();
            if (titleManager) titleManager.ReturnToTitle();
            else SceneManager.LoadScene(backScene);
        }
    }
}
