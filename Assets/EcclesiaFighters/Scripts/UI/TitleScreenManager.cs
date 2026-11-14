using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EcclesiaFighters
{
    public class TitleScreenManager : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject titlePanel;
        public GameObject charSelectPanel;
        public GameObject mapSelectPanel;
        public GameObject settingsPanel;

        [Header("Fade & FX")]
        public Image fadeOverlay;
        public Image lightningFlash;
        public CanvasGroup titleLogoGroup;
        public CanvasGroup menuGroup;

        [Header("Audio")]
        public AudioSource bgmSource;
        public AudioClip   bgmTitleClip;   // BGM_TitleScreen.mp3
        public AudioClip   bgmCharClip;    // BGM_CharSelect_JesuJoy.mp3

        [Header("Menu Buttons")]
        public Button[] menuButtons;

        [Header("Scene Flow (used when the target panel is not part of this scene)")]
        public string charSelectScene = "CharSelect";
        public string mapSelectScene  = "MapSelect";

        private int selectedIndex = 0;
        private readonly Color normalColor   = new Color(0.94f, 0.91f, 0.85f);
        private readonly Color selectedColor = new Color(0.99f, 0.78f, 0.20f);

        void Start()
        {
            titlePanel.SetActive(true);
            if (charSelectPanel) charSelectPanel.SetActive(false);
            if (mapSelectPanel)  mapSelectPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            fadeOverlay.color = Color.black;
            bgmSource.clip = bgmTitleClip;
            bgmSource.loop = true;
            bgmSource.Play();
            StartCoroutine(IntroSequence());
            StartCoroutine(LightningLoop());
        }

        void Update()
        {
            if (!titlePanel.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.DownArrow))
                UpdateSel((selectedIndex + 1) % menuButtons.Length);
            if (Input.GetKeyDown(KeyCode.UpArrow))
                UpdateSel((selectedIndex + menuButtons.Length - 1) % menuButtons.Length);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                menuButtons[selectedIndex].onClick.Invoke();
        }

        IEnumerator IntroSequence()
        {
            yield return Fade(1, 0, 0.9f);
            yield return FadeGroup(titleLogoGroup, 0, 1, 1.1f);
            yield return new WaitForSeconds(0.4f);
            yield return FadeGroup(menuGroup, 0, 1, 0.8f);
            UpdateSel(0);
        }

        IEnumerator LightningLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(8f + Random.Range(0f, 5f));
                float t = 0f;
                while (t < 0.22f) { t += Time.deltaTime; lightningFlash.color = new Color(1,1,1,Mathf.Sin(t/0.22f*Mathf.PI)*0.12f); yield return null; }
                lightningFlash.color = Color.clear;
            }
        }

        public void UpdateSel(int idx)
        {
            selectedIndex = idx;
            for (int i = 0; i < menuButtons.Length; i++)
            {
                var txt = menuButtons[i].GetComponentInChildren<Text>();
                if (txt) txt.color = i == idx ? selectedColor : normalColor;
            }
        }

        // Called by "Iniciar Batalha" button
        // (panel flow if the panel lives in this scene, otherwise load the CharSelect/MapSelect scene)
        public void GotoCharSelect()
        {
            if (charSelectPanel) StartCoroutine(TransitionTo(charSelectPanel, bgmCharClip));
            else                 StartCoroutine(FadeAndLoad(charSelectScene));
        }
        public void GotoSettings()
        {
            if (settingsPanel) StartCoroutine(TransitionTo(settingsPanel, null));
        }
        public void GotoMapSelect()
        {
            if (mapSelectPanel) StartCoroutine(TransitionToMap());
            else                StartCoroutine(FadeAndLoad(mapSelectScene));
        }

        // Called by the "Sair" button
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator FadeAndLoad(string sceneName)
        {
            yield return Fade(0, 1, 0.4f);
            SceneManager.LoadScene(sceneName);
        }

        IEnumerator TransitionTo(GameObject target, AudioClip clip)
        {
            yield return Fade(0, 1, 0.4f);
            if (clip != null && bgmSource.clip != clip) { bgmSource.Stop(); bgmSource.clip = clip; bgmSource.Play(); }
            titlePanel.SetActive(false);
            target.SetActive(true);
            yield return Fade(1, 0, 0.4f);
        }

        IEnumerator TransitionToMap()
        {
            yield return Fade(0, 1, 0.35f);
            charSelectPanel.SetActive(false);
            mapSelectPanel.SetActive(true);
            yield return Fade(1, 0, 0.35f);
        }

        public void ReturnToTitle()   => StartCoroutine(BackToTitle());
        public void ReturnToCharSel() => StartCoroutine(BackToChar());

        IEnumerator BackToTitle()
        {
            yield return Fade(0, 1, 0.35f);
            bgmSource.Stop(); bgmSource.clip = bgmTitleClip; bgmSource.Play();
            if (charSelectPanel) charSelectPanel.SetActive(false);
            if (mapSelectPanel)  mapSelectPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            titlePanel.SetActive(true);
            yield return Fade(1, 0, 0.35f);
        }

        IEnumerator BackToChar()
        {
            yield return Fade(0, 1, 0.3f);
            mapSelectPanel.SetActive(false);
            charSelectPanel.SetActive(true);
            yield return Fade(1, 0, 0.3f);
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            float t = 0; fadeOverlay.color = new Color(0,0,0,from);
            while (t < dur) { t += Time.deltaTime; fadeOverlay.color = new Color(0,0,0,Mathf.Lerp(from,to,t/dur)); yield return null; }
            fadeOverlay.color = new Color(0,0,0,to);
        }

        IEnumerator FadeGroup(CanvasGroup g, float from, float to, float dur)
        {
            float t = 0; g.alpha = from;
            while (t < dur) { t += Time.deltaTime; g.alpha = Mathf.Lerp(from,to,t/dur); yield return null; }
            g.alpha = to;
        }
    }
}
