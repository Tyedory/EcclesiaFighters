using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    /// <summary>
    /// Handles movement, attacks, blocking and HP for ONE fighter.
    /// Attach to each character prefab.
    /// isPlayer = true  → reads keyboard input (Player 1)
    /// isPlayer = false → FighterAI drives this controller
    ///
    /// ── Animator contract (Mixamo-ready) ──────────────────────────────────
    /// Assign an AnimatorController whose parameters / states use these names.
    /// Every call is guarded, so missing parameters or a missing controller are
    /// silently ignored instead of spamming the console.
    ///   State    "Idle"                     (default state; also Played on ResetFighter)
    ///   Bool     "Walking"                  true while moving horizontally
    ///   Trigger  "Jump"                     DoJump()
    ///   Trigger  "Punch"                    DoAttack("Punch")
    ///   Trigger  "Kick"                     DoAttack("Kick")
    ///   Trigger  "Special"                  DoSpecial()
    ///   Trigger  "Hit"                      TakeDamage() while not blocking
    ///   Trigger  "Death"                    TakeDamage() when HP reaches 0
    ///   Bool     "Blocking"  (optional)     true while guarding
    ///   Trigger  "Idle"      (optional)     only used if the controller has no "Idle" state
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
            /// <summary>Mirror transform.localScale.x. Only correct for flat sprites that are not rotated.</summary>
            FlipScaleX
        }

        // ── Identity ──────────────────────────────────────────
        public string characterName = "Fighter";
        public bool   isPlayer = true;

        // ── Stats ─────────────────────────────────────────────
        public float maxHP          = 100f;
        public float moveSpeed      = 4f;
        public float jumpForce      = 7.5f;
        public float punchDamage    = 10f;
        public float kickDamage     = 14f;
        public float specialDamage  = 38f;
        public float specialCooldown= 5f;
        public float blockReduction = 0.15f;   // blocked hit = 15% damage

        // ── Facing ────────────────────────────────────────────
        [Header("Facing")]
        public FacingMode facingMode = FacingMode.RotateY;

        // ── Special projectile ────────────────────────────────
        [Header("Special")]
        public GameObject specialPrefab;
        [Tooltip("Spawn origin of the special. If empty, a 'FirePoint' child is created at chest height on first use.")]
        public Transform  firePoint;
        [Tooltip("Chest height as a fraction of the collider height (0 = feet, 1 = head).")]
        [Range(0f, 1f)] public float chestHeightRatio = 0.7f;
        [Tooltip("Chest height in metres, used only when the fighter has no Collider.")]
        public float fallbackChestHeight = 1.3f;
        [Tooltip("How far in front of the firePoint (along the facing direction) the projectile spawns.")]
        public float muzzleForwardOffset = 0.5f;

        // ── Controls (Player 1 defaults) ──────────────────────
        [Header("Controls — ignored when isPlayer=false")]
        public KeyCode kLeft    = KeyCode.A;
        public KeyCode kRight   = KeyCode.D;
        public KeyCode kJump    = KeyCode.W;
        public KeyCode kBlock   = KeyCode.S;
        public KeyCode kPunch   = KeyCode.J;
        public KeyCode kKick    = KeyCode.K;
        public KeyCode kSpecial = KeyCode.L;

        // ── References ────────────────────────────────────────
        [HideInInspector] public Transform  opponent;
        [HideInInspector] public Slider     hpBar;
        [HideInInspector] public Action     OnKO;

        // ── Public state (read by AI) ─────────────────────────
        public float CurrentHP      => hp;
        public float HPRatio        => hp / maxHP;
        public bool  IsGrounded     => grounded;
        public bool  IsBlocking     => blocking;
        public bool  SpecialReady   => spCooldown <= 0;
        public bool  IsDead         => dead;

        /// <summary>+1 = facing right (+X), -1 = facing left (-X). Derived from the transform, so it can never desync.</summary>
        public int FacingDir => facingMode == FacingMode.FlipScaleX
            ? (transform.localScale.x >= 0f ? 1 : -1)
            : (transform.forward.x   >= 0f ? 1 : -1);

        /// <summary>World-space unit vector the fighter is facing (always along X).</summary>
        public Vector3 FacingVector => new Vector3(FacingDir, 0f, 0f);

        // ── Animator hashes ───────────────────────────────────
        static readonly int HashIdle     = Animator.StringToHash("Idle");
        static readonly int HashWalking  = Animator.StringToHash("Walking");
        static readonly int HashJump     = Animator.StringToHash("Jump");
        static readonly int HashPunch    = Animator.StringToHash("Punch");
        static readonly int HashKick     = Animator.StringToHash("Kick");
        static readonly int HashSpecial  = Animator.StringToHash("Special");
        static readonly int HashHit      = Animator.StringToHash("Hit");
        static readonly int HashDeath    = Animator.StringToHash("Death");
        static readonly int HashBlocking = Animator.StringToHash("Blocking");

        // ── Private ───────────────────────────────────────────
        float  hp, spCooldown;
        bool   grounded = true, blocking, dead, inputEnabled, walking;
        Rigidbody rb;
        Animator  anim;
        bool      isAttacking;

        // name-hash → type of every parameter that actually exists in the controller
        readonly Dictionary<int, AnimatorControllerParameterType> animParams =
            new Dictionary<int, AnimatorControllerParameterType>();
        bool animReady;

        void Awake()
        {
            rb   = GetComponent<Rigidbody>();
            anim = GetComponent<Animator>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            CacheAnimator();
        }

        void Start()
        {
            ResetFighter();
            // Player 2 control scheme
            if (!isPlayer)
            {
                kLeft = KeyCode.LeftArrow; kRight = KeyCode.RightArrow;
                kJump = KeyCode.UpArrow;   kBlock = KeyCode.DownArrow;
                kPunch = KeyCode.Keypad1;  kKick  = KeyCode.Keypad2;
                kSpecial = KeyCode.Keypad3;
            }
        }

        public void ResetFighter()
        {
            hp = maxHP; spCooldown = 0; dead = false; isAttacking = false;
            blocking = false; walking = false;
            if (hpBar) hpBar.value = 1f;
            ResetAnimator();
        }

        public void SetInputEnabled(bool v)
        {
            inputEnabled = v;
            if (!v) SetWalking(false);
        }

        // ── Facing ────────────────────────────────────────────
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

        // ── Called by FighterAI ───────────────────────────────
        public void AIMoveDir(float dir)
        {
            if (dead || !inputEnabled) return;
            if (dir == 0) SetWalking(false); else Move(dir);
        }
        public void AIJump()             { if (!dead && inputEnabled && grounded) DoJump(); }
        public void AIPunch()            { if (!dead && inputEnabled) StartCoroutine(DoAttack("Punch",  punchDamage,   0.9f)); }
        public void AIKick()             { if (!dead && inputEnabled) StartCoroutine(DoAttack("Kick",   kickDamage,    1.0f)); }
        public void AISpecial()          { if (!dead && inputEnabled && SpecialReady) DoSpecial(); }
        public void AISetBlock(bool v)   { if (!dead && inputEnabled) SetBlocking(v); }

        // ── Update (player only) ──────────────────────────────
        void Update()
        {
            if (!inputEnabled || dead || !isPlayer) { spCooldown -= Time.deltaTime; return; }
            spCooldown -= Time.deltaTime;

            // Block
            SetBlocking(Input.GetKey(kBlock) && grounded);

            // Move (can't move while blocking)
            if (!blocking && !isAttacking)
            {
                float dir = 0;
                if (Input.GetKey(kLeft))  dir = -1;
                if (Input.GetKey(kRight)) dir =  1;
                if (dir != 0) Move(dir); else SetWalking(false);
            }
            else SetWalking(false);

            // Jump
            if (Input.GetKeyDown(kJump) && grounded && !blocking) DoJump();

            // Attacks
            if (!isAttacking)
            {
                if (Input.GetKeyDown(kPunch))  StartCoroutine(DoAttack("Punch",  punchDamage,  0.9f));
                if (Input.GetKeyDown(kKick))   StartCoroutine(DoAttack("Kick",   kickDamage,   1.0f));
                if (Input.GetKeyDown(kSpecial) && SpecialReady) DoSpecial();
            }

            FaceOpponent();
        }

        void Move(float dir)
        {
            if (dir == 0) return;
            transform.Translate(Vector3.right * dir * moveSpeed * Time.deltaTime, Space.World);
            SetWalking(true);
        }

        void DoJump()
        {
            grounded = false;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            AnimTrigger(HashJump);
        }

        IEnumerator DoAttack(string trigger, float dmg, float hitDelay)
        {
            isAttacking = true;
            SetWalking(false);
            AnimTrigger(Animator.StringToHash(trigger));    // "Punch" / "Kick"
            yield return new WaitForSeconds(hitDelay * 0.45f);   // wind-up

            // Hit detection (in front of the fighter, along the facing direction)
            Vector3 hitPos = transform.position + FacingVector * 1.1f + Vector3.up * 1f;
            var hits = Physics.OverlapSphere(hitPos, 0.75f);
            foreach (var h in hits)
            {
                if (h.gameObject == gameObject) continue;
                h.GetComponent<FighterController>()?.TakeDamage(dmg);
            }
            yield return new WaitForSeconds(hitDelay * 0.55f);   // recovery
            isAttacking = false;
        }

        void DoSpecial()
        {
            if (!specialPrefab) return;
            spCooldown = specialCooldown;
            AnimTrigger(HashSpecial);

            Transform origin   = EnsureFirePoint();
            Vector3   facing   = FacingVector;
            Vector3   spawnPos = origin.position + facing * muzzleForwardOffset;
            // The projectile inherits the caster's exact facing direction (rotation + velocity).
            Quaternion rot     = Quaternion.LookRotation(facing, Vector3.up);

            var proj = Instantiate(specialPrefab, spawnPos, rot);
            var rb2  = proj.GetComponent<Rigidbody>();
            if (rb2) rb2.velocity = facing * 16f;
            var dmg  = proj.GetComponent<ProjectileDamage>();
            if (dmg) { dmg.damage = specialDamage; dmg.owner = gameObject; }
            Destroy(proj, 3f);
        }

        /// <summary>Returns firePoint, creating a "FirePoint" child at chest height if none is assigned.</summary>
        Transform EnsureFirePoint()
        {
            if (firePoint) return firePoint;

            Transform existing = transform.Find("FirePoint");
            if (existing) { firePoint = existing; return firePoint; }

            var go = new GameObject("FirePoint");
            firePoint = go.transform;
            firePoint.SetParent(transform, false);
            firePoint.localPosition = new Vector3(0f, ChestLocalHeight(), 0f);
            firePoint.localRotation = Quaternion.identity;
            return firePoint;
        }

        float ChestLocalHeight()
        {
            var col = GetComponent<Collider>();
            if (col)
            {
                Bounds b = col.bounds;
                float worldY = b.min.y + b.size.y * chestHeightRatio;
                return transform.InverseTransformPoint(new Vector3(transform.position.x, worldY, transform.position.z)).y;
            }
            return fallbackChestHeight / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        }

        public void TakeDamage(float amount)
        {
            if (dead) return;
            float final = blocking ? amount * blockReduction : amount;
            hp = Mathf.Max(0, hp - final);
            if (hpBar) hpBar.value = hp / maxHP;

            if (!blocking) AnimTrigger(HashHit);

            if (hp <= 0)
            {
                dead = true;
                SetWalking(false);
                SetBlocking(false);
                AnimTrigger(HashDeath);
                OnKO?.Invoke();
            }
        }

        void FaceOpponent()
        {
            if (!opponent) return;
            float diff = opponent.position.x - transform.position.x;
            FaceDirection(diff > 0 ? 1 : -1);
        }

        void OnCollisionEnter(Collision col)
        {
            if (col.contacts[0].normal.y > 0.5f) grounded = true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 facing = Application.isPlaying ? FacingVector : transform.forward;
            Gizmos.DrawWireSphere(transform.position + facing * 1.1f + Vector3.up, 0.75f);
        }

        // ══════════════════════════════════════════════════════
        //  Animator hooks (all guarded: safe with no controller / missing params)
        // ══════════════════════════════════════════════════════
        void CacheAnimator()
        {
            animParams.Clear();
            animReady = anim != null && anim.runtimeAnimatorController != null;
            if (!animReady) return;
            foreach (var p in anim.parameters) animParams[p.nameHash] = p.type;
        }

        void AnimTrigger(int hash)
        {
            if (!animReady) return;
            if (animParams.TryGetValue(hash, out var t) && t == AnimatorControllerParameterType.Trigger)
                anim.SetTrigger(hash);
        }

        void AnimBool(int hash, bool value)
        {
            if (!animReady) return;
            if (animParams.TryGetValue(hash, out var t) && t == AnimatorControllerParameterType.Bool)
                anim.SetBool(hash, value);
        }

        void SetWalking(bool v)
        {
            if (walking == v) return;
            walking = v;
            AnimBool(HashWalking, v);
        }

        void SetBlocking(bool v)
        {
            if (blocking == v) return;
            blocking = v;
            AnimBool(HashBlocking, v);
        }

        /// <summary>Clears pending triggers and returns the Animator to Idle (called on every round reset).</summary>
        void ResetAnimator()
        {
            if (!animReady) return;
            AnimBool(HashWalking, false);
            AnimBool(HashBlocking, false);
            foreach (var kv in animParams)
                if (kv.Value == AnimatorControllerParameterType.Trigger) anim.ResetTrigger(kv.Key);

            if (anim.HasState(0, HashIdle)) anim.Play(HashIdle, 0, 0f);
            else AnimTrigger(HashIdle);
        }
    }
}
