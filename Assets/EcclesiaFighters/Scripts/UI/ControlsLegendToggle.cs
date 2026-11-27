using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    /// <summary>
    /// Toggles the FightScene Controls Legend via button click or hotkey ([TAB]).
    /// Dynamically pulls live bindings from KeybindManager and adapts to 1P, 2P Local, and AI vs AI modes.
    /// </summary>
    public class ControlsLegendToggle : MonoBehaviour
    {
        public GameObject contentPanel;
        public Text       toggleHintText;
        public KeyCode    toggleKey     = KeyCode.Tab;
        public bool       startExpanded = true;

        bool expanded;

        static readonly Color ColIvory      = new Color(0.96f, 0.93f, 0.86f, 1.00f);
        static readonly Color ColBrightGold = new Color(1.00f, 0.85f, 0.36f, 1.00f);
        static readonly Color ColFaithBlue  = new Color(0.56f, 0.84f, 1.00f, 1.00f);

        void Awake()
        {
            // Upgrade legacy serialized KeyCode.H cleanly so it never conflicts with P1 Kick (H).
            if (toggleKey == KeyCode.H) toggleKey = KeyCode.Tab;
        }

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        void OnLanguageChanged(GameLanguage _)
        {
            SyncLegendBindings();
            Refresh();
        }

        void Start()
        {
            expanded = startExpanded;
            SyncLegendBindings();
            Refresh();
        }

        void Update()
        {
            if (FightOverlayController.IsAnyModalOpen) return;
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void Toggle()
        {
            if (FightOverlayController.IsAnyModalOpen) return;
            expanded = !expanded;
            Refresh();
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void SyncLegendBindings()
        {
            if (!contentPanel) return;

            var rootRt = transform as RectTransform;
            if (rootRt && rootRt.sizeDelta.x < 780f)
                rootRt.sizeDelta = new Vector2(780f, Mathf.Max(rootRt.sizeDelta.y, 122f));

            var contentLe = contentPanel.GetComponent<LayoutElement>();
            if (contentLe && contentLe.preferredHeight < 80f)
                contentLe.preferredHeight = 80f;

            var bodyVlg = contentPanel.GetComponent<VerticalLayoutGroup>();
            if (bodyVlg)
            {
                bodyVlg.padding = new RectOffset(16, 16, 10, 10);
                bodyVlg.spacing = 6f;
            }

            Transform moveTf = contentPanel.transform.Find("MoveControls");
            Transform atkTf  = contentPanel.transform.Find("AttackControls");
            Text moveTxt = moveTf ? moveTf.GetComponent<Text>() : null;
            Text atkTxt  = atkTf  ? atkTf.GetComponent<Text>()  : null;
            if (!moveTxt && !atkTxt) return;

            ConfigureLegendLine(moveTxt);
            ConfigureLegendLine(atkTxt);

            bool p1AI = PlayerPrefs.GetInt("P1IsAI", 0) == 1;
            bool p2AI = PlayerPrefs.GetInt("P2IsAI", 1) == 1;

            if (p1AI && p2AI)
            {
                if (moveTxt)
                {
                    moveTxt.text  = LocalizationManager.Pick("MODO ESPECTADOR  ·  IA vs IA", "SPECTATOR MODE  ·  AI vs AI");
                    moveTxt.color = ColBrightGold;
                }
                if (atkTxt)
                {
                    atkTxt.text   = LocalizationManager.Pick("ESC Pausar  ·  TAB Ocultar Dicas", "ESC Pause  ·  TAB Hide Hints");
                    atkTxt.color  = ColIvory;
                }
            }
            else if (!p1AI && !p2AI)
            {
                string p1Pre = LocalizationManager.Pick("J1: ", "P1: ");
                string p2Pre = LocalizationManager.Pick("J2: ", "P2: ");
                if (moveTxt) { moveTxt.text = p1Pre + FormatCompactPlayer(0); moveTxt.color = ColFaithBlue; }
                if (atkTxt)  { atkTxt.text  = p2Pre + FormatCompactPlayer(1); atkTxt.color  = ColBrightGold; }
            }
            else
            {
                int humanIdx = (p1AI && !p2AI) ? 1 : 0;
                if (moveTxt) { moveTxt.text = KeybindManager.FormatMoveLegend(humanIdx);   moveTxt.color = ColIvory; }
                if (atkTxt)  { atkTxt.text  = KeybindManager.FormatAttackLegend(humanIdx); atkTxt.color  = ColBrightGold; }
            }
        }

        static void ConfigureLegendLine(Text txt)
        {
            if (!txt) return;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize    = 10;
            txt.resizeTextMaxSize    = 15;
            txt.horizontalOverflow   = HorizontalWrapMode.Wrap;
            txt.verticalOverflow     = VerticalWrapMode.Truncate;

            var le = txt.GetComponent<LayoutElement>();
            if (le)
            {
                le.minHeight       = 24f;
                le.preferredHeight = Mathf.Max(le.preferredHeight, 26f);
            }
        }

        static string FormatShortKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftArrow:  return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.UpArrow:    return "↑";
                case KeyCode.DownArrow:  return "↓";
                default:                 return KeybindManager.GetPrettyKeyName(key);
            }
        }

        static string FormatCompactPlayer(int playerIndex)
        {
            string l  = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.MoveLeft));
            string r  = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.MoveRight));
            string j  = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.Jump));
            string c  = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.Crouch));
            string b  = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.Block));
            string lp = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.LightPunch));
            string hp = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.HeavyPunch));
            string k  = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.Kick));
            string sp = FormatShortKey(KeybindManager.GetKey(playerIndex, FighterAction.Special));
            return LocalizationManager.Pick(
                $"{l}/{r}/{j}/{c} Mov · {b} Def · {b}+{c} Carr · {lp}/{hp}/{k}/{sp}",
                $"{l}/{r}/{j}/{c} Move · {b} Blk · {b}+{c} Chg · {lp}/{hp}/{k}/{sp}");
        }

        void Refresh()
        {
            if (contentPanel) contentPanel.SetActive(expanded);
            if (toggleHintText)
            {
                string keyLabel = KeybindManager.GetPrettyKeyName(toggleKey);
                string pauseWord = LocalizationManager.Pick("PAUSAR", "PAUSE");
                toggleHintText.text = expanded
                    ? $"[{keyLabel}] {LocalizationManager.Pick("OCULTAR", "HIDE")}  ·  [ESC] {pauseWord}"
                    : $"[{keyLabel}] {LocalizationManager.Pick("CONTROLES", "CONTROLS")}  ·  [ESC] {pauseWord}";
            }
        }
    }
}
