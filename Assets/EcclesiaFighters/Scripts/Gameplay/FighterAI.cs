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
        public bool       useMatchConfigDifficulty = true;

        [Header("Distâncias")]
        public float closeRange = 1.32f;
        public float midRange   = 5.5f;
        public float retreatHP  = 0.25f;

        // Tunables per difficulty
        float reactionDelay;
        float hesitationChance;
        float missChance;
        float blockReactChance;
        float crouchReactChance;
        float defenseCheckInterval = 0.75f;
        float comboChance;
        float specialMinInterval;
        float specialMidChance     = 0.48f;
        float specialLongChance    = 0.38f;
        float punishChance         = 0.42f;
        float antiJumpChance       = 0.35f;
        float meleeBlockChance     = 0.14f;
        float chargeChance         = 0.36f;

        FighterController self;
        FighterController opponentCtrl;
        Coroutine         thinkRoutine;

        float lastSpecialTime  = -10f;
        float defenseCheckCd;

        enum State { Idle, Approach, Retreat, LightPunch, HeavyPunch, Kick, CrouchPoke, Special, Block, Crouch, Jump, Charge }
        State state = State.Idle;

        void Awake()
        {
            self = GetComponent<FighterController>();
            if (closeRange > 1.45f) closeRange = 1.32f;
        }

        void OnEnable()
        {
            if (self && self.isPlayerControlled)
            {
                enabled = false;
                return;
            }
            if (useMatchConfigDifficulty)
                difficulty = MatchConfig.ToFighterAIDifficulty(MatchConfig.Difficulty);
            ConfigDifficulty();
            if (thinkRoutine != null) StopCoroutine(thinkRoutine);
            thinkRoutine = StartCoroutine(Think());
        }

        public void SetDifficulty(Difficulty newDifficulty)
        {
            difficulty = newDifficulty;
            useMatchConfigDifficulty = false;
            ConfigDifficulty();
        }

        void OnDisable()
        {
            if (thinkRoutine != null) { StopCoroutine(thinkRoutine); thinkRoutine = null; }
            if (self && !self.isPlayerControlled)
            {
                self.AIMoveDir(0f);
                self.AISetBlock(false);
                self.AISetCrouch(false);
                self.AISetCharging(false);
            }
        }

        void Update()
        {
            if (Time.timeScale <= 0f || FightOverlayController.IsAnyModalOpen) return;
            if (defenseCheckCd > 0f) defenseCheckCd -= Time.deltaTime;
            if (!self) return;
            if (self.isPlayerControlled)
            {
                enabled = false;
                return;
            }
            if (self.IsDead || !self.opponent) return;

            float diff = self.opponent.position.x - transform.position.x;
            if (Mathf.Abs(diff) >= 0.05f && !self.IsAttacking && !self.IsInHitstun)
                self.FaceDirection(diff > 0f ? 1 : -1);
        }

        void ConfigDifficulty()
        {
            switch (difficulty)
            {
                case Difficulty.Facil:
                    reactionDelay        = 0.72f;
                    hesitationChance     = 0.30f;
                    missChance           = 0.30f;
                    blockReactChance     = 0.12f;
                    crouchReactChance    = 0.08f;
                    defenseCheckInterval = 1.25f;
                    comboChance          = 0.10f;
                    specialMinInterval   = 9.0f;
                    specialMidChance     = 0.22f;
                    specialLongChance    = 0.18f;
                    punishChance         = 0.10f;
                    antiJumpChance       = 0.10f;
                    meleeBlockChance     = 0.06f;
                    chargeChance         = 0.08f;
                    break;
                case Difficulty.Medio:
                    reactionDelay        = 0.28f;
                    hesitationChance     = 0.06f;
                    missChance           = 0.12f;
                    blockReactChance     = 0.42f;
                    crouchReactChance    = 0.22f;
                    defenseCheckInterval = 0.75f;
                    comboChance          = 0.40f;
                    specialMinInterval   = 5.5f;
                    specialMidChance     = 0.48f;
                    specialLongChance    = 0.38f;
                    punishChance         = 0.42f;
                    antiJumpChance       = 0.35f;
                    meleeBlockChance     = 0.14f;
                    chargeChance         = 0.36f;
                    break;
                case Difficulty.Dificil:
                    reactionDelay        = 0.12f;
                    hesitationChance     = 0.0f;
                    missChance           = 0.03f;
                    blockReactChance     = 0.72f;
                    crouchReactChance    = 0.16f;
                    defenseCheckInterval = 0.38f;
                    comboChance          = 0.70f;
                    specialMinInterval   = 3.4f;
                    specialMidChance     = 0.65f;
                    specialLongChance    = 0.58f;
                    punishChance         = 0.78f;
                    antiJumpChance       = 0.68f;
                    meleeBlockChance     = 0.18f;
                    chargeChance         = 0.58f;
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
            return self.IsSpecialReady && (Time.time - lastSpecialTime >= specialMinInterval);
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
                if (self && self.isPlayerControlled)
                {
                    enabled = false;
                    yield break;
                }

                if (Time.timeScale <= 0f || FightOverlayController.IsAnyModalOpen)
                {
                    yield return null;
                    continue;
                }

                if (!self || self.IsDead || !self.InputEnabled || !self.opponent)
                {
                    if (self)
                    {
                        self.AIMoveDir(0f);
                        self.AISetBlock(false);
                        self.AISetCrouch(false);
                        self.AISetCharging(false);
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
                defenseCheckCd = defenseCheckInterval;
                float r = Random.value;
                if (r < blockReactChance) return State.Block;
                if (r < blockReactChance + crouchReactChance) return State.Crouch;
            }

            // Anti-jump pressure & punish openings
            FighterController opp = GetOpponent();
            if (opp != null && !opp.IsDead)
            {
                if (!opp.IsGrounded && dist <= midRange && Random.value < antiJumpChance)
                {
                    if (dist <= closeRange + 0.45f)
                        return Random.value < 0.60f ? State.Kick : State.LightPunch;
                    if (CanUseRangedSpecial() && Random.value < 0.55f)
                        return State.Special;
                    return State.Approach;
                }

                if (opp.IsInHitstun && dist <= closeRange + 0.25f && Random.value < punishChance)
                    return Random.value < 0.55f ? State.HeavyPunch : State.Kick;
            }

            // Intentional hesitation pause on lower tiers
            if (hesitationChance > 0f && Random.value < hesitationChance)
                return State.Idle;

            // Low-HP spacing
            if (self.HPRatio <= retreatHP && dist < closeRange * 1.1f && Random.value < 0.45f)
                return State.Retreat;

            // 1) Melee Range
            if (dist <= closeRange)
            {
                float roll = Random.value;
                float crouchThreshold = meleeBlockChance + crouchReactChance * 0.40f;
                if (roll < meleeBlockChance) return State.Block;
                if (roll < crouchThreshold)  return State.Crouch;
                if (roll < crouchThreshold + 0.08f && CanUseRangedSpecial()) return State.Special;

                // Mix LightPunch, HeavyPunch, Kick, and CrouchPoke based on distance
                if (dist <= 1.05f)
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

            // 2) Mid Range: fire Special selectively, charge energy safely, or close the gap
            if (dist <= midRange)
            {
                if (CanUseRangedSpecial() && Random.value < specialMidChance)
                    return State.Special;

                if (dist >= midRange * 0.78f && self.IsGrounded && !self.IsSpecialReady
                    && !IsIncomingAttackThreat(dist) && Random.value < chargeChance * 0.65f)
                    return State.Charge;

                float r = Random.value;
                if (r < 0.72f) return State.Approach;
                if (r < 0.86f) return State.Jump;
                return State.Crouch;
            }

            // 3) Long Range: fire ranged Special, safely charge energy, or close distance actively
            if (CanUseRangedSpecial() && Random.value < specialLongChance)
                return State.Special;

            if (self.IsGrounded && !self.IsSpecialReady && !IsIncomingAttackThreat(dist) && Random.value < chargeChance)
                return State.Charge;

            return Random.value < 0.82f ? State.Approach : State.Jump;
        }

        IEnumerator ExecuteState(float initialDist)
        {
            switch (state)
            {
                case State.Idle:
                    float idleDur = difficulty == Difficulty.Facil
                        ? Random.Range(0.28f, 0.48f)
                        : Random.Range(0.06f, 0.16f);
                    yield return new WaitForSeconds(idleDur);
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

                case State.Charge:
                {
                    float maxDur = difficulty == Difficulty.Dificil
                        ? Random.Range(0.75f, 1.35f)
                        : (difficulty == Difficulty.Medio ? Random.Range(0.85f, 1.50f) : Random.Range(0.65f, 1.10f));
                    float t = 0f;
                    self.AISetCharging(true);
                    while (t < maxDur && self != null && !self.IsDead && self.InputEnabled && self.opponent != null)
                    {
                        if (self.IsSpecialReady || self.IsInHitstun || !self.IsGrounded) break;
                        float d = Mathf.Abs(transform.position.x - self.opponent.position.x);
                        if (d < midRange * 0.70f || IsIncomingAttackThreat(d))
                        {
                            defenseCheckCd = 0f;
                            break;
                        }
                        t += Time.deltaTime;
                        yield return null;
                    }
                    if (self != null) self.AISetCharging(false);
                    break;
                }
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
