using UnityEngine;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    /// <summary>
    /// Toggles the FightScene Controls Legend via button click or hotkey ([TAB]).
    /// Also syncs legend labels live from KeybindManager for 1P / 2P Local matches.
    /// </summary>
    public class ControlsLegendToggle : MonoBehaviour
    {
        public GameObject contentPanel;
        public Text       toggleHintText;
        public KeyCode    toggleKey     = KeyCode.Tab;
        public bool       startExpanded = true;

        bool expanded;

        void Awake()
        {
            // Upgrade legacy serialized KeyCode.H cleanly so it never conflicts with P1 Kick (H).
            if (toggleKey == KeyCode.H) toggleKey = KeyCode.Tab;
        }

        void Start()
        {
            expanded = startExpanded;
            SyncLegendBindings();
            Refresh();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void Toggle()
        {
            expanded = !expanded;
            Refresh();
        }

        public void SyncLegendBindings()
        {
            if (!contentPanel) return;

            Transform moveTf = contentPanel.transform.Find("MoveControls");
            Transform atkTf  = contentPanel.transform.Find("AttackControls");
            Text moveTxt = moveTf ? moveTf.GetComponent<Text>() : null;
            Text atkTxt  = atkTf  ? atkTf.GetComponent<Text>()  : null;
            if (!moveTxt && !atkTxt) return;

            bool p1AI = PlayerPrefs.GetInt("P1IsAI", 0) == 1;
            bool p2AI = PlayerPrefs.GetInt("P2IsAI", 1) == 1;

            if (!p1AI && !p2AI)
            {
                if (moveTxt) moveTxt.text = "J1: " + FormatCompactPlayer(0);
                if (atkTxt)  atkTxt.text  = "J2: " + FormatCompactPlayer(1);
            }
            else if (p1AI && !p2AI)
            {
                if (moveTxt) moveTxt.text = KeybindManager.FormatMoveLegend(1);
                if (atkTxt)  atkTxt.text  = KeybindManager.FormatAttackLegend(1);
            }
            else
            {
                if (moveTxt) moveTxt.text = KeybindManager.FormatMoveLegend(0);
                if (atkTxt)  atkTxt.text  = KeybindManager.FormatAttackLegend(0);
            }
        }

        static string FormatCompactPlayer(int playerIndex)
        {
            string l  = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.MoveLeft));
            string r  = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.MoveRight));
            string j  = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Jump));
            string c  = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Crouch));
            string b  = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Block));
            string lp = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.LightPunch));
            string hp = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.HeavyPunch));
            string k  = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Kick));
            string sp = KeybindManager.GetPrettyKeyName(KeybindManager.GetKey(playerIndex, FighterAction.Special));
            return $"{l}/{r}/{j}/{c} Mov · {b} Def · {lp}/{hp}/{k}/{sp}";
        }

        void Refresh()
        {
            if (contentPanel) contentPanel.SetActive(expanded);
            if (toggleHintText)
            {
                string keyLabel = KeybindManager.GetPrettyKeyName(toggleKey);
                toggleHintText.text = expanded ? $"[{keyLabel}] OCULTAR" : $"[{keyLabel}] CONTROLES";
            }
        }
    }
}
