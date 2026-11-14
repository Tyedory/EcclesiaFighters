using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
            // Populate VS bar from PlayerPrefs
            // (names are stored as strings via CharacterSelectManager)
            int idxA = PlayerPrefs.GetInt("SelectedCharA", 0);
            int idxB = PlayerPrefs.GetInt("SelectedCharB", 1);
            string[] names = { "Santo Agostinho", "Karl Marx" };
            if (nameA) nameA.text = names[Mathf.Clamp(idxA, 0, names.Length-1)].ToUpper();
            if (nameB) nameB.text = names[Mathf.Clamp(idxB, 0, names.Length-1)].ToUpper();
            goBtn.interactable = false;
        }

        public void SelectExterior()
        {
            selectedMap = MapID.Exterior;
            mapChosen = true;
            goBtn.interactable = true;
            HighlightCard(cardExterior, cardInterior);
        }

        public void SelectInterior()
        {
            selectedMap = MapID.Interior;
            mapChosen = true;
            goBtn.interactable = true;
            HighlightCard(cardInterior, cardExterior);
        }

        void HighlightCard(Image on, Image off)
        {
            // Tint selected card bright, dim the other
            if (on)  on.color  = Color.white;
            if (off) off.color = new Color(0.5f, 0.5f, 0.5f);
        }

        public void OnConfirm()
        {
            if (!mapChosen) return;
            PlayerPrefs.SetString("SelectedMap", selectedMap.ToString());
            PlayerPrefs.Save();
            StartCoroutine(FightIntro());
        }

        IEnumerator FightIntro()
        {
            // Show fight overlay
            fightOverlay.alpha = 0; fightOverlay.gameObject.SetActive(true);
            if (fightMapText)   fightMapText.text   = selectedMap == MapID.Exterior ? "Fachada da Igreja" : "Interior Sagrado";
            if (fightNamesText) fightNamesText.text = nameA.text + " · " + nameB.text;
            // Fade in overlay
            float t = 0;
            while (t < 0.35f) { t += Time.deltaTime; fightOverlay.alpha = t/0.35f; yield return null; }
            fightOverlay.alpha = 1;
            yield return new WaitForSeconds(2.2f);
            // Load fight scene
            SceneManager.LoadScene("FightScene");
        }

        public void OnBack()
        {
            if (titleManager) titleManager.ReturnToCharSel();
            else SceneManager.LoadScene(backScene);
        }
    }
}
