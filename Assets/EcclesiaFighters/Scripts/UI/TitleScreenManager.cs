using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        private bool introPlaying = false;
        private bool menuReady    = false;
        private Coroutine introRoutine;
        private Coroutine enableMenuRoutine;

        private readonly Color normalColor   = new Color(0.96f, 0.93f, 0.86f);
        private readonly Color selectedColor = new Color(1.00f, 0.84f, 0.30f);

        void Start()
        {
            if (titlePanel != null)      titlePanel.SetActive(true);
            if (charSelectPanel != null) charSelectPanel.SetActive(false);
            if (mapSelectPanel != null)  mapSelectPanel.SetActive(false);
            if (settingsPanel != null)   settingsPanel.SetActive(false);
            if (fadeOverlay != null)     fadeOverlay.color = Color.black;

            introPlaying = true;
            menuReady    = false;
            SetGroupState(titleLogoGroup, alpha: 0f, interactive: false);
            SetGroupState(menuGroup,      alpha: 0f, interactive: false);

            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.playOnAwake = false;
                bgmSource.enabled     = false;
            }
            AudioManager.PlayTitleBGM();

            introRoutine = StartCoroutine(IntroSequence());
            StartCoroutine(LightningLoop());
        }

        void Update()
        {
            if (titlePanel == null || !titlePanel.activeSelf) return;

            if (introPlaying)
            {
                if (Input.GetMouseButtonDown(0) ||
                    Input.GetKeyDown(KeyCode.Space) ||
                    Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SkipIntroImmediately();
                }
                return;
            }

            if (!menuReady || menuButtons == null || menuButtons.Length == 0) return;

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                AudioManager.PlayUIHover();
                UpdateSel((selectedIndex + 1) % menuButtons.Length);
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                AudioManager.PlayUIHover();
                UpdateSel((selectedIndex + menuButtons.Length - 1) % menuButtons.Length);
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                if (menuButtons[selectedIndex] != null)
                    menuButtons[selectedIndex].onClick.Invoke();
            }
        }

        IEnumerator IntroSequence()
        {
            yield return Fade(1f, 0f, 0.9f);
            yield return FadeGroup(titleLogoGroup, 0f, 1f, 1.1f);
            yield return new WaitForSeconds(0.4f);
            yield return FadeGroup(menuGroup, 0f, 1f, 0.8f);

            introPlaying = false;
            introRoutine = null;
            UpdateSel(0);
            SetMenuInteractive(true);
        }

        void SkipIntroImmediately()
        {
            if (!introPlaying) return;
            introPlaying = false;

            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }

            if (fadeOverlay != null) fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
            SetGroupState(titleLogoGroup, alpha: 1f, interactive: false);
            // Reveal buttons visually right away, but delay raycast/click readiness until skip input releases
            SetGroupState(menuGroup,      alpha: 1f, interactive: false);

            UpdateSel(0);
            ClearEventSystemSelection();

            if (enableMenuRoutine != null) StopCoroutine(enableMenuRoutine);
            enableMenuRoutine = StartCoroutine(EnableMenuAfterInputConsumed());
        }

        IEnumerator EnableMenuAfterInputConsumed()
        {
            yield return null;
            while (Input.GetMouseButton(0)) yield return null;
            yield return null;
            SetMenuInteractive(true);
            enableMenuRoutine = null;
        }

        void SetMenuInteractive(bool interactive)
        {
            menuReady = interactive;
            if (menuGroup != null)
            {
                menuGroup.interactable   = interactive;
                menuGroup.blocksRaycasts = interactive;
            }
        }

        static void SetGroupState(CanvasGroup group, float alpha, bool interactive)
        {
            if (group == null) return;
            group.alpha          = alpha;
            group.interactable   = interactive;
            group.blocksRaycasts = interactive;
        }

        static void ClearEventSystemSelection()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        IEnumerator LightningLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(8f + Random.Range(0f, 5f));
                float t = 0f;
                while (t < 0.22f)
                {
                    t += Time.deltaTime;
                    if (lightningFlash != null)
                        lightningFlash.color = new Color(1, 1, 1, Mathf.Sin(t / 0.22f * Mathf.PI) * 0.12f);
                    yield return null;
                }
                if (lightningFlash != null) lightningFlash.color = Color.clear;
            }
        }

        public void UpdateSel(int idx)
        {
            if (menuButtons == null || menuButtons.Length == 0) return;
            selectedIndex = idx;
            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] == null) continue;
                var txt = menuButtons[i].GetComponentInChildren<Text>();
                if (txt != null) txt.color = i == idx ? selectedColor : normalColor;
                var juice = menuButtons[i].GetComponent<UIButtonJuice>();
                if (juice != null) juice.SetSelected(i == idx);
            }
        }

        // Called by "Iniciar Batalha" button
        public void GotoCharSelect()
        {
            if (!menuReady) return;
            if (charSelectPanel != null) StartCoroutine(TransitionTo(charSelectPanel, bgmCharClip));
            else                         StartCoroutine(FadeAndLoad(charSelectScene));
        }

        public void GotoSettings()
        {
            if (!menuReady) return;
            if (settingsPanel != null) StartCoroutine(TransitionTo(settingsPanel, null));
        }

        public void GotoMapSelect()
        {
            if (mapSelectPanel != null) StartCoroutine(TransitionToMap());
            else                        StartCoroutine(FadeAndLoad(mapSelectScene));
        }

        // Called by the "Sair" button
        public void QuitGame()
        {
            if (!menuReady) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator FadeAndLoad(string sceneName)
        {
            SetMenuInteractive(false);
            yield return Fade(0f, 1f, 0.4f);
            SceneManager.LoadScene(sceneName);
        }

        IEnumerator TransitionTo(GameObject target, AudioClip clip)
        {
            SetMenuInteractive(false);
            yield return Fade(0f, 1f, 0.4f);
            if (target == charSelectPanel) AudioManager.PlayCharSelectBGM();
            if (titlePanel != null) titlePanel.SetActive(false);
            if (target != null)     target.SetActive(true);
            yield return Fade(1f, 0f, 0.4f);
        }

        IEnumerator TransitionToMap()
        {
            yield return Fade(0f, 1f, 0.35f);
            if (charSelectPanel != null) charSelectPanel.SetActive(false);
            if (mapSelectPanel != null)  mapSelectPanel.SetActive(true);
            yield return Fade(1f, 0f, 0.35f);
        }

        public void ReturnToTitle()   { AudioManager.PlayUIBack(); StartCoroutine(BackToTitle()); }
        public void ReturnToCharSel() { AudioManager.PlayUIBack(); StartCoroutine(BackToChar()); }

        IEnumerator BackToTitle()
        {
            SetMenuInteractive(false);
            yield return Fade(0f, 1f, 0.35f);
            AudioManager.PlayTitleBGM();
            if (charSelectPanel != null) charSelectPanel.SetActive(false);
            if (mapSelectPanel != null)  mapSelectPanel.SetActive(false);
            if (settingsPanel != null)   settingsPanel.SetActive(false);
            if (titlePanel != null)      titlePanel.SetActive(true);
            yield return Fade(1f, 0f, 0.35f);
            SetMenuInteractive(true);
        }

        IEnumerator BackToChar()
        {
            yield return Fade(0f, 1f, 0.3f);
            AudioManager.PlayCharSelectBGM();
            if (mapSelectPanel != null)  mapSelectPanel.SetActive(false);
            if (charSelectPanel != null) charSelectPanel.SetActive(true);
            yield return Fade(1f, 0f, 0.3f);
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            float t = 0f;
            if (fadeOverlay != null) fadeOverlay.color = new Color(0f, 0f, 0f, from);
            while (t < dur)
            {
                t += Time.deltaTime;
                if (fadeOverlay != null) fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            if (fadeOverlay != null) fadeOverlay.color = new Color(0f, 0f, 0f, to);
        }

        IEnumerator FadeGroup(CanvasGroup g, float from, float to, float dur)
        {
            if (g == null) yield break;
            float t = 0f;
            g.alpha = from;
            while (t < dur)
            {
                t += Time.deltaTime;
                g.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            g.alpha = to;
        }
    }
}
