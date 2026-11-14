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

        [Header("Scene Flow (used when titleManager is not in this scene)")]
        public string nextScene = "MapSelect";
        public string backScene = "TitleScreen";

        private int selectedA = -1;
        private int selectedB = -1;

        void Start()
        {
            // Auto-assign: find Faith char for A, Ideology for B
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].faction == "Faith"     && selectedA < 0) SelectA(i);
                if (characters[i].faction == "Ideology"  && selectedB < 0) SelectB(i);
            }
        }

        public void SelectA(int idx) { selectedA = idx; Apply(idx, portraitA, nameA, descA, new Color(0.5f,0.8f,1f)); }
        public void SelectB(int idx) { selectedB = idx; Apply(idx, portraitB, nameB, descB, new Color(1f,0.5f,0.5f)); }

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
            PlayerPrefs.SetInt("SelectedCharA", selectedA);
            PlayerPrefs.SetInt("SelectedCharB", selectedB);
            PlayerPrefs.Save();
            if (confirmText) confirmText.text = "✦ " + characters[selectedA].characterName.ToUpper() + " — CONFIRMADO! ✦";
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
            if (titleManager) titleManager.ReturnToTitle();
            else SceneManager.LoadScene(backScene);
        }
    }
}
