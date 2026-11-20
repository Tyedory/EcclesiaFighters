using System.Collections;
using UnityEngine;

namespace EcclesiaFighters
{
    /// <summary>
    /// Upgraded FSM AI for Karl Marx (CPU). Attach alongside FighterController (isPlayer = false).
    ///
    /// Behavior:
    /// - Actively closes distance when out of melee range.
    /// - Mixes Light Punch, Heavy Punch, Kick, and occasional crouch-attacks at melee range.
    /// - Reactively blocks or crouches against incoming melee attacks / projectiles.
    /// - Uses Special Attack mostly at mid-to-long range with a cooldown gate (no nonstop spam).
    /// - Survives round resets cleanly (stays active across best-of-3 rounds).
    /// </summary>
    [RequireComponent(typeof(FighterController))]
    public class FighterAI : MonoBehaviour
    {
        public enum Difficulty { Facil = 0, Medio = 1, Dificil = 2 }

        [Header("AI Settings")]
        public Difficulty difficulty = Difficulty.Medio;

        [Header("Distâncias")]
        public float closeRange = 1.95f;
        public float midRange   = 5.5f;
        public float retreatHP  = 0.25f;

        // Tunables per difficulty
        float reactionDelay;
        float missChance;
        float blockReactChance;
        float crouchReactChance;
        float comboChance;
        float specialMinInterval;

        FighterController self;
        FighterController opponentCtrl;
        Coroutine         thinkRoutine;

        float lastSpecialTime  = -10f;
        float defenseCheckCd;

        enum State { Idle, Approach, Retreat, LightPunch, HeavyPunch, Kick, CrouchPoke, Special, Block, Crouch, Jump }
        State state = State.Idle;

        void Awake()
        {
            self = GetComponent<FighterController>();
        }

        void OnEnable()
        {
            ConfigDifficulty();
            if (thinkRoutine != null) StopCoroutine(thinkRoutine);
            thinkRoutine = StartCoroutine(Think());
        }

        void OnDisable()
        {
            if (thinkRoutine != null) { StopCoroutine(thinkRoutine); thinkRoutine = null; }
        }

        void Update()
        {
            if (defenseCheckCd > 0f) defenseCheckCd -= Time.deltaTime;
            if (!self || self.IsDead || !self.opponent) return;

            float diff = self.opponent.position.x - transform.position.x;
            if (Mathf.Abs(diff) >= 0.05f && !self.IsAttacking && !self.IsInHitstun)
                self.FaceDirection(diff > 0f ? 1 : -1);
        }

        void ConfigDifficulty()
        {
            switch (difficulty)
            {
                case Difficulty.Facil:
                    reactionDelay      = 0.55f;
                    missChance         = 0.28f;
                    blockReactChance   = 0.20f;
                    crouchReactChance  = 0.14f;
                    comboChance        = 0.15f;
                    specialMinInterval = 7.5f;
                    break;
                case Difficulty.Medio:
                    reactionDelay      = 0.28f;
                    missChance         = 0.12f;
                    blockReactChance   = 0.42f;
                    crouchReactChance  = 0.24f;
                    comboChance        = 0.40f;
                    specialMinInterval = 5.5f;
                    break;
                case Difficulty.Dificil:
                    reactionDelay      = 0.14f;
                    missChance         = 0.04f;
                    blockReactChance   = 0.68f;
                    crouchReactChance  = 0.35f;
                    comboChance        = 0.65f;
                    specialMinInterval = 4.0f;
                    break;
            }
        }

        FighterController GetOpponent()
        {
            if (!self || !self.opponent) return null;
            if (!opponentCtrl || opponentCtrl.transform != self.opponent)
                opponentCtrl = self.opponent.GetComponent<FighterController>();
            return opponentCtrl;
        }

        bool CanUseRangedSpecial()
        {
            return self.SpecialReady && (Time.time - lastSpecialTime >= specialMinInterval);
        }

