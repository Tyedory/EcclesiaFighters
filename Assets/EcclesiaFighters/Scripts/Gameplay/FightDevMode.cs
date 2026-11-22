using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    /// <summary>
    /// Self-contained runtime Dev / Training Mode overlay for FightScene.
    /// Auto-attached by FightSceneManager when allowDevMode == true.
    /// Press F1 to toggle a scrollable panel docked to the right edge of the screen.
    /// </summary>
    public class FightDevMode : MonoBehaviour
    {
        FightSceneManager mgr;

        bool panelOpen;
        bool uiBuilt;
        int  targetIndex; // 0 = P1, 1 = P2
        bool showHurtboxes;
        bool showMoveHitboxes;

        // ── UI Palette ("Sacred Arcade / Baroque Dev") ────────
        static readonly Color ColPanelBg     = new Color(0.05f, 0.045f, 0.07f, 0.90f);
        static readonly Color ColBorderGold  = new Color(0.76f, 0.58f,  0.22f, 0.90f);
        static readonly Color ColHeaderBg    = new Color(0.11f, 0.08f,  0.11f, 0.96f);
        static readonly Color ColSectionBg   = new Color(0.10f, 0.095f, 0.14f, 0.88f);
        static readonly Color ColBtnIdle     = new Color(0.17f, 0.16f,  0.22f, 0.96f);
        static readonly Color ColBtnActive   = new Color(0.78f, 0.59f,  0.20f, 0.98f);
        static readonly Color ColBtnDanger   = new Color(0.55f, 0.13f,  0.14f, 0.96f);
        static readonly Color ColBtnBlue     = new Color(0.16f, 0.34f,  0.50f, 0.96f);
        static readonly Color ColTextLight   = new Color(0.95f, 0.92f,  0.86f, 1.00f);
        static readonly Color ColTextDark    = new Color(0.07f, 0.05f,  0.04f, 1.00f);
        static readonly Color ColTextGold    = new Color(0.94f, 0.78f,  0.38f, 1.00f);
        static readonly Color ColTextMuted   = new Color(0.72f, 0.69f,  0.65f, 1.00f);

        // ── Cached UI References ──────────────────────────────
        GameObject canvasGO;
        Font       uiFont;

        Button aiP1ToggleBtn, aiP2ToggleBtn, freezeTimerBtn;
        Text   aiP1ToggleLbl, aiP2ToggleLbl, freezeTimerLbl;

        Button speed1Btn, speedHalfBtn, speedQuarterBtn;
        Text   speed1Lbl, speedHalfLbl, speedQuarterLbl;

        Button infHpA_Btn, infHpB_Btn;
        Text   infHpA_Lbl, infHpB_Lbl;

        Button hurtboxBtn, moveHitboxBtn;
        Text   hurtboxLbl, moveHitboxLbl;

        Button targetP1Btn, targetP2Btn;
        Text   targetP1Lbl, targetP2Lbl;
        Text   targetStatusText;

        Button crouchToggleBtn, blockToggleBtn;
        Text   crouchToggleLbl, blockToggleLbl;

        // ── Runtime Game-View Hitbox Visualizer Objects ───────
        GameObject hurtboxVisA;
        GameObject hurtboxVisB;
        readonly Dictionary<ProjectileDamage, GameObject> projVisuals = new Dictionary<ProjectileDamage, GameObject>();
        readonly List<ProjectileDamage> staleProjectiles = new List<ProjectileDamage>();

        Material matHurtA;
        Material matHurtB;
        Material matProjHit;

        float nextUiSyncTime;

        public void Initialize(FightSceneManager sceneManager)
        {
            mgr = sceneManager;
        }

        void Awake()
        {
            if (!mgr) mgr = GetComponent<FightSceneManager>();
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        void Update()
        {
            if (!mgr) mgr = GetComponent<FightSceneManager>();
            if (!mgr || !mgr.allowDevMode)
            {
                if (panelOpen) SetPanelOpen(false);
                CleanupVisualizers();
                if (!Mathf.Approximately(Time.timeScale, 1f) && !FightOverlayController.IsPaused) Time.timeScale = 1f;
                return;
            }

            if (Input.GetKeyDown(KeyCode.F1))
                SetPanelOpen(!panelOpen);

            if (panelOpen && Time.unscaledTime >= nextUiSyncTime)
            {
                nextUiSyncTime = Time.unscaledTime + 0.15f;
                RefreshUIState();
            }
        }

        void LateUpdate()
        {
            SyncMoveHitboxFlag(mgr ? mgr.FighterA : null);
            SyncMoveHitboxFlag(mgr ? mgr.FighterB : null);

            if (showHurtboxes && mgr)
            {
                UpdateHurtboxVisual(mgr.FighterA, ref hurtboxVisA, ref matHurtA, new Color(0.15f, 0.95f, 0.72f, 0.34f), "DevHurtbox_P1");
                UpdateHurtboxVisual(mgr.FighterB, ref hurtboxVisB, ref matHurtB, new Color(0.45f, 1.00f, 0.25f, 0.34f), "DevHurtbox_P2");
            }
            else
            {
                DestroySafe(ref hurtboxVisA);
                DestroySafe(ref hurtboxVisB);
            }

            if (showMoveHitboxes)
                UpdateProjectileHitboxVisuals();
            else
                ClearProjectileVisuals();
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            SyncMoveHitboxFlag(mgr ? mgr.FighterA : null, forceFalse: true);
            SyncMoveHitboxFlag(mgr ? mgr.FighterB : null, forceFalse: true);
            CleanupVisualizers();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            CleanupVisualizers();
            DestroySafe(ref canvasGO);
            if (matHurtA)   Destroy(matHurtA);
            if (matHurtB)   Destroy(matHurtB);
            if (matProjHit) Destroy(matProjHit);
        }

        void SetPanelOpen(bool open)
        {
            panelOpen = open;
            if (open && !uiBuilt) BuildOverlayUI();
            if (canvasGO) canvasGO.SetActive(open);
            if (open) RefreshUIState();
            ClearEventSystemSelection();
        }

        // ══════════════════════════════════════════════════════
        //  Visualizer Helpers (Hurtboxes & Projectiles)
        // ══════════════════════════════════════════════════════
        void SyncMoveHitboxFlag(FighterController fighter, bool forceFalse = false)
        {
            if (fighter) fighter.forceShowMoveHitboxes = !forceFalse && showMoveHitboxes;
        }

        void UpdateHurtboxVisual(FighterController fighter, ref GameObject vis, ref Material mat, Color tint, string visName)
        {
            if (!fighter || fighter.IsDead)
            {
                DestroySafe(ref vis);
                return;
            }

            var cap = fighter.GetComponent<CapsuleCollider>();
            if (!cap || !cap.enabled)
            {
                DestroySafe(ref vis);
                return;
            }

            if (!vis)
            {
                vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                vis.name = visName;
                StripCollider(vis);

                var mr = vis.GetComponent<MeshRenderer>();
                if (mr)
                {
                    if (!mat) mat = CreateTransparentMaterial(tint);
                    mr.sharedMaterial    = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows    = false;
                }
            }

            Vector3 worldCenter = cap.transform.TransformPoint(cap.center);
            Vector3 lossy       = cap.transform.lossyScale;
            float scaleY        = Mathf.Abs(lossy.y);
            float scaleXZ       = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
            float worldRadius   = Mathf.Max(0.05f, cap.radius * scaleXZ);
            float worldHeight   = Mathf.Max(worldRadius * 2f, cap.height * scaleY);

            // Default Unity Capsule primitive is 2.0 tall and 1.0 wide (radius 0.5).
            vis.transform.SetPositionAndRotation(worldCenter, Quaternion.identity);
            vis.transform.localScale = new Vector3(worldRadius * 2f, worldHeight * 0.5f, worldRadius * 2f);
        }

        void UpdateProjectileHitboxVisuals()
        {
            staleProjectiles.Clear();
            foreach (var kv in projVisuals)
            {
                if (!kv.Key || !kv.Key.gameObject.activeInHierarchy)
                    staleProjectiles.Add(kv.Key);
            }
            foreach (var stale in staleProjectiles)
            {
                if (projVisuals.TryGetValue(stale, out GameObject go) && go) Destroy(go);
                projVisuals.Remove(stale);
            }

            foreach (ProjectileDamage pd in FindObjectsOfType<ProjectileDamage>())
            {
                if (!pd || !pd.gameObject.activeInHierarchy) continue;

                if (!projVisuals.TryGetValue(pd, out GameObject vis) || !vis)
                {
                    Collider col = pd.GetComponent<Collider>();
                    PrimitiveType shape = col is SphereCollider ? PrimitiveType.Sphere : PrimitiveType.Cube;
                    vis = GameObject.CreatePrimitive(shape);
                    vis.name = "DevProjHitbox_" + pd.name;
                    StripCollider(vis);

                    var mr = vis.GetComponent<MeshRenderer>();
                    if (mr)
                    {
                        if (!matProjHit) matProjHit = CreateTransparentMaterial(new Color(1f, 0.85f, 0.12f, 0.48f));
                        mr.sharedMaterial    = matProjHit;
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                        mr.receiveShadows    = false;
                    }
                    projVisuals[pd] = vis;
                }

                Collider activeCol = pd.GetComponent<Collider>();
                if (activeCol)
                {
                    Bounds b = activeCol.bounds;
                    Vector3 size = b.size;
                    if (size.sqrMagnitude < 0.0001f) size = Vector3.one * 0.45f;
                    vis.transform.SetPositionAndRotation(b.center, Quaternion.identity);
                    vis.transform.localScale = size;
                }
                else
                {
                    vis.transform.SetPositionAndRotation(pd.transform.position, pd.transform.rotation);
                    vis.transform.localScale = Vector3.one * 0.45f;
                }
            }
        }

        void CleanupVisualizers()
        {
            DestroySafe(ref hurtboxVisA);
            DestroySafe(ref hurtboxVisB);
            ClearProjectileVisuals();
        }

        void ClearProjectileVisuals()
        {
            foreach (var kv in projVisuals)
                if (kv.Value) Destroy(kv.Value);
            projVisuals.Clear();
            staleProjectiles.Clear();
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col)
            {
                col.enabled = false;
                Destroy(col);
            }
        }

        static void DestroySafe(ref GameObject go)
        {
            if (go) { Destroy(go); go = null; }
        }

        static Material CreateTransparentMaterial(Color col)
        {
            Shader s = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Transparent")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Standard");

            var mat = new Material(s);
            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Surface"))   mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Mode"))      mat.SetFloat("_Mode", 3f);
            if (mat.HasProperty("_SrcBlend"))  mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))  mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))    mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        // ══════════════════════════════════════════════════════
        //  UI Actions & State Sync
        // ══════════════════════════════════════════════════════
        FighterController GetTargetFighter()
        {
            if (!mgr) return null;
            return targetIndex == 0 ? mgr.FighterA : mgr.FighterB;
        }

        /// <summary>Gets puppet target and automatically disables any active FighterAI on that target.</summary>
        FighterController PreparePuppetTarget()
        {
            FighterController target = GetTargetFighter();
            if (!target) return null;

            var ai = target.GetComponent<FighterAI>();
            if (ai && ai.enabled)
            {
                ai.enabled = false;
                target.DevClearOverrides();
            }
            return target;
        }

        void ToggleAI(int slot)
        {
            FighterController f = mgr ? (slot == 0 ? mgr.FighterA : mgr.FighterB) : null;
            if (!f) return;

            var ai = f.GetComponent<FighterAI>();
            bool currentlyAI = !f.isPlayerControlled && ai && ai.enabled;

            if (currentlyAI)
            {
                if (ai) ai.enabled = false;
                f.DevClearOverrides();
                string prefKey = slot == 0 ? "P1IsAI" : "P2IsAI";
                bool restoreHuman = slot == 0 || PlayerPrefs.GetInt(prefKey, 1) == 0;
                f.isPlayerControlled = restoreHuman;
            }
            else
            {
                f.ConfigureControlOwnership(playerControlled: false, slotIndex: slot);
            }
            FinishClick();
        }

        void ToggleFreezeTimer()
        {
            if (!mgr) return;
            mgr.freezeTimer = !mgr.freezeTimer;
            FinishClick();
        }

        void SetGameSpeed(float speed)
        {
            if (FightOverlayController.IsPaused)
                FightOverlayController.SetResumeTimeScale(speed);
            else
                Time.timeScale = speed;
            FinishClick();
        }

        void ToggleInfiniteHP(bool playerA)
        {
            FighterController f = mgr ? (playerA ? mgr.FighterA : mgr.FighterB) : null;
            if (!f) return;
            f.infiniteHP = !f.infiniteHP;
            if (f.infiniteHP) f.FullHeal();
            FinishClick();
        }

        void ToggleHurtboxes()
        {
            showHurtboxes = !showHurtboxes;
            FinishClick();
        }

        void ToggleMoveHitboxes()
        {
            showMoveHitboxes = !showMoveHitboxes;
            SyncMoveHitboxFlag(mgr ? mgr.FighterA : null);
            SyncMoveHitboxFlag(mgr ? mgr.FighterB : null);
            FinishClick();
        }

        void SelectTarget(int idx)
        {
            targetIndex = idx;
            FinishClick();
        }

        void RunPuppet(Action<FighterController> action)
        {
            FighterController target = PreparePuppetTarget();
            if (target) action(target);
            FinishClick();
        }

        void FinishClick()
        {
            ClearEventSystemSelection();
            RefreshUIState();
        }

        static void ClearEventSystemSelection()
        {
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        void RefreshUIState()
        {
            if (!uiBuilt || !mgr) return;

            FighterController a = mgr.FighterA;
            FighterController b = mgr.FighterB;

            // Independent AI Toggles for P1 and P2
            var aiA  = a ? a.GetComponent<FighterAI>() : null;
            var aiB  = b ? b.GetComponent<FighterAI>() : null;
            bool aiOnA = a && !a.isPlayerControlled && aiA && aiA.enabled;
            bool aiOnB = b && !b.isPlayerControlled && aiB && aiB.enabled;
            SetToggleStyle(aiP1ToggleBtn, aiP1ToggleLbl, aiOnA, "AI P1: ON", "AI P1: OFF");
            SetToggleStyle(aiP2ToggleBtn, aiP2ToggleLbl, aiOnB, "AI P2: ON", "AI P2: OFF");

            // Timer Freeze
            SetToggleStyle(freezeTimerBtn, freezeTimerLbl, mgr.freezeTimer, "TIMER: FROZEN", "TIMER: RUNNING");

            // Game Speed
            float activeSpeed = FightOverlayController.IsPaused ? FightOverlayController.SavedResumeTimeScale : Time.timeScale;
            SetTierStyle(speed1Btn,       speed1Lbl,       Mathf.Abs(activeSpeed - 1.00f) < 0.05f);
            SetTierStyle(speedHalfBtn,    speedHalfLbl,    Mathf.Abs(activeSpeed - 0.50f) < 0.05f);
            SetTierStyle(speedQuarterBtn, speedQuarterLbl, Mathf.Abs(activeSpeed - 0.25f) < 0.05f);

            // Infinite HP
            bool infA = a && a.infiniteHP;
            bool infB = b && b.infiniteHP;
            SetToggleStyle(infHpA_Btn, infHpA_Lbl, infA, "Inf HP P1: ON", "Inf HP P1: OFF");
            SetToggleStyle(infHpB_Btn, infHpB_Lbl, infB, "Inf HP P2: ON", "Inf HP P2: OFF");

            // Hitbox Visualizer
            SetToggleStyle(hurtboxBtn,    hurtboxLbl,    showHurtboxes,    "Hurtboxes: ON",     "Hurtboxes: OFF");
            SetToggleStyle(moveHitboxBtn, moveHitboxLbl, showMoveHitboxes, "Move Hitboxes: ON", "Move Hitboxes: OFF");

            // Puppet Target Selector
            SetTierStyle(targetP1Btn, targetP1Lbl, targetIndex == 0);
            SetTierStyle(targetP2Btn, targetP2Lbl, targetIndex == 1);

            FighterController tgt = GetTargetFighter();
            if (targetStatusText)
            {
                if (!tgt)
                {
                    targetStatusText.text  = "Alvo indisponível";
                    targetStatusText.color = ColTextMuted;
                }
                else
                {
                    string slot  = targetIndex == 0 ? "P1" : "P2";
                    string mode  = tgt.HasAnimatorController ? "Humanoid Animator" : "Placeholder Visuals";
                    string state = tgt.IsDead ? "DEAD" : (tgt.IsAttacking ? tgt.CurrentAttack.ToString() : "Ready");
                    targetStatusText.text  = $"{slot}: {tgt.characterName}  •  [{mode}]  •  {state}";
                    targetStatusText.color = tgt.HasAnimatorController ? ColTextGold : ColTextLight;
                }
            }

            // Posture Toggles on Active Target
            bool crouchOn = tgt && tgt.DevCrouchActive;
            bool blockOn  = tgt && tgt.DevBlockActive;
            SetToggleStyle(crouchToggleBtn, crouchToggleLbl, crouchOn, "Crouch: ON", "Crouch: OFF");
            SetToggleStyle(blockToggleBtn,  blockToggleLbl,  blockOn,  "Block: ON",  "Block: OFF");
        }

        static void SetToggleStyle(Button btn, Text lbl, bool active, string onText, string offText)
        {
            if (lbl) lbl.text = active ? onText : offText;
            SetTierStyle(btn, lbl, active);
        }

        static void SetTierStyle(Button btn, Text lbl, bool active)
        {
            if (btn && btn.image) btn.image.color = active ? ColBtnActive : ColBtnIdle;
            if (lbl) lbl.color = active ? ColTextDark : ColTextLight;
        }

        // ══════════════════════════════════════════════════════
        //  UI Construction (Docked Right Scrollable Overlay)
        // ══════════════════════════════════════════════════════
        void BuildOverlayUI()
        {
            uiBuilt = true;

            if (!FindObjectOfType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            canvasGO = new GameObject("DevModeOverlayCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Docked Right Panel (unobtrusive to center arena and top HUD bars)
            var panel = CreatePanel("DevPanel", canvasGO.transform, ColPanelBg);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin        = new Vector2(1f, 0.03f);
            panelRt.anchorMax        = new Vector2(1f, 0.85f);
            panelRt.pivot            = new Vector2(1f, 0.5f);
            panelRt.anchoredPosition = new Vector2(-12f, 0f);
            panelRt.sizeDelta        = new Vector2(368f, 0f);

            var outline = panel.AddComponent<Outline>();
            outline.effectColor    = ColBorderGold;
            outline.effectDistance = new Vector2(2f, -2f);

            // Header bar
            var header = CreatePanel("Header", panel.transform, ColHeaderBg);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin        = new Vector2(0f, 1f);
            headerRt.anchorMax        = new Vector2(1f, 1f);
            headerRt.pivot            = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta        = new Vector2(0f, 40f);

            var headerText = CreateLabel("HeaderTitle", header.transform, "DEV / TRAINING MODE  [F1]", 15, FontStyle.Bold, ColTextGold, TextAnchor.MiddleLeft);
            var hdrTxtRt = headerText.GetComponent<RectTransform>();
            hdrTxtRt.anchorMin = Vector2.zero; hdrTxtRt.anchorMax = Vector2.one;
            hdrTxtRt.offsetMin = new Vector2(12f, 0f); hdrTxtRt.offsetMax = new Vector2(-46f, 0f);

            CreateStyledButton(header.transform, "X", ColBtnDanger, ColTextLight, () => SetPanelOpen(false), 32f, 28f, isFixedHeaderBtn: true);

            // ScrollView Area
            var scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGO.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(6f, 6f);
            scrollRt.offsetMax = new Vector2(-6f, -44f);

            var viewport = CreatePanel("Viewport", scrollGO.transform, new Color(0f, 0f, 0f, 0.01f));
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = new Vector2(-10f, 0f);
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding                = new RectOffset(4, 4, 4, 8);
            vlg.spacing                = 8f;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Vertical Scrollbar
            var sbGO = CreatePanel("Scrollbar", scrollGO.transform, new Color(0.08f, 0.08f, 0.11f, 0.7f));
            var sbRt = sbGO.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f); sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot     = new Vector2(1f, 0.5f);
            sbRt.sizeDelta = new Vector2(8f, 0f);

            var handleGO = CreatePanel("Handle", sbGO.transform, ColBorderGold);
            var handleRt = handleGO.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero; handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero; handleRt.offsetMax = Vector2.zero;

            var sb = sbGO.AddComponent<Scrollbar>();
            sb.direction  = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handleRt;

            var scrollRect = scrollGO.GetComponent<ScrollRect>();
            scrollRect.viewport          = vpRt;
            scrollRect.content           = contentRt;
            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            scrollRect.scrollSensitivity = 28f;
            scrollRect.movementType      = ScrollRect.MovementType.Clamped;
            scrollRect.verticalScrollbar = sb;

            // ── SECTION 1: Match & Cheat Controls ─────────────────
            Transform sMatch = CreateSection(content.transform, "MATCH & CHEATS");
            Transform rowAI = CreateRow(sMatch);
            (aiP1ToggleBtn, aiP1ToggleLbl) = CreateButton(rowAI, "AI P1: OFF", ColBtnIdle, () => ToggleAI(0));
            (aiP2ToggleBtn, aiP2ToggleLbl) = CreateButton(rowAI, "AI P2: ON",  ColBtnIdle, () => ToggleAI(1));

            Transform rowTimer = CreateRow(sMatch);
            (freezeTimerBtn, freezeTimerLbl) = CreateButton(rowTimer, "TIMER: RUNNING", ColBtnIdle, ToggleFreezeTimer);

            CreateSubLabel(sMatch, "Game Speed (Time.timeScale)");
            Transform rowSpeed = CreateRow(sMatch);
            (speed1Btn, speed1Lbl)             = CreateButton(rowSpeed, "1.0x",  ColBtnActive, () => SetGameSpeed(1.0f));
            (speedHalfBtn, speedHalfLbl)       = CreateButton(rowSpeed, "0.5x",  ColBtnIdle,   () => SetGameSpeed(0.5f));
            (speedQuarterBtn, speedQuarterLbl) = CreateButton(rowSpeed, "0.25x", ColBtnIdle,   () => SetGameSpeed(0.25f));

            Transform rowInf = CreateRow(sMatch);
            (infHpA_Btn, infHpA_Lbl) = CreateButton(rowInf, "Inf HP P1: OFF", ColBtnIdle, () => ToggleInfiniteHP(true));
            (infHpB_Btn, infHpB_Lbl) = CreateButton(rowInf, "Inf HP P2: OFF", ColBtnIdle, () => ToggleInfiniteHP(false));

            Transform rowHeal = CreateRow(sMatch);
            CreateButton(rowHeal, "Full Heal Both",  ColBtnBlue, () => { mgr?.DevFullHealBoth(); FinishClick(); });
            CreateButton(rowHeal, "Reset Positions", ColBtnBlue, () => { mgr?.DevResetPositionsAndRevive(); FinishClick(); });

            Transform rowKO = CreateRow(sMatch);
            CreateButton(rowKO, "Insta-Kill P1", ColBtnDanger, () => { mgr?.DevInstaKill(true);  FinishClick(); });
            CreateButton(rowKO, "Insta-Kill P2", ColBtnDanger, () => { mgr?.DevInstaKill(false); FinishClick(); });

            // ── SECTION 2: Game-View Hitbox Visualizer ────────────
            Transform sVis = CreateSection(content.transform, "GAME-VIEW HITBOX VISUALIZER");
            Transform rowVis = CreateRow(sVis);
            (hurtboxBtn, hurtboxLbl)       = CreateButton(rowVis, "Hurtboxes: OFF",     ColBtnIdle, ToggleHurtboxes);
            (moveHitboxBtn, moveHitboxLbl) = CreateButton(rowVis, "Move Hitboxes: OFF", ColBtnIdle, ToggleMoveHitboxes);

            // ── SECTION 3: Animation & Action Control Room ────────
            Transform sPuppet = CreateSection(content.transform, "ANIMATION & ACTION CONTROL ROOM");
            Transform rowTgt = CreateRow(sPuppet);
            (targetP1Btn, targetP1Lbl) = CreateButton(rowTgt, "Target: P1", ColBtnActive, () => SelectTarget(0));
            (targetP2Btn, targetP2Lbl) = CreateButton(rowTgt, "Target: P2", ColBtnIdle,   () => SelectTarget(1));

            targetStatusText = CreateSubLabel(sPuppet, "Target ready");

            CreateSubLabel(sPuppet, "Combat Moves (Auto-disables target AI)");
            Transform rowAtk1 = CreateRow(sPuppet);
            CreateButton(rowAtk1, "Light Punch", ColBtnIdle, () => RunPuppet(f => f.DevTriggerAttack(FighterController.AttackType.LightPunch)));
            CreateButton(rowAtk1, "Heavy Punch", ColBtnIdle, () => RunPuppet(f => f.DevTriggerAttack(FighterController.AttackType.HeavyPunch)));

            Transform rowAtk2 = CreateRow(sPuppet);
            CreateButton(rowAtk2, "Kick",    ColBtnIdle, () => RunPuppet(f => f.DevTriggerAttack(FighterController.AttackType.Kick)));
            CreateButton(rowAtk2, "Special", ColBtnIdle, () => RunPuppet(f => f.DevTriggerAttack(FighterController.AttackType.Special)));
            CreateButton(rowAtk2, "Jump",    ColBtnIdle, () => RunPuppet(f => f.DevTriggerJump()));

            CreateSubLabel(sPuppet, "Held States & Timed Walk");
            Transform rowPosture = CreateRow(sPuppet);
            (crouchToggleBtn, crouchToggleLbl) = CreateButton(rowPosture, "Crouch: OFF", ColBtnIdle, () => RunPuppet(f => f.DevToggleCrouch()));
            (blockToggleBtn,  blockToggleLbl)  = CreateButton(rowPosture, "Block: OFF",  ColBtnIdle, () => RunPuppet(f => f.DevToggleBlock()));

            Transform rowWalk = CreateRow(sPuppet);
            CreateButton(rowWalk, "Walk Forward (1s)", ColBtnIdle, () => RunPuppet(f => f.DevWalkTimed(+1, 1.0f)));
            CreateButton(rowWalk, "Walk Back (1s)",    ColBtnIdle, () => RunPuppet(f => f.DevWalkTimed(-1, 1.0f)));

            CreateSubLabel(sPuppet, "Reactions (No Attacker Needed)");
            Transform rowReact = CreateRow(sPuppet);
            CreateButton(rowReact, "Trigger Hit",   ColBtnIdle,   () => RunPuppet(f => f.DevTriggerHit(0f)));
            CreateButton(rowReact, "Trigger Death", ColBtnDanger, () => RunPuppet(f => f.DevForceKill(triggerKOEvent: false)));

            Transform rowRevive = CreateRow(sPuppet);
            CreateButton(rowRevive, "Revive / Reset Idle", ColBtnBlue, () => RunPuppet(f => f.DevReviveAndResetIdle()));

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        }

        // ── Small UI Factory Helpers ──────────────────────────
        static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        Text CreateLabel(string name, Transform parent, string text, int size, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font      = uiFont;
            t.text      = text;
            t.fontSize  = size;
            t.fontStyle = style;
            t.color     = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        Transform CreateSection(Transform parent, string title)
        {
            var box = CreatePanel("Section_" + title, parent, ColSectionBg);
            var vlg = box.AddComponent<VerticalLayoutGroup>();
            vlg.padding                = new RectOffset(8, 8, 6, 8);
            vlg.spacing                = 5f;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var lbl = CreateLabel("Title", box.transform, title, 12, FontStyle.Bold, ColTextGold, TextAnchor.MiddleLeft);
            var le  = lbl.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 18f;
            return box.transform;
        }

        Text CreateSubLabel(Transform parent, string text)
        {
            var lbl = CreateLabel("SubLabel", parent, text, 11, FontStyle.Normal, ColTextMuted, TextAnchor.MiddleLeft);
            var le  = lbl.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 16f;
            return lbl;
        }

        static Transform CreateRow(Transform parent)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 5f;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 30f;
            return row.transform;
        }

        (Button, Text) CreateButton(Transform parent, string text, Color bg, Action onClick)
        {
            return CreateStyledButton(parent, text, bg, ColTextLight, onClick, 0f, 30f, isFixedHeaderBtn: false);
        }

        (Button, Text) CreateStyledButton(
            Transform parent, string text, Color bg, Color fg, Action onClick,
            float width, float height, bool isFixedHeaderBtn)
        {
            var go  = CreatePanel("Btn_" + text, parent, bg);
            var img = go.GetComponent<Image>();

            if (isFixedHeaderBtn)
            {
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(1f, 0.5f);
                rt.anchorMax        = new Vector2(1f, 0.5f);
                rt.pivot            = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-6f, 0f);
                rt.sizeDelta        = new Vector2(width, height);
            }
            else
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredHeight = height;
                if (width > 0f) le.preferredWidth = width;
            }

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation    = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor    = Color.white;
            btn.colors = colors;

            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var lbl = CreateLabel("Label", go.transform, text, 12, FontStyle.Bold, fg, TextAnchor.MiddleCenter);
            var lrt = lbl.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(4f, 0f); lrt.offsetMax = new Vector2(-4f, 0f);

            return (btn, lbl);
        }
    }
}
