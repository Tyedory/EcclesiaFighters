using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    /// <summary>
    /// Handles movement, crouching, blocking, combo-timed attacks, melee hitboxes,
    /// hitstun/knockback, placeholder feedback, and guarded Animator hooks for ONE fighter.
    ///
    /// ── Animator contract (Mixamo-ready) ──────────────────────────────────
    ///   State    "Idle"          (default state; played on ResetFighter)
    ///   Bool     "Walking"       true while walking forward
    ///   Bool     "WalkingBack"   true while walking backward
    ///   Bool     "Crouching"     true while crouched
    ///   Bool     "Blocking"      true while guarding
    ///   Trigger  "Jump"          DoJump()
    ///   Trigger  "LightPunch"    Light punch (falls back to "Punch" if missing)
    ///   Trigger  "HeavyPunch"    Heavy punch (falls back to "Punch" if missing)
    ///   Trigger  "Kick"          Kick
    ///   Trigger  "Special"       Special projectile attack
    ///   Trigger  "Hit"           Unblocked hitstun
    ///   Trigger  "Death"         KO
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Animator))]
    public class FighterController : MonoBehaviour
    {
        /// <summary>How the fighter turns to face left / right.</summary>
        public enum FacingMode
        {
            /// <summary>Rotate the root around Y (±90°). Correct for 3D models.</summary>
            RotateY,
            /// <summary>Mirror transform.localScale.x. Only correct for flat sprites.</summary>
            FlipScaleX
        }

        public enum AttackType { None, LightPunch, HeavyPunch, Kick, Special }
        enum AttackPhase { None, Startup, Active, Recovery }

        [Serializable]
        public struct AttackConfig
        {
            public float damage;
            public float startup;
            public float active;
            public float recovery;
            public float cooldown;
            public float range;
            public Vector3 hitHalfExtents;
            public float knockback;
            public float hitstun;

            public AttackConfig(
                float damage, float startup, float active, float recovery, float cooldown,
                float range, Vector3 hitHalfExtents, float knockback, float hitstun)
            {
                this.damage = damage;
                this.startup = startup;
                this.active = active;
                this.recovery = recovery;
                this.cooldown = cooldown;
                this.range = range;
                this.hitHalfExtents = hitHalfExtents;
                this.knockback = knockback;
                this.hitstun = hitstun;
            }
        }

        // ── Identity ──────────────────────────────────────────
        [Header("Identity")]
        public string characterName = "Fighter";
        public int playerIndex = 0;
        [FormerlySerializedAs("isPlayer")]
        public bool isPlayerControlled = true;

        /// <summary>Backwards-compatible alias for isPlayerControlled.</summary>
        public bool isPlayer
        {
            get => isPlayerControlled;
            set => isPlayerControlled = value;
        }

        // ── Stats & Movement ──────────────────────────────────
        [Header("Stats & Movement")]
        public float maxHP = 100f;
        public float moveSpeed = 4.5f;
        [Range(0.3f, 1f)] public float backWalkSpeedMultiplier = 0.82f;
        public float doubleTapWindow = 0.28f;
        public float sprintForwardMultiplier = 1.75f;
        public float sprintRetreatMultiplier = 1.45f;
        public float sprintJumpAirMultiplier = 1.60f;
        public float minBodySpacing = 0.68f;
        public float bodyOverlapHeightThreshold = 1.35f;
        [Range(0.2f, 0.9f)] public float crouchSpeedMultiplier = 0.45f;
        [Range(0.4f, 0.85f)] public float crouchHeightRatio = 0.60f;
        public float jumpForce = 11.2f;
        public float ascentGravityMultiplier = 2.2f;
        public float fallGravityMultiplier = 3.0f;
        public float blockReduction = 0.15f;   // blocked hit = 15% chip damage
        public float blockKnockbackScale = 0.35f;
        public float knockbackDamping = 18f;
        public float invulnerabilityDuration = 0.22f;

        [Header("Stage Bounds")]
        public float stageMinX = -8.5f;
        public float stageMaxX = 8.5f;

        // ── Attacks ───────────────────────────────────────────
        [Header("Melee Moves")]
        public AttackConfig lightPunch = new AttackConfig(
            8f, 0.07f, 0.09f, 0.14f, 0.08f, 0.52f, new Vector3(0.32f, 0.36f, 0.60f), 2.2f, 0.18f);
        public AttackConfig heavyPunch = new AttackConfig(
            15f, 0.16f, 0.12f, 0.26f, 0.18f, 0.62f, new Vector3(0.37f, 0.38f, 0.60f), 5.0f, 0.32f);
        public AttackConfig kick = new AttackConfig(
            13f, 0.13f, 0.13f, 0.24f, 0.15f, 0.72f, new Vector3(0.42f, 0.38f, 0.60f), 5.8f, 0.28f);

        // ── Facing ────────────────────────────────────────────
        [Header("Facing")]
        public FacingMode facingMode = FacingMode.RotateY;

        // ── Special projectile ────────────────────────────────
        [Header("Special")]
        public float specialDamage = 28f;
        public float specialCooldown = 5f;
        public float specialStartup = 0.16f;
        public float specialActive = 0.10f;
        public float specialRecovery = 0.24f;
        public float specialSpeed = 16f;
        public GameObject specialPrefab;
        [Tooltip("Spawn origin of the special. If empty, a 'FirePoint' child is created at chest height on first use.")]
        public Transform firePoint;
        [Range(0f, 1f)] public float chestHeightRatio = 0.7f;
        public float fallbackChestHeight = 1.3f;
        public float muzzleForwardOffset = 0.55f;

        [Header("Animated Impact Timings (Used when AnimatorController is assigned)")]
        [Tooltip("Seconds from Light Punch input until hitbox activates when animated.")]
        public float lightPunchStartup = 0.11f;
        [Tooltip("Seconds from Heavy Punch input until hitbox activates when animated.")]
        public float heavyPunchStartup = 0.25f;
        [Tooltip("Seconds from Kick input until hitbox activates when animated.")]
        public float kickStartup = 0.24f;
        [Tooltip("Seconds from Special input until projectile spawns at throw release when animated.")]
        public float specialSpawnDelay = 0.37f;

        // ── Controls (Player 1 defaults) ──────────────────────
        [Header("Controls — ignored when isPlayerControlled=false")]
        public KeyCode keyLeft = KeyCode.A;
        public KeyCode keyRight = KeyCode.D;
        public KeyCode keyJump = KeyCode.W;
        public KeyCode keyJumpAlt = KeyCode.Space;
        public KeyCode keyCrouch = KeyCode.S;
        public KeyCode keyBlock = KeyCode.LeftShift;
        public KeyCode keyLightPunch = KeyCode.J;
        public KeyCode keyHeavyPunch = KeyCode.K;
        public KeyCode keyKick = KeyCode.L;
        public KeyCode keySpecial1 = KeyCode.U;
        public KeyCode keySpecial2 = KeyCode.I;

        // ── Placeholder Visual Feedback ───────────────────────
        [Header("Placeholder Visuals (Pre-Animation Testing)")]
        public bool usePlaceholderVisuals = true;
        public Transform visualRoot;

        // ── Dev / Training Flags ──────────────────────────────
        [Header("Dev / Training Flags")]
        public bool infiniteHP = false;
        [HideInInspector] public bool forceShowMoveHitboxes = false;

        // ── References ────────────────────────────────────────
        [HideInInspector] public Transform opponent;
        [HideInInspector] public Slider hpBar;
        [HideInInspector] public Action OnKO;

        // ── Public state (read by FighterAI & HUD) ────────────
        public float CurrentHP => hp;
        public float HPRatio => maxHP > 0f ? Mathf.Clamp01(hp / maxHP) : 0f;
        public bool IsGrounded => grounded;
        public bool IsBlocking => blocking;
        public bool IsCrouching => crouching;
        public bool IsAttacking => isAttacking;
        public bool IsInHitstun => hitstunTimer > 0f;
        public bool InputEnabled => inputEnabled;
        public AttackType CurrentAttack => currentAttack;
        public bool SpecialReady => spCooldown <= 0f;
        public bool IsDead => dead;
        public bool IsSprinting => sprinting;
        public bool DevCrouchActive => devForceCrouch;
        public bool DevBlockActive => devForceBlock;
        public bool HasAnimatorController => animReady;

        /// <summary>+1 = facing right (+X), -1 = facing left (-X). Derived from transform so it cannot desync.</summary>
        public int FacingDir => facingMode == FacingMode.FlipScaleX
            ? (transform.localScale.x >= 0f ? 1 : -1)
            : (transform.forward.x >= 0f ? 1 : -1);

        /// <summary>World-space unit vector the fighter is facing (always along X).</summary>
        public Vector3 FacingVector => new Vector3(FacingDir, 0f, 0f);

        // ── Animator hashes ───────────────────────────────────
        static readonly int HashIdle = Animator.StringToHash("Idle");
        static readonly int HashWalking = Animator.StringToHash("Walking");
        static readonly int HashWalkingBack = Animator.StringToHash("WalkingBack");
        static readonly int HashRunning = Animator.StringToHash("Running");
        static readonly int HashRunningBack = Animator.StringToHash("RunningBack");
        static readonly int HashCrouching = Animator.StringToHash("Crouching");
        static readonly int HashBlocking = Animator.StringToHash("Blocking");
        static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
        static readonly int HashJump = Animator.StringToHash("Jump");
        static readonly int HashLightPunch = Animator.StringToHash("LightPunch");
        static readonly int HashHeavyPunch = Animator.StringToHash("HeavyPunch");
        static readonly int HashPunch = Animator.StringToHash("Punch");
        static readonly int HashKick = Animator.StringToHash("Kick");
        static readonly int HashSpecial = Animator.StringToHash("Special");
        static readonly int HashHit = Animator.StringToHash("Hit");
        static readonly int HashDeath = Animator.StringToHash("Death");

        static readonly int ShaderColorId = Shader.PropertyToID("_Color");
        static readonly int ShaderBaseColorId = Shader.PropertyToID("_BaseColor");

        static Material sharedHitIndicatorMat;
        static Material sharedFallbackProjMat;

        // ── Runtime state ─────────────────────────────────────
        float hp, baseMaxHP, spCooldown, attackCooldownTimer;
        float hitstunTimer, invulnTimer, hitFlashTimer, blockFlashTimer;
        float jumpGraceTimer, lastGroundContactTime = -10f;
        float stageZ;

        bool grounded = true, blocking, crouching, dead, inputEnabled;
        bool hasBeenAirborne;
        float lastSpawnOrResetTime, lastLandSfxTime = -10f;
        bool walkingForward, walkingBackward;
        bool sprinting, isSprintJump, runningForward, runningBackward;
        int sprintInputDir, lastTappedDir;
        float lastLeftTapTime = -10f, lastRightTapTime = -10f;
        bool holdingAway, aiWantsBlock, aiWantsCrouch;
        bool devForceCrouch, devForceBlock, devWalkJustEnded;
        float devWalkTimer;
        int devWalkRelDir;
        bool isAttacking;

        AttackType currentAttack = AttackType.None;
        AttackPhase attackPhase = AttackPhase.None;
        float attackPhaseT;
        Coroutine attackRoutine;

        Rigidbody rb;
        Animator anim;
        CapsuleCollider capsule;
        FighterController cachedOpponentCtrl;

        float origColliderHeight = 1.93f;
        Vector3 origColliderCenter = Vector3.zero;
        float bottomLocalY = -0.965f;

        // ── Visual feedback cache ─────────────────────────────
        Vector3 baseVisualLocalPos;
        Quaternion baseVisualLocalRot;
        Vector3 baseVisualLocalScale;
        Vector3 visualUpWeights = Vector3.up;
        bool hasVisualRoot;
        bool placeholderWasActive;

        Renderer[] cachedRenderers;
        Color[] baseRendererColors;
        MaterialPropertyBlock propBlock;
        float currentVisualSquash = 1f;
        float currentVisualTilt;
        float currentVisualLunge;

        // ── Animator cache ────────────────────────────────────
        readonly Dictionary<int, AnimatorControllerParameterType> animParams =
            new Dictionary<int, AnimatorControllerParameterType>();
        bool animReady;

        // ══════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════
        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            anim = GetComponent<Animator>();
            capsule = GetComponent<CapsuleCollider>();

            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            stageZ = transform.position.z;
            baseMaxHP = Mathf.Max(1f, maxHP);
            lastSpawnOrResetTime = Time.time;

            if (capsule)
            {
                origColliderHeight = capsule.height;
                origColliderCenter = capsule.center;
                bottomLocalY = origColliderCenter.y - origColliderHeight * 0.5f;
            }

            ApplyArcadeCombatTuning();
            CacheAnimator();
            CacheVisuals();
        }

        void ApplyArcadeCombatTuning()
        {
            // Upgrade legacy serialized prefab values to snappy arcade jump & tighter melee hitboxes
            if (jumpForce < 9.5f) jumpForce = 11.2f;
            if (ascentGravityMultiplier < 1.1f) ascentGravityMultiplier = 2.2f;
            if (fallGravityMultiplier < 1.1f) fallGravityMultiplier = 3.0f;

            if (lightPunch.hitHalfExtents.x > 0.45f || lightPunch.range > 0.70f)
            {
                lightPunch.range = 0.52f;
                lightPunch.hitHalfExtents = new Vector3(0.32f, 0.36f, 0.60f);
            }
            if (heavyPunch.hitHalfExtents.x > 0.50f || heavyPunch.range > 0.82f)
            {
                heavyPunch.range = 0.62f;
                heavyPunch.hitHalfExtents = new Vector3(0.37f, 0.38f, 0.60f);
            }
            if (kick.hitHalfExtents.x > 0.55f || kick.range > 0.95f)
            {
                kick.range = 0.72f;
                kick.hitHalfExtents = new Vector3(0.42f, 0.38f, 0.60f);
            }
        }

        void Start() => ResetFighter();

        public void ResetFighter()
        {
            StopActiveAttack();

            hp = maxHP;
            spCooldown = 0f;
            attackCooldownTimer = 0f;
            hitstunTimer = 0f;
            invulnTimer = 0f;
            hitFlashTimer = 0f;
            blockFlashTimer = 0f;
            jumpGraceTimer = 0f;
            dead = false;
            grounded = true;
            hasBeenAirborne = false;
            lastSpawnOrResetTime = Time.time;
            holdingAway = false;
            aiWantsBlock = false;
            aiWantsCrouch = false;
            devForceCrouch = false;
            devForceBlock = false;
            devWalkJustEnded = false;
            devWalkTimer = 0f;

            if (rb) rb.velocity = Vector3.zero;

            isSprintJump = false;
            StopSprint();
            SetWalking(false, false);
            SetCrouching(false);
            SetBlocking(false);
            RestoreCollider();
            ResetVisualPose();

            if (hpBar != null)
            {
                hpBar.minValue = 0f;
                hpBar.maxValue = 1f;
                hpBar.value = 1f;
            }
            ResetAnimator();
        }

        /// <summary>Scales maxHP from the fighter's original baseMaxHP without compounding across rounds.</summary>
        public void ApplyHPMultiplier(float multiplier)
        {
            if (baseMaxHP <= 0f) baseMaxHP = Mathf.Max(1f, maxHP);
            maxHP = Mathf.Max(1f, Mathf.Round(baseMaxHP * Mathf.Clamp(multiplier, 0.25f, 5f)));
            hp = maxHP;
            if (hpBar != null)
            {
                hpBar.minValue = 0f;
                hpBar.maxValue = 1f;
                hpBar.value = 1f;
            }
        }

        public void SetInputEnabled(bool v)
        {
            inputEnabled = v;
            if (!v)
            {
                holdingAway = false;
                aiWantsBlock = false;
                aiWantsCrouch = false;
                devForceCrouch = false;
                devForceBlock = false;
                devWalkJustEnded = false;
                devWalkTimer = 0f;
                isSprintJump = false;
                StopSprint();
                SetWalking(false, false);
                SetCrouching(false);
                SetBlocking(false);
            }
        }

        /// <summary>
        /// Enforces strict P1 (human) vs P2 (CPU AI) ownership regardless of prefab defaults.
        /// Strips any FighterAI components when playerControlled is true, or ensures one is
        /// attached and enabled when playerControlled is false.
        /// </summary>
        public FighterAI ConfigureControlOwnership(bool playerControlled, int slotIndex = -1)
        {
            if (slotIndex >= 0) playerIndex = slotIndex;
            isPlayerControlled = playerControlled;
            aiWantsBlock = false;
            aiWantsCrouch = false;
            holdingAway = false;
            SetWalking(false, false);
            SetCrouching(false);
            SetBlocking(false);

            if (playerControlled)
            {
                foreach (FighterAI existing in GetComponents<FighterAI>())
                {
                    if (!existing) continue;
                    existing.enabled = false;
                    existing.StopAllCoroutines();
                    Destroy(existing);
                }
                return null;
            }

            var ai = GetComponent<FighterAI>() ?? gameObject.AddComponent<FighterAI>();
            ai.enabled = true;
            return ai;
        }

        // ══════════════════════════════════════════════════════
        //  Facing
        // ══════════════════════════════════════════════════════
        /// <summary>Turn to face +X (dir &gt; 0) or -X (dir &lt; 0). dir == 0 is ignored.</summary>
        public void FaceDirection(int dir)
        {
            if (dir == 0) return;
            dir = dir > 0 ? 1 : -1;

            if (facingMode == FacingMode.FlipScaleX)
            {
                Vector3 s = transform.localScale;
                s.x = Mathf.Abs(s.x) * dir;
                transform.localScale = s;
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, dir * 90f, 0f);
            }
        }

        void FaceOpponent()
        {
            if (!opponent) return;
            float diff = opponent.position.x - transform.position.x;
            if (Mathf.Abs(diff) < 0.05f) return;
            FaceDirection(diff > 0f ? 1 : -1);
        }

        FighterController GetOpponentController()
        {
            if (!opponent) return null;
            if (!cachedOpponentCtrl || cachedOpponentCtrl.transform != opponent)
                cachedOpponentCtrl = opponent.GetComponent<FighterController>();
            return cachedOpponentCtrl;
        }

        // ══════════════════════════════════════════════════════
        //  AI Control API (called by FighterAI)
        // ══════════════════════════════════════════════════════
        public void AIMoveDir(float dir)
        {
            if (isPlayerControlled) return;
            if (!CanAct()) { SetWalking(false, false); return; }
            if (Mathf.Abs(dir) < 0.01f || blocking)
                SetWalking(false, false);
            else
                Move(Mathf.Sign(dir));
        }

        public void AIJump()
        {
            if (isPlayerControlled) return;
            if (CanAct() && grounded && !crouching && !blocking) DoJump();
        }

        public void AILightPunch() { if (!isPlayerControlled) TryStartMelee(AttackType.LightPunch); }
        public void AIHeavyPunch() { if (!isPlayerControlled) TryStartMelee(AttackType.HeavyPunch); }
        public void AIPunch() { if (!isPlayerControlled) TryStartMelee(AttackType.LightPunch); }
        public void AIKick() { if (!isPlayerControlled) TryStartMelee(AttackType.Kick); }
        public void AISpecial() { if (!isPlayerControlled) TryStartSpecial(); }

        public void AISetBlock(bool v)
        {
            if (isPlayerControlled) return;
            aiWantsBlock = v;
            if (dead || !inputEnabled) { SetBlocking(false); return; }
            if (v && !isAttacking && hitstunTimer <= 0f && grounded)
            {
                SetWalking(false, false);
                SetBlocking(true);
            }
            else if (!v)
            {
                SetBlocking(false);
            }
        }

        public void AISetCrouch(bool v)
        {
            if (isPlayerControlled) return;
            aiWantsCrouch = v;
            if (dead || !inputEnabled) { SetCrouching(false); return; }
            SetCrouching(v && grounded && !isAttacking && hitstunTimer <= 0f);
        }

        // ══════════════════════════════════════════════════════
        //  Update & Physics
        // ══════════════════════════════════════════════════════
        void Update()
        {
            if (Time.timeScale <= 0f || FightOverlayController.IsPaused) return;

            float dt = Time.deltaTime;
            if (spCooldown > 0f) spCooldown -= dt;
            if (attackCooldownTimer > 0f) attackCooldownTimer -= dt;
            if (invulnTimer > 0f) invulnTimer -= dt;
            if (hitFlashTimer > 0f) hitFlashTimer -= dt;
            if (blockFlashTimer > 0f) blockFlashTimer -= dt;
            if (jumpGraceTimer > 0f) jumpGraceTimer -= dt;

            if (devWalkTimer > 0f)
            {
                devWalkTimer -= dt;
                if (devWalkTimer <= 0f)
                {
                    devWalkTimer = 0f;
                    devWalkJustEnded = true;
                }
            }

            if (hitstunTimer > 0f)
            {
                hitstunTimer -= dt;
                if (hitstunTimer <= 0f) hitstunTimer = 0f;
            }

            if (dead)
            {
                AnimBool(HashIsGrounded, grounded);
                UpdatePlaceholderVisuals();
                return;
            }

            if (!inputEnabled)
            {
                SetWalking(false, false);
                SetBlocking(false);
                SetCrouching(false);
                AnimBool(HashIsGrounded, grounded);
                UpdatePlaceholderVisuals();
                return;
            }

            if (isPlayerControlled)
                HandlePlayerInput();
            else
                RefreshAIStates();

            if (!isAttacking && hitstunTimer <= 0f) FaceOpponent();
            ClampStagePosition();
            AnimBool(HashIsGrounded, grounded);
            UpdatePlaceholderVisuals();
        }

        void FixedUpdate()
        {
            if (jumpGraceTimer <= 0f)
                SetGroundedState((Time.time - lastGroundContactTime) < 0.08f || CheckFeetGrounded());
            AnimBool(HashIsGrounded, grounded);

            if (rb)
            {
                Vector3 v = rb.velocity;
                v.x = Mathf.MoveTowards(v.x, 0f, knockbackDamping * Time.fixedDeltaTime);
                v.z = 0f;
                rb.velocity = v;

                if (!grounded)
                {
                    float gravMul = v.y > 0.05f ? ascentGravityMultiplier : fallGravityMultiplier;
                    if (gravMul > 1f)
                        rb.AddForce(Physics.gravity * (gravMul - 1f), ForceMode.Acceleration);
                }
            }

            ClampStagePosition();
        }

        void RefreshAIStates()
        {
            bool stunned = hitstunTimer > 0f;
            if (stunned || !grounded)
            {
                SetCrouching(false);
                SetBlocking(false);
                SetWalking(false, false);
                devWalkJustEnded = false;
                return;
            }

            SetCrouching((aiWantsCrouch || devForceCrouch) && !isAttacking);
            SetBlocking((aiWantsBlock || devForceBlock) && !isAttacking);
            if (isAttacking || blocking)
            {
                SetWalking(false, false);
            }
            else if (devWalkTimer > 0f)
            {
                Move(devWalkRelDir * FacingDir);
            }
            else if (devWalkJustEnded)
            {
                SetWalking(false, false);
            }
            devWalkJustEnded = false;
        }

        void HandlePlayerInput()
        {
            if (!isPlayerControlled || FightOverlayController.IsAnyModalOpen) return;

            if (hitstunTimer > 0f)
            {
                holdingAway = false;
                devWalkJustEnded = false;
                StopSprint();
                SetWalking(false, false);
                SetCrouching(false);
                SetBlocking(false);
                return;
            }

            KeyCode kLeft = KeybindManager.GetKey(playerIndex, FighterAction.MoveLeft);
            KeyCode kRight = KeybindManager.GetKey(playerIndex, FighterAction.MoveRight);
            KeyCode kJump = KeybindManager.GetKey(playerIndex, FighterAction.Jump);
            KeyCode kCrouch = KeybindManager.GetKey(playerIndex, FighterAction.Crouch);
            KeyCode kBlock = KeybindManager.GetKey(playerIndex, FighterAction.Block);
            KeyCode kLightPunch = KeybindManager.GetKey(playerIndex, FighterAction.LightPunch);
            KeyCode kHeavyPunch = KeybindManager.GetKey(playerIndex, FighterAction.HeavyPunch);
            KeyCode kKick = KeybindManager.GetKey(playerIndex, FighterAction.Kick);
            KeyCode kSpecial = KeybindManager.GetKey(playerIndex, FighterAction.Special);

            if (Input.GetKeyDown(kCrouch) || Input.GetKeyDown(kJump))
                devForceCrouch = false;

            if (Input.GetKeyDown(kBlock))
                devForceBlock = false;

            // Read directional & posture inputs via KeybindManager (isolated per playerIndex)
            float dir = 0f;
            if (Input.GetKey(kLeft)) dir -= 1f;
            if (Input.GetKey(kRight)) dir += 1f;

            bool crouchHeld = (Input.GetKey(kCrouch) || devForceCrouch) && grounded;
            bool shiftBlock = (Input.GetKey(kBlock) || devForceBlock) && grounded;

            if (Input.GetKeyDown(kLeft))
            {
                if ((Time.time - lastLeftTapTime) <= doubleTapWindow && lastTappedDir == -1 && grounded && !crouchHeld && !shiftBlock)
                {
                    sprinting = true;
                    sprintInputDir = -1;
                }
                else if (sprinting && sprintInputDir != -1)
                {
                    StopSprint();
                }
                lastLeftTapTime = Time.time;
                lastTappedDir = -1;
            }
            if (Input.GetKeyDown(kRight))
            {
                if ((Time.time - lastRightTapTime) <= doubleTapWindow && lastTappedDir == 1 && grounded && !crouchHeld && !shiftBlock)
                {
                    sprinting = true;
                    sprintInputDir = 1;
                }
                else if (sprinting && sprintInputDir != 1)
                {
                    StopSprint();
                }
                lastRightTapTime = Time.time;
                lastTappedDir = 1;
            }

            if (sprinting)
            {
                bool heldMatchingDir = (sprintInputDir == -1 && Input.GetKey(kLeft) && dir < -0.5f)
                                    || (sprintInputDir == 1 && Input.GetKey(kRight) && dir > 0.5f);
                if (!heldMatchingDir || !grounded || crouchHeld || shiftBlock || isAttacking)
                    StopSprint();
            }

            if (!Mathf.Approximately(dir, 0f))
            {
                devWalkTimer = 0f;
                devWalkJustEnded = false;
            }
            else if (devWalkTimer > 0f)
            {
                dir = devWalkRelDir * FacingDir;
            }
            devWalkJustEnded = false;

            holdingAway = grounded && dir != 0f && (dir * FacingDir < 0f);

            if (!isAttacking) SetCrouching(crouchHeld);

            // Attacks take priority when ready (allow crisp sprint-to-attack cancel)
            if (!isAttacking && attackCooldownTimer <= 0f)
            {
                if (Input.GetKeyDown(kSpecial)) { if (TryStartSpecial()) return; }
                if (Input.GetKeyDown(kLightPunch)) { TryStartMelee(AttackType.LightPunch); return; }
                if (Input.GetKeyDown(kHeavyPunch)) { TryStartMelee(AttackType.HeavyPunch); return; }
                if (Input.GetKeyDown(kKick)) { TryStartMelee(AttackType.Kick); return; }
            }

            if (isAttacking)
            {
                StopSprint();
                SetWalking(false, false);
                SetBlocking(false);
                SetCrouching(crouchHeld);
                return;
            }

            bool autoAwayBlock = !sprinting && holdingAway && IsThreatApproaching();
            bool blockActive = shiftBlock || autoAwayBlock;
            if (blockActive) StopSprint();
            SetBlocking(blockActive);

            // Jump (allowed when standing and not actively holding block; allows sprint-to-jump cancel)
            bool jumpPressed = Input.GetKeyDown(kJump);
            if (jumpPressed && grounded && !crouching && !shiftBlock)
            {
                DoJump();
                return;
            }

            // Horizontal walk / sprint / crouch-walk (holding block holds position)
            if (dir != 0f && !shiftBlock && !autoAwayBlock)
                Move(dir);
            else
            {
                StopSprint();
                SetWalking(false, false);
            }
        }

        bool IsThreatApproaching()
        {
            FighterController opp = GetOpponentController();
            if (opp != null && opp.IsAttacking && Mathf.Abs(opp.transform.position.x - transform.position.x) <= 4.5f)
                return true;

            foreach (ProjectileDamage p in FindObjectsOfType<ProjectileDamage>())
            {
                if (!p || p.owner == gameObject) continue;
                float dx = p.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) <= 4.2f && dx * FacingDir > 0f) return true;
            }
            return false;
        }

        bool CanAct() => !dead && inputEnabled && !isAttacking && hitstunTimer <= 0f;

        void Move(float dir)
        {
            if (Mathf.Approximately(dir, 0f)) { StopSprint(); SetWalking(false, false); return; }

            float speed = moveSpeed;
            bool isForward = (dir * FacingDir) > 0f;
            bool activeSprint = sprinting && grounded && !crouching && !blocking;
            if (crouching) speed *= crouchSpeedMultiplier;
            else if (activeSprint) speed *= isForward ? sprintForwardMultiplier : sprintRetreatMultiplier;
            else if (!grounded && isSprintJump) speed *= sprintJumpAirMultiplier;
            else if (!isForward) speed *= backWalkSpeedMultiplier;

            transform.Translate(Vector3.right * dir * speed * Time.deltaTime, Space.World);
            ClampStagePosition();

            if (grounded)
            {
                if (activeSprint && animParams.ContainsKey(HashRunning))
                {
                    SetRunning(isForward, !isForward);
                    SetWalking(false, false);
                }
                else
                {
                    SetRunning(false, false);
                    SetWalking(isForward, !isForward);
                }
            }
            else
            {
                SetRunning(false, false);
                SetWalking(false, false);
            }
        }

        void ClampStagePosition()
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, stageMinX, stageMaxX);
            p.z = stageZ;
            transform.position = p;

            ResolveOpponentBodyOverlap();
        }

        /// <summary>
        /// Anti-phasing pushbox guard: when two living fighters vertically overlap (grounded or low jump),
        /// enforces minBodySpacing along X and clears inward horizontal velocities so high-speed sprinting cannot pass through.
        /// Jumping high above (vertical delta >= bodyOverlapHeightThreshold) still clears cleanly.
        /// </summary>
        void ResolveOpponentBodyOverlap()
        {
            FighterController opp = GetOpponentController();
            if (opp == null || dead || opp.IsDead || minBodySpacing <= 0f) return;

            Vector3 myPos = transform.position;
            Vector3 oppPos = opp.transform.position;

            if (Mathf.Abs(myPos.y - oppPos.y) >= bodyOverlapHeightThreshold) return;

            float dx = myPos.x - oppPos.x;
            float dist = Mathf.Abs(dx);
            if (dist >= minBodySpacing) return;

            float sign;
            if (dist > 0.001f)
                sign = dx >= 0f ? 1f : -1f;
            else
                sign = playerIndex == 0 ? -1f : 1f;

            float halfPush = (minBodySpacing - dist) * 0.5f;
            float myX = Mathf.Clamp(myPos.x + sign * halfPush, stageMinX, stageMaxX);
            float oppX = Mathf.Clamp(oppPos.x - sign * halfPush, opp.stageMinX, opp.stageMaxX);

            // If one fighter is pinned against a stage wall, shift the remaining separation onto the unpinned fighter
            float remaining = minBodySpacing - Mathf.Abs(myX - oppX);
            if (remaining > 0.0005f)
            {
                float shiftedMy = Mathf.Clamp(myX + sign * remaining, stageMinX, stageMaxX);
                remaining -= Mathf.Abs(shiftedMy - myX);
                myX = shiftedMy;

                if (remaining > 0.0005f)
                    oppX = Mathf.Clamp(oppX - sign * remaining, opp.stageMinX, opp.stageMaxX);
            }

            myPos.x = myX;
            oppPos.x = oppX;
            transform.position = myPos;
            opp.transform.position = oppPos;

            if (rb != null)
            {
                Vector3 v = rb.velocity;
                if (v.x * (-sign) > 0f)
                {
                    v.x = 0f;
                    rb.velocity = v;
                }
            }
            if (opp.rb != null)
            {
                Vector3 ov = opp.rb.velocity;
                if (ov.x * sign > 0f)
                {
                    ov.x = 0f;
                    opp.rb.velocity = ov;
                }
            }
        }

        void DoJump()
        {
            grounded = false;
            hasBeenAirborne = true;
            jumpGraceTimer = 0.12f;
            isSprintJump = sprinting;
            StopSprint();
            SetWalking(false, false);
            SetCrouching(false);
            SetBlocking(false);

            if (rb)
            {
                Vector3 v = rb.velocity;
                v.y = 0f;
                rb.velocity = v;
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
            AudioManager.PlayJump();
            AnimBool(HashIsGrounded, false);
            AnimTrigger(HashJump);
        }

        // ══════════════════════════════════════════════════════
        //  Attacks & Hit Detection
        // ══════════════════════════════════════════════════════
        bool TryStartMelee(AttackType type)
        {
            if (!CanAct() || attackCooldownTimer > 0f) return false;
            StopActiveAttack();
            attackRoutine = StartCoroutine(DoMeleeAttack(type));
            return true;
        }

        bool TryStartSpecial()
        {
            if (!CanAct() || attackCooldownTimer > 0f || !SpecialReady) return false;
            StopActiveAttack();
            attackRoutine = StartCoroutine(DoSpecialAttack());
            return true;
        }

        void StopActiveAttack()
        {
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            isAttacking = false;
            currentAttack = AttackType.None;
            attackPhase = AttackPhase.None;
            attackPhaseT = 0f;
        }

        AttackConfig GetAttackConfig(AttackType type)
        {
            switch (type)
            {
                case AttackType.HeavyPunch: return heavyPunch;
                case AttackType.Kick: return kick;
                default: return lightPunch;
            }
        }

        void TriggerAttackAnimation(AttackType type)
        {
            AnimResetTrigger(HashLightPunch);
            AnimResetTrigger(HashHeavyPunch);
            AnimResetTrigger(HashPunch);
            AnimResetTrigger(HashKick);
            AnimResetTrigger(HashSpecial);

            switch (type)
            {
                case AttackType.LightPunch:
                    if (!AnimTrigger(HashLightPunch)) AnimTrigger(HashPunch);
                    break;
                case AttackType.HeavyPunch:
                    if (!AnimTrigger(HashHeavyPunch)) AnimTrigger(HashPunch);
                    break;
                case AttackType.Kick:
                    AnimTrigger(HashKick);
                    break;
                case AttackType.Special:
                    AnimTrigger(HashSpecial);
                    break;
            }
        }

        float GetEffectiveMeleeStartup(AttackType type, AttackConfig spec)
        {
            if (!animReady) return spec.startup;
            switch (type)
            {
                case AttackType.LightPunch: return lightPunchStartup > 0f ? lightPunchStartup : spec.startup;
                case AttackType.HeavyPunch: return heavyPunchStartup > 0f ? heavyPunchStartup : spec.startup;
                case AttackType.Kick: return kickStartup > 0f ? kickStartup : spec.startup;
                default: return spec.startup;
            }
        }

        float GetEffectiveSpecialSpawnDelay()
        {
            if (!animReady) return specialStartup;
            return specialSpawnDelay > 0f ? specialSpawnDelay : specialStartup;
        }

        IEnumerator DoMeleeAttack(AttackType type)
        {
            AttackConfig spec = GetAttackConfig(type);
            isAttacking = true;
            currentAttack = type;
            StopSprint();
            SetWalking(false, false);
            SetBlocking(false);
            TriggerAttackAnimation(type);

            if (type == AttackType.LightPunch) AudioManager.PlayPunchLight();
            else AudioManager.PlayPunchHeavy();

            // 1) Startup (synced to impact frame when animated, fast fallback otherwise)
            attackPhase = AttackPhase.Startup;
            yield return RunPhaseTimer(GetEffectiveMeleeStartup(type, spec));

            // 2) Active
            attackPhase = AttackPhase.Active;
            var hitTargets = new HashSet<FighterController>();
            bool indicatorSpawned = false;

            float activeElapsed = 0f;
            float activeDur = Mathf.Max(0.02f, spec.active);
            while (activeElapsed < activeDur)
            {
                attackPhaseT = activeElapsed / activeDur;
                Vector3 hitCenter = GetMeleeHitboxCenter(type, spec);

                if (!indicatorSpawned && (forceShowMoveHitboxes || (usePlaceholderVisuals && !animReady)))
                {
                    indicatorSpawned = true;
                    SpawnHitIndicator(hitCenter, spec.hitHalfExtents, type, forceShowMoveHitboxes);
                }

                CheckMeleeOverlap(hitCenter, type, spec, hitTargets);
                activeElapsed += Time.deltaTime;
                yield return null;
            }

            // 3) Recovery
            attackPhase = AttackPhase.Recovery;
            yield return RunPhaseTimer(spec.recovery);

            attackCooldownTimer = spec.cooldown;
            isAttacking = false;
            currentAttack = AttackType.None;
            attackPhase = AttackPhase.None;
            attackRoutine = null;
        }

        IEnumerator DoSpecialAttack()
        {
            isAttacking = true;
            currentAttack = AttackType.Special;
            spCooldown = specialCooldown;
            StopSprint();
            SetWalking(false, false);
            SetBlocking(false);
            TriggerAttackAnimation(AttackType.Special);

            // 1) Startup (synced to actual throw release frame when animated)
            attackPhase = AttackPhase.Startup;
            yield return RunPhaseTimer(GetEffectiveSpecialSpawnDelay());

            // 2) Active — fire projectile
            attackPhase = AttackPhase.Active;
            SpawnSpecialProjectile();
            yield return RunPhaseTimer(specialActive);

            // 3) Recovery
            attackPhase = AttackPhase.Recovery;
            yield return RunPhaseTimer(specialRecovery);

            attackCooldownTimer = 0.15f;
            isAttacking = false;
            currentAttack = AttackType.None;
            attackPhase = AttackPhase.None;
            attackRoutine = null;
        }

        IEnumerator RunPhaseTimer(float duration)
        {
            if (duration <= 0f) { attackPhaseT = 1f; yield break; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                attackPhaseT = elapsed / duration;
                elapsed += Time.deltaTime;
                yield return null;
            }
            attackPhaseT = 1f;
        }

        Vector3 GetMeleeHitboxCenter(AttackType type, AttackConfig spec)
        {
            float feetY = GetFeetWorldY();
            float standH = GetStandingWorldHeight();
            float heightRatio;

            if (crouching)
                heightRatio = 0.34f;
            else if (type == AttackType.Kick)
                heightRatio = 0.48f;
            else
                heightRatio = 0.65f;

            float hitY = feetY + standH * heightRatio;
            return new Vector3(transform.position.x, hitY, transform.position.z) + FacingVector * spec.range;
        }

        void CheckMeleeOverlap(Vector3 hitCenter, AttackType type, AttackConfig spec, HashSet<FighterController> hitTargets)
        {
            Physics.SyncTransforms();
            Collider[] hits = Physics.OverlapBox(hitCenter, spec.hitHalfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider h in hits)
            {
                if (!h || h.transform.root == transform.root) continue;
                FighterController target = h.GetComponentInParent<FighterController>();
                if (!target || target == this || hitTargets.Contains(target)) continue;

                hitTargets.Add(target);
                target.TakeDamage(spec.damage, FacingVector, spec.knockback, spec.hitstun,
                    isHeavyHit: type != AttackType.LightPunch, isSpecialProjectile: false);
            }
        }

        void SpawnSpecialProjectile()
        {
            Transform origin = EnsureFirePoint();
            Vector3 facing = FacingVector;
            Vector3 spawnPos = origin.position + facing * muzzleForwardOffset;
            if (crouching) spawnPos.y -= GetStandingWorldHeight() * 0.22f;

            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            GameObject proj = specialPrefab
                ? Instantiate(specialPrefab, spawnPos, rot)
                : CreateFallbackProjectile(spawnPos, rot);

            Rigidbody projRb = proj.GetComponent<Rigidbody>();
            if (projRb) projRb.velocity = facing * specialSpeed;

            ProjectileDamage pd = proj.GetComponent<ProjectileDamage>();
            if (pd)
            {
                pd.damage = specialDamage;
                pd.owner = gameObject;
                pd.SetTravelDirection(facing);
            }

            AudioManager.PlaySpecialLaunch();
            Destroy(proj, 3f);
        }

        GameObject CreateFallbackProjectile(Vector3 pos, Quaternion rot)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FallbackSpecialProjectile";
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * 0.42f;

            var sphereCol = go.GetComponent<SphereCollider>();
            if (sphereCol) sphereCol.isTrigger = true;

            var projRb = go.AddComponent<Rigidbody>();
            projRb.useGravity = false;
            projRb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

            var pd = go.AddComponent<ProjectileDamage>();
            pd.damage = specialDamage;
            pd.spinSelf = false;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr)
            {
                if (!sharedFallbackProjMat)
                {
                    Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                    sharedFallbackProjMat = new Material(s) { color = new Color(1f, 0.85f, 0.25f, 0.92f) };
                }
                mr.sharedMaterial = sharedFallbackProjMat;
            }
            return go;
        }

        /// <summary>Returns firePoint, creating a "FirePoint" child at standing chest height if none is assigned.</summary>
        Transform EnsureFirePoint()
        {
            if (firePoint) return firePoint;

            Transform existing = transform.Find("FirePoint");
            if (existing) { firePoint = existing; return firePoint; }

            var go = new GameObject("FirePoint");
            firePoint = go.transform;
            firePoint.SetParent(transform, false);
            firePoint.localPosition = new Vector3(0f, StandingChestLocalHeight(), 0f);
            firePoint.localRotation = Quaternion.identity;
            return firePoint;
        }

        float StandingChestLocalHeight()
        {
            if (capsule) return bottomLocalY + origColliderHeight * chestHeightRatio;
            return fallbackChestHeight / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        }

        float GetFeetWorldY()
        {
            if (capsule) return transform.TransformPoint(0f, bottomLocalY, 0f).y;
            return transform.position.y;
        }

        float GetStandingWorldHeight()
        {
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            return capsule ? origColliderHeight * scaleY : fallbackChestHeight * 1.4f;
        }

        // ══════════════════════════════════════════════════════
        //  Damage, Blocking, Hitstun & Knockback
        // ══════════════════════════════════════════════════════
        /// <summary>Compatibility overload (used by ProjectileDamage).</summary>
        public void TakeDamage(float amount)
        {
            Vector3 kbDir = opponent
                ? (transform.position.x >= opponent.position.x ? Vector3.right : Vector3.left)
                : -FacingVector;
            TakeDamage(amount, kbDir, 4.0f, 0.24f, isHeavyHit: true, isSpecialProjectile: true);
        }

        public void TakeDamage(float amount, Vector3 knockbackDir, float knockbackForce, float hitstunDuration,
            bool isHeavyHit = false, bool isSpecialProjectile = false)
        {
            if (dead || invulnTimer > 0f) return;

            bool blockedHit = blocking || (grounded && !isAttacking && hitstunTimer <= 0f && holdingAway);
            float finalDmg = blockedHit ? amount * blockReduction : amount;

            if (isSpecialProjectile)
            {
                AudioManager.PlaySpecialImpact();
                if (blockedHit) AudioManager.PlayBlock();
            }
            else if (blockedHit)
            {
                AudioManager.PlayBlock();
            }
            else if (isHeavyHit)
            {
                AudioManager.PlayHitHeavy();
            }
            else
            {
                AudioManager.PlayHitLight();
            }

            hp = infiniteHP ? maxHP : Mathf.Max(0f, hp - finalDmg);
            if (hpBar != null) hpBar.value = HPRatio;

            float dirX = Mathf.Abs(knockbackDir.x) > 0.01f ? Mathf.Sign(knockbackDir.x) : -FacingDir;
            float appliedKB = blockedHit ? knockbackForce * blockKnockbackScale : knockbackForce;
            if (rb && appliedKB > 0f)
            {
                Vector3 v = rb.velocity;
                v.x = dirX * appliedKB;
                rb.velocity = v;
            }

            if (hp <= 0f && !infiniteHP)
            {
                dead = true;
                StopActiveAttack();
                hitstunTimer = 0f;
                isSprintJump = false;
                StopSprint();
                SetWalking(false, false);
                SetCrouching(false);
                SetBlocking(false);
                RestoreCollider();
                AnimTrigger(HashDeath);
                OnKO?.Invoke();
                return;
            }

            if (blockedHit)
            {
                blockFlashTimer = 0.16f;
                invulnTimer = 0.08f;
            }
            else
            {
                StopActiveAttack();
                isSprintJump = false;
                StopSprint();
                SetWalking(false, false);
                SetBlocking(false);
                hitstunTimer = Mathf.Max(hitstunTimer, hitstunDuration);
                invulnTimer = invulnerabilityDuration;
                hitFlashTimer = Mathf.Max(0.18f, hitstunDuration);
                AnimTrigger(HashHit);
            }
        }

        // ══════════════════════════════════════════════════════
        //  Collider & Grounding
        // ══════════════════════════════════════════════════════
        void ApplyColliderHeight(bool crouchState)
        {
            if (!capsule) return;
            if (crouchState)
            {
                float targetH = Mathf.Max(capsule.radius * 2f, origColliderHeight * crouchHeightRatio);
                capsule.height = targetH;
                capsule.center = new Vector3(origColliderCenter.x, bottomLocalY + targetH * 0.5f, origColliderCenter.z);
            }
            else
            {
                RestoreCollider();
            }
        }

        void RestoreCollider()
        {
            if (!capsule) return;
            capsule.height = origColliderHeight;
            capsule.center = origColliderCenter;
        }

        bool CheckFeetGrounded()
        {
            if (rb && rb.velocity.y > 0.25f) return false;
            Vector3 feetPos = new Vector3(transform.position.x, GetFeetWorldY() + 0.14f, transform.position.z);
            Collider[] hits = Physics.OverlapSphere(feetPos, 0.22f, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in hits)
                if (c && c.transform.root != transform.root) return true;
            return false;
        }

        void SetGroundedState(bool newGrounded)
        {
            if (!newGrounded && (Time.time - lastSpawnOrResetTime > 0.25f))
                hasBeenAirborne = true;

            if (newGrounded)
                isSprintJump = false;

            if (newGrounded && !grounded && hasBeenAirborne)
            {
                hasBeenAirborne = false;
                if (Time.time - lastLandSfxTime > 0.12f)
                {
                    lastLandSfxTime = Time.time;
                    AudioManager.PlayLand();
                }
            }
            grounded = newGrounded;
        }

        void RecordGroundContact(Collision col)
        {
            if (col.transform.root == transform.root) return;
            for (int i = 0; i < col.contactCount; i++)
            {
                if (col.GetContact(i).normal.y > 0.5f)
                {
                    lastGroundContactTime = Time.time;
                    if (jumpGraceTimer <= 0f) SetGroundedState(true);
                    return;
                }
            }
        }

        void OnCollisionEnter(Collision col) => RecordGroundContact(col);
        void OnCollisionStay(Collision col) => RecordGroundContact(col);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.75f);
            Vector3 facing = Application.isPlaying ? FacingVector : transform.forward;
            float standH = capsule ? capsule.height : fallbackChestHeight * 1.4f;
            float feetY = capsule ? transform.TransformPoint(0f, capsule.center.y - capsule.height * 0.5f, 0f).y : transform.position.y;
            Vector3 center = new Vector3(transform.position.x, feetY + standH * 0.65f, transform.position.z) + facing * lightPunch.range;
            Gizmos.DrawWireCube(center, lightPunch.hitHalfExtents * 2f);
        }

        // ══════════════════════════════════════════════════════
        //  Toggleable Placeholder Visual Feedback
        // ══════════════════════════════════════════════════════
        void CacheVisuals()
        {
            propBlock = new MaterialPropertyBlock();

            if (!visualRoot)
            {
                Transform meshChild = transform.Find("Mesh_0");
                if (meshChild) visualRoot = meshChild;
                else
                {
                    foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.transform != transform) { visualRoot = r.transform; break; }
                    }
                }
            }

            hasVisualRoot = visualRoot != null && visualRoot != transform;
            if (hasVisualRoot)
            {
                baseVisualLocalPos = visualRoot.localPosition;
                baseVisualLocalRot = visualRoot.localRotation;
                baseVisualLocalScale = visualRoot.localScale;

                Vector3 childUp = Quaternion.Inverse(baseVisualLocalRot) * Vector3.up;
                visualUpWeights = new Vector3(Mathf.Abs(childUp.x), Mathf.Abs(childUp.y), Mathf.Abs(childUp.z));
            }

            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            baseRendererColors = new Color[cachedRenderers.Length];
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Material m = cachedRenderers[i] ? cachedRenderers[i].sharedMaterial : null;
                if (m && m.HasProperty(ShaderColorId)) baseRendererColors[i] = m.GetColor(ShaderColorId);
                else if (m && m.HasProperty(ShaderBaseColorId)) baseRendererColors[i] = m.GetColor(ShaderBaseColorId);
                else baseRendererColors[i] = Color.white;
            }
        }

        void UpdatePlaceholderVisuals()
        {
            if (!usePlaceholderVisuals)
            {
                if (placeholderWasActive) ResetVisualPose();
                return;
            }

            // 1) Pose transform (crouch squash, attack tilt/lunge, hitstun/KO lean)
            //    Automatically skipped when a real AnimatorController drives the rig.
            if (hasVisualRoot && !animReady)
            {
                placeholderWasActive = true;

                float targetSquash = crouching ? 0.68f : 1f;
                float targetTilt = 0f;
                float targetLunge = 0f;

                if (dead)
                {
                    targetSquash = 0.85f;
                    targetTilt = -78f;
                    targetLunge = -0.45f;
                }
                else if (hitstunTimer > 0f)
                {
                    targetTilt = -18f;
                    targetLunge = -0.14f;
                }
                else if (isAttacking)
                {
                    ComputeAttackPose(out targetTilt, out targetLunge);
                }
                else if (blocking)
                {
                    targetTilt = -8f;
                    targetLunge = -0.06f;
                }

                float blend = 1f - Mathf.Exp(-20f * Time.deltaTime);
                currentVisualSquash = Mathf.Lerp(currentVisualSquash, targetSquash, blend);
                currentVisualTilt = Mathf.Lerp(currentVisualTilt, targetTilt, blend);
                currentVisualLunge = Mathf.Lerp(currentVisualLunge, targetLunge, blend);

                Vector3 scaleFactor = Vector3.one + visualUpWeights * (currentVisualSquash - 1f);
                visualRoot.localScale = Vector3.Scale(baseVisualLocalScale, scaleFactor);

                Vector3 localFwd = transform.InverseTransformVector(FacingVector).normalized;
                Vector3 tiltAxis = Vector3.Cross(Vector3.up, localFwd);
                if (tiltAxis.sqrMagnitude < 0.001f) tiltAxis = Vector3.right;

                visualRoot.localRotation = Quaternion.AngleAxis(currentVisualTilt, tiltAxis.normalized) * baseVisualLocalRot;

                float footPivotDrop = (1f - currentVisualSquash) * Mathf.Max(0f, baseVisualLocalPos.y - bottomLocalY);
                if (dead) footPivotDrop += 0.45f;
                visualRoot.localPosition = baseVisualLocalPos + localFwd * currentVisualLunge - Vector3.up * footPivotDrop;
            }
            else if (placeholderWasActive && animReady)
            {
                placeholderWasActive = false;
                if (hasVisualRoot)
                {
                    visualRoot.localPosition = baseVisualLocalPos;
                    visualRoot.localRotation = baseVisualLocalRot;
                    visualRoot.localScale = baseVisualLocalScale;
                }
            }

            // 2) Tint / flash for hit and block (stays active with or without AnimatorController)
            if (hitFlashTimer > 0f)
            {
                float pulse = Mathf.PingPong(Time.time * 24f, 1f);
                Color hitTint = Color.Lerp(new Color(1f, 0.22f, 0.22f, 1f), Color.white, pulse * 0.45f);
                ApplyRendererTint(hitTint, 0.85f);
            }
            else if (blocking || blockFlashTimer > 0f)
            {
                float strength = blockFlashTimer > 0f ? 0.80f : 0.48f;
                ApplyRendererTint(new Color(0.32f, 0.72f, 1f, 1f), strength);
            }
            else
            {
                ClearRendererTint();
            }
        }

        void ComputeAttackPose(out float tiltDeg, out float lungeMeters)
        {
            float maxTilt = currentAttack == AttackType.HeavyPunch ? 24f : (currentAttack == AttackType.Kick ? 22f : 16f);
            float maxLunge = currentAttack == AttackType.HeavyPunch ? 0.36f : (currentAttack == AttackType.Kick ? 0.40f : 0.24f);
            if (currentAttack == AttackType.Special) { maxTilt = 14f; maxLunge = 0.20f; }

            switch (attackPhase)
            {
                case AttackPhase.Startup:
                    tiltDeg = Mathf.Lerp(0f, -8f, attackPhaseT);
                    lungeMeters = Mathf.Lerp(0f, -0.06f, attackPhaseT);
                    break;
                case AttackPhase.Active:
                    tiltDeg = maxTilt;
                    lungeMeters = maxLunge;
                    break;
                case AttackPhase.Recovery:
                    tiltDeg = Mathf.Lerp(maxTilt, 0f, attackPhaseT);
                    lungeMeters = Mathf.Lerp(maxLunge, 0f, attackPhaseT);
                    break;
                default:
                    tiltDeg = 0f; lungeMeters = 0f;
                    break;
            }
        }

        void ApplyRendererTint(Color tint, float strength)
        {
            if (cachedRenderers == null) return;
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer r = cachedRenderers[i];
                if (!r) continue;
                Color c = Color.Lerp(baseRendererColors[i], tint, strength);
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor(ShaderColorId, c);
                propBlock.SetColor(ShaderBaseColorId, c);
                r.SetPropertyBlock(propBlock);
            }
        }

        void ClearRendererTint()
        {
            if (cachedRenderers == null) return;
            for (int i = 0; i < cachedRenderers.Length; i++)
                if (cachedRenderers[i]) cachedRenderers[i].SetPropertyBlock(null);
        }

        void ResetVisualPose()
        {
            currentVisualSquash = 1f;
            currentVisualTilt = 0f;
            currentVisualLunge = 0f;
            placeholderWasActive = false;

            if (hasVisualRoot)
            {
                visualRoot.localPosition = baseVisualLocalPos;
                visualRoot.localRotation = baseVisualLocalRot;
                visualRoot.localScale = baseVisualLocalScale;
            }
            ClearRendererTint();
        }

        void SpawnHitIndicator(Vector3 center, Vector3 halfExtents, AttackType type, bool devMoveHitbox)
        {
            GameObject indicator = GameObject.CreatePrimitive(devMoveHitbox ? PrimitiveType.Cube : PrimitiveType.Sphere);
            indicator.name = (devMoveHitbox ? "DevHitbox_" : "HitIndicator_") + type;
            Collider c = indicator.GetComponent<Collider>();
            if (c)
            {
                c.enabled = false;
                Destroy(c);
            }

            indicator.transform.position = center;
            indicator.transform.rotation = Quaternion.identity;
            indicator.transform.localScale = devMoveHitbox
                ? halfExtents * 2f
                : new Vector3(halfExtents.x * 2f, halfExtents.y * 2f, halfExtents.x * 1.4f);

            var mr = indicator.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                if (!sharedHitIndicatorMat)
                {
                    Shader s = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Unlit/Transparent")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Standard");
                    sharedHitIndicatorMat = new Material(s);
                }
                mr.sharedMaterial = sharedHitIndicatorMat;

                Color col = devMoveHitbox
                    ? (type == AttackType.HeavyPunch
                        ? new Color(1f, 0.14f, 0.08f, 0.46f)
                        : new Color(1f, 0.36f, 0.06f, 0.44f))
                    : (type == AttackType.HeavyPunch
                        ? new Color(1f, 0.35f, 0.15f, 0.42f)
                        : (type == AttackType.Kick
                            ? new Color(0.35f, 0.85f, 1f, 0.40f)
                            : new Color(1f, 0.82f, 0.2f, 0.38f)));

                var pb = new MaterialPropertyBlock();
                pb.SetColor(ShaderColorId, col);
                pb.SetColor(ShaderBaseColorId, col);
                mr.SetPropertyBlock(pb);
            }

            Destroy(indicator, devMoveHitbox ? 0.25f : 0.14f);
        }

        // ══════════════════════════════════════════════════════
        //  Animator hooks (all guarded: zero warnings when unassigned)
        // ══════════════════════════════════════════════════════
        void CacheAnimator()
        {
            animParams.Clear();
            Animator[] allAnims = GetComponentsInChildren<Animator>(true);
            foreach (Animator a in allAnims)
                if (a) a.applyRootMotion = false;

            if (anim == null || anim.runtimeAnimatorController == null)
            {
                foreach (Animator a in allAnims)
                {
                    if (a && a.runtimeAnimatorController != null)
                    {
                        anim = a;
                        break;
                    }
                }
            }

            animReady = anim != null && anim.runtimeAnimatorController != null;
            if (!animReady) return;
            foreach (AnimatorControllerParameter p in anim.parameters)
                animParams[p.nameHash] = p.type;
        }

        bool AnimTrigger(int hash)
        {
            if (!animReady) return false;
            if (animParams.TryGetValue(hash, out var t) && t == AnimatorControllerParameterType.Trigger)
            {
                anim.SetTrigger(hash);
                return true;
            }
            return false;
        }

        void AnimResetTrigger(int hash)
        {
            if (!animReady) return;
            if (animParams.TryGetValue(hash, out var t) && t == AnimatorControllerParameterType.Trigger)
                anim.ResetTrigger(hash);
        }

        void AnimBool(int hash, bool value)
        {
            if (!animReady) return;
            if (animParams.TryGetValue(hash, out var t) && t == AnimatorControllerParameterType.Bool)
                anim.SetBool(hash, value);
        }

        void SetWalking(bool forward, bool backward)
        {
            if (walkingForward != forward)
            {
                walkingForward = forward;
                AnimBool(HashWalking, forward);
            }
            if (walkingBackward != backward)
            {
                walkingBackward = backward;
                AnimBool(HashWalkingBack, backward);
            }
        }

        void SetRunning(bool forward, bool backward)
        {
            if (runningForward != forward)
            {
                runningForward = forward;
                AnimBool(HashRunning, forward);
            }
            if (runningBackward != backward)
            {
                runningBackward = backward;
                AnimBool(HashRunningBack, backward);
            }
        }

        void StopSprint()
        {
            sprinting = false;
            sprintInputDir = 0;
            SetRunning(false, false);
        }

        void SetCrouching(bool v)
        {
            if (crouching == v) return;
            crouching = v;
            ApplyColliderHeight(v);
            AnimBool(HashCrouching, v);
        }

        void SetBlocking(bool v)
        {
            if (blocking == v) return;
            blocking = v;
            AnimBool(HashBlocking, v);
        }

        /// <summary>Clears pending triggers and returns the Animator to Idle on round reset.</summary>
        void ResetAnimator()
        {
            if (!animReady) return;
            AnimBool(HashWalking, false);
            AnimBool(HashWalkingBack, false);
            AnimBool(HashRunning, false);
            AnimBool(HashRunningBack, false);
            AnimBool(HashCrouching, false);
            AnimBool(HashBlocking, false);
            AnimBool(HashIsGrounded, grounded);
            foreach (var kv in animParams)
                if (kv.Value == AnimatorControllerParameterType.Trigger) anim.ResetTrigger(kv.Key);

            if (anim.HasState(0, HashIdle)) anim.Play(HashIdle, 0, 0f);
            else AnimTrigger(HashIdle);
        }

        // ══════════════════════════════════════════════════════
        //  Dev / Training Mode Helpers (Fighter & Puppet API)
        // ══════════════════════════════════════════════════════
        /// <summary>Restores fighter HP to maxHP and syncs HUD bar.</summary>
        public void FullHeal()
        {
            hp = maxHP;
            if (hpBar) hpBar.value = 1f;
        }

        /// <summary>Clears held dev/AI posture, walk timers, and walking state.</summary>
        public void DevClearOverrides()
        {
            devForceCrouch = false;
            devForceBlock = false;
            devWalkTimer = 0f;
            devWalkJustEnded = false;
            aiWantsCrouch = false;
            aiWantsBlock = false;
            holdingAway = false;
            SetWalking(false, false);
            SetCrouching(false);
            SetBlocking(false);
        }

        /// <summary>Revives fighter (if dead) and returns Animator / placeholder visuals cleanly to Idle.</summary>
        public void DevReviveAndResetIdle()
        {
            StopActiveAttack();
            dead = false;
            inputEnabled = true;
            hitstunTimer = 0f;
            invulnTimer = 0f;
            hitFlashTimer = 0f;
            blockFlashTimer = 0f;
            attackCooldownTimer = 0f;
            spCooldown = 0f;

            DevClearOverrides();
            RestoreCollider();
            FullHeal();

            if (rb) rb.velocity = Vector3.zero;
            ResetVisualPose();

            if (animReady && anim)
            {
                anim.Rebind();
                anim.applyRootMotion = false;
                anim.Update(0f);
                ResetAnimator();
            }
        }

        /// <summary>Triggers KO / Death animation cleanly; set triggerKOEvent=false for animation-only preview.</summary>
        public void DevForceKill(bool triggerKOEvent = true)
        {
            StopActiveAttack();
            DevClearOverrides();

            dead = true;
            hitstunTimer = 0f;
            invulnTimer = 0f;
            hitFlashTimer = 0f;
            blockFlashTimer = 0f;

            if (triggerKOEvent)
            {
                hp = 0f;
                if (hpBar) hpBar.value = 0f;
                SetInputEnabled(false);
            }

            if (rb)
            {
                Vector3 v = rb.velocity;
                v.x = 0f;
                rb.velocity = v;
            }

            RestoreCollider();
            AnimTrigger(HashDeath);
            UpdatePlaceholderVisuals();

            if (triggerKOEvent) OnKO?.Invoke();
        }

        /// <summary>Plays hitstun, hit-flash, knockback, and Hit animation without needing an attacker.</summary>
        public void DevTriggerHit(float damage = 0f)
        {
            if (dead) DevReviveAndResetIdle();

            StopActiveAttack();
            DevClearOverrides();

            if (!infiniteHP && damage > 0f)
            {
                hp = Mathf.Max(0f, hp - damage);
                if (hpBar != null) hpBar.value = HPRatio;
            }

            if (rb)
            {
                Vector3 v = rb.velocity;
                v.x = -FacingDir * 2.8f;
                rb.velocity = v;
            }

            hitstunTimer = 0.28f;
            hitFlashTimer = 0.28f;
            AnimTrigger(HashHit);
            UpdatePlaceholderVisuals();
        }

        /// <summary>Fires a combat move immediately on this fighter regardless of cooldown or prior attack.</summary>
        public void DevTriggerAttack(AttackType type)
        {
            if (type == AttackType.None) return;
            if (dead) DevReviveAndResetIdle();
            if (!inputEnabled) inputEnabled = true;

            StopActiveAttack();
            devForceBlock = false;
            aiWantsBlock = false;
            SetBlocking(false);
            hitstunTimer = 0f;
            attackCooldownTimer = 0f;

            attackRoutine = type == AttackType.Special
                ? StartCoroutine(DoSpecialAttack())
                : StartCoroutine(DoMeleeAttack(type));
        }

        /// <summary>Triggers a jump immediately on this fighter.</summary>
        public void DevTriggerJump()
        {
            if (dead) DevReviveAndResetIdle();
            if (!inputEnabled) inputEnabled = true;

            StopActiveAttack();
            devForceCrouch = false;
            devForceBlock = false;
            aiWantsCrouch = false;
            aiWantsBlock = false;
            hitstunTimer = 0f;
            DoJump();
        }

        public void DevSetCrouch(bool active)
        {
            if (active && dead) DevReviveAndResetIdle();
            devForceCrouch = active;
            if (active) { devForceBlock = false; SetBlocking(false); }
            SetCrouching(active && !isAttacking);
        }

        public void DevToggleCrouch() => DevSetCrouch(!devForceCrouch);

        public void DevSetBlock(bool active)
        {
            if (active && dead) DevReviveAndResetIdle();
            devForceBlock = active;
            if (active)
            {
                devWalkTimer = 0f;
                devWalkJustEnded = false;
                SetWalking(false, false);
            }
            SetBlocking(active && !isAttacking);
        }

        public void DevToggleBlock() => DevSetBlock(!devForceBlock);

        /// <summary>Walks forward (+1) or backward (-1) relative to FacingDir for duration seconds.</summary>
        public void DevWalkTimed(int relativeDir, float duration = 1.0f)
        {
            if (dead) DevReviveAndResetIdle();
            if (!inputEnabled) inputEnabled = true;

            StopActiveAttack();
            devForceBlock = false;
            aiWantsBlock = false;
            SetBlocking(false);
            devWalkRelDir = relativeDir >= 0 ? 1 : -1;
            devWalkTimer = Mathf.Max(0.05f, duration);
            devWalkJustEnded = false;
        }
    }
}
