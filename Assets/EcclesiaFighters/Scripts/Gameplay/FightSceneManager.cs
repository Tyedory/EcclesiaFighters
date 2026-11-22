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

        // ══════════════════════════════════════════════════════
        void Start() => StartCoroutine(Setup());

        IEnumerator Setup()
        {
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

            objA = Instantiate(characterPrefabs[cleanA], spawnA.position, Quaternion.Euler(0, 90, 0));
            objB = Instantiate(characterPrefabs[cleanB], spawnB.position, Quaternion.Euler(0, -90, 0));
            objA.name = $"{NAMES[cleanA]}_P1";
            objB.name = $"{NAMES[cleanB]}_P2";

            ctrlA = objA.GetComponent<FighterController>();
            ctrlB = objB.GetComponent<FighterController>();

            if (ctrlA)
            {
                ctrlA.characterName = NAMES[cleanA];
                ctrlA.ConfigureControlOwnership(playerControlled: !p1IsAI, slotIndex: 0);
                ctrlA.opponent      = objB.transform;
                ctrlA.hpBar         = hpBarA;
                ctrlA.FaceDirection(+1);
                ctrlA.OnKO         += () => EndRound(false);
                ctrlA.SetInputEnabled(false);
                if (nameTagA) nameTagA.text = NAMES[cleanA];
            }
            if (ctrlB)
            {
                ctrlB.characterName = NAMES[cleanB];
                ctrlB.ConfigureControlOwnership(playerControlled: !p2IsAI, slotIndex: 1);
                ctrlB.opponent      = objA.transform;
                ctrlB.hpBar         = hpBarB;
                ctrlB.FaceDirection(-1);
                ctrlB.OnKO         += () => EndRound(true);
                ctrlB.SetInputEnabled(false);
                if (nameTagB) nameTagB.text = NAMES[cleanB];
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
            if (!roundActive) return;
            if (!freezeTimer) timer -= Time.deltaTime;
            if (timerText) timerText.text = Mathf.CeilToInt(Mathf.Max(timer,0)).ToString();
            if (!freezeTimer && timer <= 0) EndRound(ctrlA && ctrlB ? ctrlA.CurrentHP >= ctrlB.CurrentHP : true);
        }

        IEnumerator RoundIntro()
        {
            hud.alpha = 0;
            bool isFinalRound = (round >= roundsToWin * 2 - 1) || (wA == roundsToWin - 1 && wB == roundsToWin - 1 && round > 1);
            string roundBanner = isFinalRound ? "FINAL ROUND" : $"ROUND {round}";
            AudioManager.PlayRoundIntroVoice(round, isFinalRound);
            yield return (showCenterRoutine = StartCoroutine(ShowCenter(roundBanner, 1.2f, 1f)));
            AudioManager.PlayFightVoice();
            yield return (showCenterRoutine = StartCoroutine(ShowCenter("FIGHT!", 0.7f, 1.5f)));
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
            string winnerName = aWins ? (ctrlA ? ctrlA.characterName : "Jogador 1")
                                      : (ctrlB ? ctrlB.characterName : "Jogador 2");
            yield return (showCenterRoutine = StartCoroutine(ShowCenter(winnerName.ToUpper() + " VENCE!", 1.8f, 1f)));
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
            AudioManager.PlayMatchEnd(isVictory);

            string winner = aWins ? (ctrlA ? ctrlA.characterName : "Jogador 1")
                                  : (ctrlB ? ctrlB.characterName : "Jogador 2");
            yield return (showCenterRoutine = StartCoroutine(ShowCenter(winner.ToUpper() + "\nVITÓRIA TOTAL!", 3f, 1.2f)));
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
