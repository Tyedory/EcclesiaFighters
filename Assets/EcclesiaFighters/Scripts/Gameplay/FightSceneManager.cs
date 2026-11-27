using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EcclesiaFighters
{
    /// <summary>
    /// Manages round flow: intro → fight → K.O. → next round / game over.
    /// Attach to a single "GameManager" GameObject in FightScene.
    /// </summary>
    public class FightSceneManager : MonoBehaviour
    {
        // ── Spawn ──────────────────────────────────────────────
        [Header("Spawn Points")]
        public Transform spawnA;          // left  (-3, 0, 0)
        public Transform spawnB;          // right (+3, 0, 0)

        [Header("Character Prefabs  [0]=Agostinho  [1]=Marx")]
        public GameObject[] characterPrefabs;

        // ── Map / Camera ───────────────────────────────────────
        [Header("Background Sprites  [0]=Exterior  [1]=Interior")]
        public Sprite[] mapBackgrounds;
        public SpriteRenderer backgroundRenderer;

        [Header("Camera Positions (Legacy Fallback)")]
        public Transform camExterior;
        public Transform camInterior;

        [Header("3D Church Stage & Dynamic Camera")]
        public ChurchStageController stageController;
        public FighterCamera         fighterCamera;

        // ── HUD ────────────────────────────────────────────────
        [Header("HUD")]
        public Slider   hpBarA, hpBarB;
        public Slider   spBarA, spBarB;
        public Image    spFillA, spFillB;
        public Text     spReadyTextA, spReadyTextB;
        public Text     nameTagA, nameTagB;
        public Text     timerText;
        public Text     winsA_icons, winsB_icons;   // "● ●" style
        public Text     centerText;                  // ROUND 1 / FIGHT! / K.O.
        public CanvasGroup hud;
        public Image    fadeOverlay;

        [Header("Round Settings")]
        public float roundTime     = 99f;
        public int   roundsToWin   = 2;             // best-of-3 (first to 2)

        [Header("Dev / Training Mode")]
        public bool allowDevMode = true;
        [HideInInspector] public bool freezeTimer = false;

        public FighterController FighterA      => ctrlA;
        public FighterController FighterB      => ctrlB;
        public bool              IsRoundActive => roundActive;
        public bool              CanPauseMatch => roundActive && !inMatchEndFlow;

        // ── Runtime ───────────────────────────────────────────
        private FighterController ctrlA, ctrlB;
        private GameObject objA, objB;
        private FightOverlayController fightOverlay;
        private float  timer;
        private bool   roundActive;
        private bool   inMatchEndFlow;
        private int    round = 1;
        private int    wA = 0, wB = 0;
        private Coroutine roundResultRoutine;
        private Coroutine gameOverRoutine;
        private Coroutine showCenterRoutine;

        static readonly string[] NAMES = { "Santo Agostinho", "Karl Marx" };

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        void OnLanguageChanged(GameLanguage _) => ApplyLocalization();

        void ApplyLocalization()
        {
            int idxA = Clamp(PlayerPrefs.GetInt("SelectedCharA", 0));
            int idxB = Clamp(PlayerPrefs.GetInt("SelectedCharB", 1));
            string nA = LocalizationManager.GetFighterName(idxA);
            string nB = LocalizationManager.GetFighterName(idxB);

            if (ctrlA != null) ctrlA.characterName = nA;
            if (ctrlB != null) ctrlB.characterName = nB;
            if (nameTagA != null) nameTagA.text = nA;
            if (nameTagB != null) nameTagB.text = nB;

            string readyLabel = LocalizationManager.Pick("ESPECIAL PRONTO!", "SPECIAL READY!");
            if (spReadyTextA != null) spReadyTextA.text = readyLabel;
            if (spReadyTextB != null) spReadyTextB.text = readyLabel;

            if (timerText != null && timerText.transform.parent != null)
            {
                Transform lblTf = timerText.transform.parent.Find("TimerLabel");
                if (lblTf != null)
                {
                    Text lbl = lblTf.GetComponent<Text>();
                    if (lbl != null) lbl.text = LocalizationManager.Pick("TEMPO", "TIME");
                }
            }
        }

        // ══════════════════════════════════════════════════════
        void Start() => StartCoroutine(Setup());

        IEnumerator Setup()
        {
            roundTime = MatchConfig.MatchDurationSeconds;
            if (timerText != null) timerText.text = Mathf.CeilToInt(roundTime).ToString();

            // Map (0 = Nave Central / Exterior key, 1 = Altar Sagrado / Interior key)
            string map = PlayerPrefs.GetString("SelectedMap", "Exterior");
            int stageIdx = (map == "Interior" || map == "1") ? 1 : 0;
            AudioManager.PlayStageBGM(stageIdx);

            if (!stageController)
                stageController = FindObjectOfType<ChurchStageController>();

            if (!fighterCamera && Camera.main)
                fighterCamera = Camera.main.GetComponent<FighterCamera>() ?? Camera.main.gameObject.AddComponent<FighterCamera>();

            if (stageController)
            {
                if (!stageController.fighterCamera)            stageController.fighterCamera            = fighterCamera;
                if (!stageController.legacyBackgroundRenderer) stageController.legacyBackgroundRenderer = backgroundRenderer;
                stageController.ApplyPreset(stageIdx);
                if (stageController.spawnA) spawnA = stageController.spawnA;
                if (stageController.spawnB) spawnB = stageController.spawnB;
            }
            else
            {
                bool interior = stageIdx == 1;
                if (backgroundRenderer && mapBackgrounds != null && mapBackgrounds.Length > stageIdx)
                    backgroundRenderer.sprite = interior ? mapBackgrounds[1] : mapBackgrounds[0];
                var camTarget = interior ? camInterior : camExterior;
                if (camTarget && Camera.main) Camera.main.transform.SetPositionAndRotation(camTarget.position, camTarget.rotation);
            }

            // Spawn
            int idxA = PlayerPrefs.GetInt("SelectedCharA", 0);
            int idxB = PlayerPrefs.GetInt("SelectedCharB", 1);
            yield return SpawnFighters(idxA, idxB);

            if (stageController)
            {
                stageController.SyncFighterBounds(ctrlA);
                stageController.SyncFighterBounds(ctrlB);
            }
            if (fighterCamera && objA && objB)
                fighterCamera.SetTargets(objA.transform, objB.transform, snapImmediate: true);

            if (allowDevMode)
            {
                var dev = GetComponent<FightDevMode>() ?? gameObject.AddComponent<FightDevMode>();
                dev.Initialize(this);
            }

            fightOverlay = GetComponent<FightOverlayController>() ?? gameObject.AddComponent<FightOverlayController>();
            fightOverlay.Initialize(this);
            EnsureSpecialBarsHUD();
            ApplyLocalization();
            if (hud) hud.alpha = 0f;
            yield return fightOverlay.ShowPreFightSplashAndWait();

            // Intro
            yield return RoundIntro();
            StartRound();
        }

        IEnumerator SpawnFighters(int idxA, int idxB)
        {
            int cleanA  = Clamp(idxA);
            int cleanB  = Clamp(idxB);
            bool p1IsAI = PlayerPrefs.GetInt("P1IsAI", 0) == 1;
            bool p2IsAI = PlayerPrefs.GetInt("P2IsAI", 1) == 1;
            float hpMul = MatchConfig.GetHPMultiplier();
            FighterAI.Difficulty aiDiff = MatchConfig.ToFighterAIDifficulty(MatchConfig.Difficulty);

            objA = Instantiate(characterPrefabs[cleanA], spawnA.position, Quaternion.Euler(0, 90, 0));
            objB = Instantiate(characterPrefabs[cleanB], spawnB.position, Quaternion.Euler(0, -90, 0));
            objA.name = $"{NAMES[cleanA]}_P1";
            objB.name = $"{NAMES[cleanB]}_P2";

            ctrlA = objA.GetComponent<FighterController>();
            ctrlB = objB.GetComponent<FighterController>();

            if (ctrlA != null)
            {
                string dispA = LocalizationManager.GetFighterName(cleanA);
                ctrlA.characterName = dispA;
                FighterAI aiA = ctrlA.ConfigureControlOwnership(playerControlled: !p1IsAI, slotIndex: 0);
                if (aiA != null) aiA.SetDifficulty(aiDiff);
                ctrlA.opponent      = objB.transform;
                ctrlA.hpBar         = hpBarA;
                ctrlA.ApplyHPMultiplier(hpMul);
                ctrlA.FaceDirection(+1);
                ctrlA.OnKO         += () => EndRound(false);
                ctrlA.SetInputEnabled(false);
                if (nameTagA != null) nameTagA.text = dispA;
            }
            if (ctrlB != null)
            {
                string dispB = LocalizationManager.GetFighterName(cleanB);
                ctrlB.characterName = dispB;
                FighterAI aiB = ctrlB.ConfigureControlOwnership(playerControlled: !p2IsAI, slotIndex: 1);
                if (aiB != null) aiB.SetDifficulty(aiDiff);
                ctrlB.opponent      = objA.transform;
                ctrlB.hpBar         = hpBarB;
                ctrlB.ApplyHPMultiplier(hpMul);
                ctrlB.FaceDirection(-1);
                ctrlB.OnKO         += () => EndRound(true);
                ctrlB.SetInputEnabled(false);
                if (nameTagB != null) nameTagB.text = dispB;
            }
            yield return null;
        }

        void StartRound()
        {
            timer          = roundTime;
            roundActive    = true;
            inMatchEndFlow = false;
            ctrlA?.SetInputEnabled(true);
            ctrlB?.SetInputEnabled(true);
        }

        void Update()
        {
            UpdateSpecialBarsHUD();
            if (!roundActive) return;
            if (!freezeTimer) timer -= Time.deltaTime;
            if (timerText) timerText.text = Mathf.CeilToInt(Mathf.Max(timer,0)).ToString();
            if (!freezeTimer && timer <= 0) EndRound(ctrlA && ctrlB ? ctrlA.CurrentHP >= ctrlB.CurrentHP : true);
        }

        void EnsureSpecialBarsHUD()
        {
            if (spBarA != null && spBarB != null)
            {
                if (spReadyTextA != null) spReadyTextA.gameObject.SetActive(false);
                if (spReadyTextB != null) spReadyTextB.gameObject.SetActive(false);
                spBarA.value = 0f;
                spBarB.value = 0f;
                return;
            }

            if (hpBarA != null && spBarA == null)
                BuildRuntimeSpecialBar(hpBarA, nameTagA, isLeft: true, out spBarA, out spFillA, out spReadyTextA);

            if (hpBarB != null && spBarB == null)
                BuildRuntimeSpecialBar(hpBarB, nameTagB, isLeft: false, out spBarB, out spFillB, out spReadyTextB);
        }

        static void BuildRuntimeSpecialBar(Slider hpBar, Text referenceNameTag, bool isLeft,
            out Slider spBar, out Image spFillImg, out Text spReadyText)
        {
            spBar       = null;
            spFillImg   = null;
            spReadyText = null;
            if (hpBar == null || hpBar.transform.parent == null) return;

            Transform clusterTf = hpBar.transform.parent;
            if (clusterTf.parent != null && clusterTf.parent.name == "TopBar")
            {
                RectTransform topBarRt = clusterTf.parent as RectTransform;
                if (topBarRt != null && topBarRt.anchorMin.y > 0.79f)
                    topBarRt.anchorMin = new Vector2(topBarRt.anchorMin.x, 0.785f);
            }

            Transform existingSp = clusterTf.Find(isLeft ? "SPBarA" : "SPBarB");
            if (existingSp != null)
            {
                spBar = existingSp.GetComponent<Slider>();
                Transform fillTf = existingSp.Find("Fill Area/Fill");
                if (fillTf != null) spFillImg = fillTf.GetComponent<Image>();
                Transform readyTf = existingSp.Find("SPReadyText");
                if (readyTf != null) spReadyText = readyTf.GetComponent<Text>();
                if (spBar != null) return;
            }

            Transform namePlateTf = clusterTf.Find("NamePlate");
            if (namePlateTf != null)
            {
                RectTransform npRt = namePlateTf as RectTransform;
                if (npRt != null)
                {
                    npRt.anchorMin = new Vector2(0f, 0.62f);
                    npRt.anchorMax = new Vector2(1f, 0.98f);
                }
            }

            RectTransform hpRt = hpBar.transform as RectTransform;
            if (hpRt != null)
            {
                hpRt.anchorMin = new Vector2(0f, 0.25f);
                hpRt.anchorMax = new Vector2(1f, 0.58f);
            }

            Sprite panelSprite = null;
            Sprite frameSprite = null;
            Sprite sheenSprite = null;
            Transform bgSrc = hpBar.transform.Find("Background");
            if (bgSrc != null)
            {
                Image bgImg = bgSrc.GetComponent<Image>();
                if (bgImg != null) panelSprite = bgImg.sprite;
            }
            Transform frameSrc = hpBar.transform.Find("MetallicFrame");
            if (frameSrc != null)
            {
                Image frImg = frameSrc.GetComponent<Image>();
                if (frImg != null) frameSprite = frImg.sprite;
            }
            Transform sheenSrc = hpBar.transform.Find("Fill Area/Fill/Sheen");
            if (sheenSrc != null)
            {
                Image shImg = sheenSrc.GetComponent<Image>();
                if (shImg != null) sheenSprite = shImg.sprite;
            }

            Font uiFont = referenceNameTag != null && referenceNameTag.font != null
                ? referenceNameTag.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var barGo = new GameObject(isLeft ? "SPBarA" : "SPBarB", typeof(RectTransform));
            barGo.transform.SetParent(clusterTf, false);
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0.02f);
            barRt.anchorMax = new Vector2(1f, 0.21f);
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;

            var troughGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            troughGo.transform.SetParent(barRt, false);
            StretchFullRect(troughGo.GetComponent<RectTransform>());
            var troughImg = troughGo.GetComponent<Image>();
            troughImg.sprite        = panelSprite;
            troughImg.type          = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            troughImg.color         = new Color(0.05f, 0.04f, 0.07f, 0.94f);
            troughImg.raycastTarget = false;

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(barRt, false);
            var faRt = fillAreaGo.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(4f, 3f);
            faRt.offsetMax = new Vector2(-4f, -3f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(faRt, false);
            StretchFullRect(fillGo.GetComponent<RectTransform>());
            spFillImg               = fillGo.GetComponent<Image>();
            spFillImg.sprite        = panelSprite;
            spFillImg.type          = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            spFillImg.color         = new Color(0.92f, 0.62f, 0.14f, 1f);
            spFillImg.raycastTarget = false;

            if (sheenSprite != null)
            {
                var sheenGo = new GameObject("Sheen", typeof(RectTransform), typeof(Image));
                sheenGo.transform.SetParent(fillGo.transform, false);
                StretchFullRect(sheenGo.GetComponent<RectTransform>());
                var sheenImg = sheenGo.GetComponent<Image>();
                sheenImg.sprite        = sheenSprite;
                sheenImg.type          = Image.Type.Sliced;
                sheenImg.color         = new Color(1f, 1f, 1f, 0.65f);
                sheenImg.raycastTarget = false;
            }

            var frameGo = new GameObject("MetallicFrame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(barRt, false);
            StretchFullRect(frameGo.GetComponent<RectTransform>());
            var frameImg = frameGo.GetComponent<Image>();
            frameImg.sprite        = frameSprite;
            frameImg.type          = frameSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            frameImg.color         = frameSprite != null ? new Color(0.56f, 0.44f, 0.19f, 1f) : new Color(0.56f, 0.44f, 0.19f, 0.25f);
            frameImg.raycastTarget = false;

            var readyGo = new GameObject("SPReadyText", typeof(RectTransform), typeof(Text), typeof(Outline));
            readyGo.transform.SetParent(barRt, false);
            StretchFullRect(readyGo.GetComponent<RectTransform>());
            spReadyText                      = readyGo.GetComponent<Text>();
            spReadyText.font                 = uiFont;
            spReadyText.text                 = LocalizationManager.Pick("ESPECIAL PRONTO!", "SPECIAL READY!");
            spReadyText.fontSize             = 14;
            spReadyText.fontStyle            = FontStyle.Bold;
            spReadyText.color                = new Color(1f, 0.97f, 0.86f, 1f);
            spReadyText.alignment            = TextAnchor.MiddleCenter;
            spReadyText.resizeTextForBestFit = true;
            spReadyText.resizeTextMinSize    = 9;
            spReadyText.resizeTextMaxSize    = 15;
            spReadyText.raycastTarget        = false;

            var outline = readyGo.GetComponent<Outline>();
            outline.effectColor    = new Color(0.06f, 0.03f, 0.02f, 0.96f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            readyGo.SetActive(false);

            spBar              = barGo.AddComponent<Slider>();
            spBar.interactable = false;
            spBar.transition   = Selectable.Transition.None;
            spBar.navigation   = new Navigation { mode = Navigation.Mode.None };
            spBar.fillRect     = fillGo.GetComponent<RectTransform>();
            spBar.handleRect   = null;
            spBar.direction    = isLeft ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight;
            spBar.minValue     = 0f;
            spBar.maxValue     = 1f;
            spBar.value        = 0f;
        }

        static void StretchFullRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void UpdateSpecialBarsHUD()
        {
            UpdateSingleSpecialBar(ctrlA, spBarA, spFillA, spReadyTextA);
            UpdateSingleSpecialBar(ctrlB, spBarB, spFillB, spReadyTextB);
        }

        static void UpdateSingleSpecialBar(FighterController fighter, Slider bar, Image fillImg, Text readyTxt)
        {
            if (fighter == null || bar == null) return;

            float target = fighter.SpecialNormalized;
            if (target < bar.value && target < 0.02f)
                bar.value = target;
            else
                bar.value = Mathf.MoveTowards(bar.value, target, Time.deltaTime * 5.0f);

            bool ready = fighter.IsSpecialReady;
            if (fillImg != null)
            {
                if (ready)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.5f);
                    fillImg.color = Color.Lerp(
                        new Color(0.98f, 0.76f, 0.18f, 1f),
                        new Color(1.00f, 0.95f, 0.48f, 1f),
                        pulse);
                }
                else if (fighter.IsCharging)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 13f);
                    fillImg.color = Color.Lerp(
                        new Color(0.90f, 0.55f, 0.12f, 1f),
                        new Color(1.00f, 0.84f, 0.26f, 1f),
                        pulse);
                }
                else
                {
                    fillImg.color = new Color(0.88f, 0.58f, 0.14f, 1f);
                }
            }

            if (readyTxt != null && readyTxt.gameObject.activeSelf != ready)
                readyTxt.gameObject.SetActive(ready);
        }

        IEnumerator RoundIntro()
        {
            hud.alpha = 0;
            bool isFinalRound = (round >= roundsToWin * 2 - 1) || (wA == roundsToWin - 1 && wB == roundsToWin - 1 && round > 1);
            string roundBanner = isFinalRound
                ? LocalizationManager.Pick("ROUND FINAL", "FINAL ROUND")
                : $"ROUND {round}";
            AudioManager.PlayRoundIntroVoice(round, isFinalRound);
            yield return (showCenterRoutine = StartCoroutine(ShowCenter(roundBanner, 1.2f, 1f)));
            AudioManager.PlayFightVoice();
            yield return (showCenterRoutine = StartCoroutine(ShowCenter(LocalizationManager.Pick("LUTE!", "FIGHT!"), 0.7f, 1.5f)));
            yield return FadeFloat(v => hud.alpha = v, 0, 1, 0.3f);
        }

        void EndRound(bool aWins)
        {
            if (!roundActive) return;
            roundActive    = false;
            inMatchEndFlow = true;
            fightOverlay?.ForceClosePauseForEndScreen();
            AudioManager.PlayKO();
            ctrlA?.SetInputEnabled(false);
            ctrlB?.SetInputEnabled(false);
            if (aWins) wA++; else wB++;
            UpdateWinIcons();
            roundResultRoutine = StartCoroutine(RoundResult(aWins));
        }

        IEnumerator RoundResult(bool aWins)
        {
            yield return (showCenterRoutine = StartCoroutine(ShowCenter("K.O.", 1.1f, 1.2f)));
            string winnerName = aWins
                ? (ctrlA != null ? ctrlA.characterName : LocalizationManager.Pick("Jogador 1", "Player 1"))
                : (ctrlB != null ? ctrlB.characterName : LocalizationManager.Pick("Jogador 2", "Player 2"));
            string winMsg = LocalizationManager.Pick(winnerName.ToUpper() + " VENCE!", winnerName.ToUpper() + " WINS!");
            yield return (showCenterRoutine = StartCoroutine(ShowCenter(winMsg, 1.8f, 1f)));
            yield return new WaitForSeconds(0.5f);

            if (wA >= roundsToWin || wB >= roundsToWin)
                gameOverRoutine = StartCoroutine(GameOver(aWins));
            else
            {
                round++;
                ctrlA?.ResetFighter(); objA.transform.position = spawnA.position; objA.transform.rotation = Quaternion.Euler(0, 90,0); ctrlA?.FaceDirection(+1);
                ctrlB?.ResetFighter(); objB.transform.position = spawnB.position; objB.transform.rotation = Quaternion.Euler(0,-90,0); ctrlB?.FaceDirection(-1);
                fighterCamera?.SnapToTargets();
                yield return RoundIntro();
                StartRound();
            }
        }

        IEnumerator GameOver(bool aWins)
        {
            bool p1AI = ctrlA ? !ctrlA.isPlayerControlled : (PlayerPrefs.GetInt("P1IsAI", 0) == 1);
            bool p2AI = ctrlB ? !ctrlB.isPlayerControlled : (PlayerPrefs.GetInt("P2IsAI", 1) == 1);
            bool soloHumanVsAI = p1AI != p2AI;
            bool humanWon      = (!p1AI && aWins) || (!p2AI && !aWins);
            bool isVictory     = !soloHumanVsAI || humanWon;

            int idxA = Clamp(PlayerPrefs.GetInt("SelectedCharA", 0));
            int idxB = Clamp(PlayerPrefs.GetInt("SelectedCharB", 1));
            int winnerIdx = aWins ? idxA : idxB;
            bool churchWon = winnerIdx == 0;
            AudioManager.PlayMatchEnd(isVictory, soloHumanVsAI, churchWon);

            string winner = aWins
                ? (ctrlA != null ? ctrlA.characterName : LocalizationManager.Pick("Jogador 1", "Player 1"))
                : (ctrlB != null ? ctrlB.characterName : LocalizationManager.Pick("Jogador 2", "Player 2"));
            string endBanner = LocalizationManager.Pick(
                winner.ToUpper() + "\nVITÓRIA TOTAL!",
                winner.ToUpper() + "\nTOTAL VICTORY!");
            yield return (showCenterRoutine = StartCoroutine(ShowCenter(endBanner, 3f, 1.2f)));

            float extraVoiceWait = 0f;
            while (AudioManager.IsVoicePlaying && extraVoiceWait < 2.5f)
            {
                extraVoiceWait += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return FadeFloat(v => fadeOverlay.color = new Color(0,0,0,v), 0, 1, 0.6f);
            Time.timeScale = 1f;
            SceneManager.LoadScene("TitleScreen");
        }

        // ══════════════════════════════════════════════════════
        //  Dev / Training Mode Helpers
        // ══════════════════════════════════════════════════════
        public void DevFullHealBoth()
        {
            ctrlA?.FullHeal();
            ctrlB?.FullHeal();
        }

        public void DevFillSpecialBoth()
        {
            if (ctrlA != null) ctrlA.DevFillSpecial();
            if (ctrlB != null) ctrlB.DevFillSpecial();
            UpdateSpecialBarsHUD();
        }

        public void DevResetPositionsAndRevive()
        {
            if (showCenterRoutine  != null) { StopCoroutine(showCenterRoutine);  showCenterRoutine  = null; }
            if (roundResultRoutine != null) { StopCoroutine(roundResultRoutine); roundResultRoutine = null; }
            if (gameOverRoutine    != null) { StopCoroutine(gameOverRoutine);    gameOverRoutine    = null; }

            if (centerText)  centerText.gameObject.SetActive(false);
            if (hud)         hud.alpha = 1f;
            if (fadeOverlay) fadeOverlay.color = new Color(0f, 0f, 0f, 0f);

            if (timer <= 0f) timer = roundTime;
            roundActive    = true;
            inMatchEndFlow = false;

            ctrlA?.DevReviveAndResetIdle();
            ctrlB?.DevReviveAndResetIdle();

            if (objA && spawnA)
            {
                var rbA = objA.GetComponent<Rigidbody>();
                if (rbA) rbA.velocity = Vector3.zero;
                objA.transform.SetPositionAndRotation(spawnA.position, Quaternion.Euler(0f, 90f, 0f));
                ctrlA?.FaceDirection(+1);
            }
            if (objB && spawnB)
            {
                var rbB = objB.GetComponent<Rigidbody>();
                if (rbB) rbB.velocity = Vector3.zero;
                objB.transform.SetPositionAndRotation(spawnB.position, Quaternion.Euler(0f, -90f, 0f));
                ctrlB?.FaceDirection(-1);
            }

            ctrlA?.SetInputEnabled(true);
            ctrlB?.SetInputEnabled(true);
            fighterCamera?.SnapToTargets();
        }

        public void DevInstaKill(bool killPlayerA)
        {
            FighterController target = killPlayerA ? ctrlA : ctrlB;
            if (!target || target.IsDead) return;

            if (showCenterRoutine  != null) { StopCoroutine(showCenterRoutine);  showCenterRoutine  = null; }
            if (roundResultRoutine != null) { StopCoroutine(roundResultRoutine); roundResultRoutine = null; }
            if (gameOverRoutine    != null) { StopCoroutine(gameOverRoutine);    gameOverRoutine    = null; }

            roundActive       = true;
            target.infiniteHP = false;
            target.DevForceKill(triggerKOEvent: true);
        }

        void UpdateWinIcons()
        {
            if (winsA_icons) winsA_icons.text = new string('●', wA) + new string('○', roundsToWin - wA);
            if (winsB_icons) winsB_icons.text = new string('●', wB) + new string('○', roundsToWin - wB);
        }

        IEnumerator ShowCenter(string msg, float hold, float fontSize = 1f)
        {
            centerText.gameObject.SetActive(true);
            centerText.text = msg;
            yield return FadeFloat(v => { var c = centerText.color; c.a = v; centerText.color = c; }, 0, 1, 0.15f);
            yield return new WaitForSeconds(hold);
            yield return FadeFloat(v => { var c = centerText.color; c.a = v; centerText.color = c; }, 1, 0, 0.15f);
            centerText.gameObject.SetActive(false);
        }

        IEnumerator FadeFloat(System.Action<float> setter, float from, float to, float dur)
        {
            float t = 0; setter(from);
            while (t < dur) { t += Time.deltaTime; setter(Mathf.Lerp(from,to,t/dur)); yield return null; }
            setter(to);
        }

        int Clamp(int i) => Mathf.Clamp(i, 0, characterPrefabs.Length - 1);
    }
}
