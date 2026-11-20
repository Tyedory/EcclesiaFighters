using UnityEngine;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    /// <summary>
    /// Toggles the FightScene Controls Legend via button click or hotkey ([H]).
    /// </summary>
    public class ControlsLegendToggle : MonoBehaviour
    {
        public GameObject contentPanel;
        public Text       toggleHintText;
        public KeyCode    toggleKey     = KeyCode.H;
        public bool       startExpanded = true;

        bool expanded;

        void Start()
        {
            expanded = startExpanded;
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

        void Refresh()
        {
            if (contentPanel) contentPanel.SetActive(expanded);
            if (toggleHintText) toggleHintText.text = expanded ? "[H] OCULTAR" : "[H] CONTROLES";
        }
    }
}