        bool IsIncomingAttackThreat(float dist)
        {
            FighterController opp = GetOpponent();
            if (opp != null && opp.IsAttacking && dist <= closeRange + 1.1f) return true;

            foreach (ProjectileDamage p in FindObjectsOfType<ProjectileDamage>())
            {
                if (!p || p.owner == gameObject) continue;
                float dx = p.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) <= 4.0f && dx * self.FacingDir > 0f) return true;
            }
            return false;
        }

        // ── Main Decision Loop (persists across rounds) ────────
        IEnumerator Think()
        {
            while (true)
            {
                if (!self || self.IsDead || !self.InputEnabled || !self.opponent)
                {
                    if (self)
                    {
                        self.AIMoveDir(0f);
                        self.AISetBlock(false);
                        self.AISetCrouch(false);
                    }
                    yield return null;
                    continue;
                }

                // Wait out active attack or hitstun before making the next decision
                while (self.IsAttacking || self.IsInHitstun)
                {
                    if (self.IsDead || !self.InputEnabled) break;
                    yield return null;
                }

                if (self.IsDead || !self.InputEnabled || !self.opponent) continue;

                float dist = Mathf.Abs(transform.position.x - self.opponent.position.x);
                state = ChooseState(dist);
                yield return ExecuteState(dist);

                float pause = reactionDelay * Random.Range(0.55f, 1.05f);
                yield return WaitInterruptible(pause);
            }
        }

        IEnumerator WaitInterruptible(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (!self || self.IsDead || !self.InputEnabled || !self.opponent) yield break;
                float dist = Mathf.Abs(transform.position.x - self.opponent.position.x);
                if (defenseCheckCd <= 0f && IsIncomingAttackThreat(dist)) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        State ChooseState(float dist)
        {
            // Reactive defense if opponent is swinging nearby
            if (defenseCheckCd <= 0f && IsIncomingAttackThreat(dist))
            {
                defenseCheckCd = 0.75f;
                float r = Random.value;
                if (r < blockReactChance) return State.Block;
                if (r < blockReactChance + crouchReactChance) return State.Crouch;
            }

            // Low-HP spacing
            if (self.HPRatio <= retreatHP && dist < closeRange * 1.1f && Random.value < 0.45f)
                return State.Retreat;

            // 1) Melee Range
            if (dist <= closeRange)
            {
                float roll = Random.value;
                if (roll < 0.14f) return State.Block;
                if (roll < 0.24f) return State.Crouch;
                if (roll < 0.30f && CanUseRangedSpecial()) return State.Special;

                // Mix LightPunch, HeavyPunch, Kick, and CrouchPoke based on distance
                if (dist <= 1.35f)
                {
                    float atk = Random.value;
                    if (atk < 0.42f) return State.LightPunch;
                    if (atk < 0.68f) return State.HeavyPunch;
                    if (atk < 0.86f) return State.Kick;
                    return State.CrouchPoke;
                }
                else
                {
                    float atk = Random.value;
                    if (atk < 0.45f) return State.Kick;
                    if (atk < 0.75f) return State.HeavyPunch;
                    if (atk < 0.90f) return State.LightPunch;
                    return State.CrouchPoke;
                }
            }

            // 2) Mid Range: fire Special selectively or close the gap
            if (dist <= midRange)
            {
                if (CanUseRangedSpecial() && Random.value < 0.52f)
                    return State.Special;

                float r = Random.value;
                if (r < 0.72f) return State.Approach;
                if (r < 0.86f) return State.Jump;
                return State.Crouch;
            }

            // 3) Long Range: close distance actively, or fire ranged Special
            if (CanUseRangedSpecial() && Random.value < 0.40f)
                return State.Special;

            return Random.value < 0.82f ? State.Approach : State.Jump;
        }

        IEnumerator ExecuteState(float initialDist)
        {
            switch (state)
            {
                case State.Idle:
                    yield return new WaitForSeconds(Random.Range(0.06f, 0.16f));
                    break;

                case State.Approach:
                {
                    float maxDur = initialDist > midRange ? Random.Range(0.55f, 0.95f) : Random.Range(0.30f, 0.60f);
                    float t = 0f;
                    while (t < maxDur && self && !self.IsDead && self.InputEnabled && self.opponent)
                    {
                        float d = Mathf.Abs(transform.position.x - self.opponent.position.x);
                        if (d <= closeRange * 0.85f) break;
                        if (defenseCheckCd <= 0f && IsIncomingAttackThreat(d)) break;

                        int dir = transform.position.x < self.opponent.position.x ? 1 : -1;
                        self.AIMoveDir(dir);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    if (self) self.AIMoveDir(0f);
                    break;
                }

                case State.Retreat:
                {
                    float dur = Random.Range(0.28f, 0.48f);
                    float t   = 0f;
                    while (t < dur && self && !self.IsDead && self.InputEnabled && self.opponent)
                    {
                        int dir = transform.position.x < self.opponent.position.x ? -1 : 1;
                        self.AIMoveDir(dir);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    if (self) self.AIMoveDir(0f);
                    break;
                }

                case State.LightPunch:
                    if (Random.value >= missChance)
                    {
                        self.AILightPunch();
                        yield return WaitAttackFinish();
                        // Optional combo follow-up into HeavyPunch or Kick
                        if (self && !self.IsDead && self.opponent && Random.value < comboChance)
                        {
                            float d = Mathf.Abs(transform.position.x - self.opponent.position.x);
                            if (d <= closeRange)
                            {
                                if (Random.value < 0.5f) self.AIHeavyPunch();
                                else self.AIKick();
                                yield return WaitAttackFinish();
                            }
                        }
                    }
                    break;

                case State.HeavyPunch:
                    if (Random.value >= missChance)
                    {
                        self.AIHeavyPunch();
                        yield return WaitAttackFinish();
                    }
                    break;

                case State.Kick:
                    if (Random.value >= missChance)
                    {
                        self.AIKick();
                        yield return WaitAttackFinish();
                    }
                    break;

                case State.CrouchPoke:
                    self.AISetCrouch(true);
                    yield return new WaitForSeconds(0.08f);
                    if (self && !self.IsDead && self.InputEnabled && Random.value >= missChance)
                    {
                        if (Random.value < 0.55f) self.AILightPunch();
                        else self.AIKick();
                        yield return WaitAttackFinish();
                    }
                    if (self) self.AISetCrouch(false);
                    break;

                case State.Special:
                    if (CanUseRangedSpecial() && Random.value >= missChance * 0.5f)
                    {
                        lastSpecialTime = Time.time;
                        self.AISpecial();
                        yield return WaitAttackFinish();
                    }
                    break;

                case State.Block:
                {
                    self.AISetBlock(true);
                    float dur = Random.Range(0.28f, 0.52f);
                    float t   = 0f;
                    while (t < dur && self && !self.IsDead && self.InputEnabled)
                    {
                        t += Time.deltaTime;
                        yield return null;
                    }
                    if (self) self.AISetBlock(false);
                    break;
                }

                case State.Crouch:
                {
                    self.AISetCrouch(true);
                    float dur = Random.Range(0.22f, 0.42f);
                    float t   = 0f;
                    while (t < dur && self && !self.IsDead && self.InputEnabled)
                    {
                        t += Time.deltaTime;
                        yield return null;
                    }
                    if (self) self.AISetCrouch(false);
                    break;
                }

                case State.Jump:
                    if (self.IsGrounded)
                    {
                        self.AIJump();
                        float dur = 0.42f;
                        float t   = 0f;
                        int dir   = self.opponent ? (transform.position.x < self.opponent.position.x ? 1 : -1) : self.FacingDir;
                        while (t < dur && self && !self.IsDead && self.InputEnabled)
                        {
                            self.AIMoveDir(dir);
                            t += Time.deltaTime;
                            yield return null;
                        }
                        if (self) self.AIMoveDir(0f);
                    }
                    break;
            }
        }

        IEnumerator WaitAttackFinish()
        {
            float safety = 1.2f;
            while (self && self.IsAttacking && !self.IsDead && safety > 0f)
            {
                safety -= Time.deltaTime;
                yield return null;
            }
        }
    }
}
