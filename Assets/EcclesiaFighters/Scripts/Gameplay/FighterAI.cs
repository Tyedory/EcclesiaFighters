using System.Collections;
using UnityEngine;

namespace EcclesiaFighters
{
    /// <summary>
    /// FSM AI para Karl Marx (CPU). Attach ao prefab junto com FighterController (isPlayer=false).
    ///
    /// Estados:
    ///   Idle      – pausa curta antes de decidir
    ///   Approach  – anda em direção ao jogador
    ///   Retreat   – recua quando HP < 25%
    ///   Punch     – soco (distância próxima)
    ///   Kick      – chute (distância próxima)
    ///   Special   – golpe especial (FOICE+MARTELO — usada em qualquer distância)
    ///   Block     – bloqueia por instante
    ///   Jump      – salta para fechar distância
    ///
    /// Dificuldades (Inspector):
    ///   0 = Fácil    delay 0.8s | erra 30% | especial toda 8s
    ///   1 = Médio    delay 0.4s | erra 12% | especial toda 4s
    ///   2 = Difícil  delay 0.15s| erra 4%  | especial assim que disponível
    /// </summary>
    [RequireComponent(typeof(FighterController))]
    public class FighterAI : MonoBehaviour
    {
        public enum Difficulty { Facil = 0, Medio = 1, Dificil = 2 }

        [Header("AI Settings")]
        public Difficulty difficulty = Difficulty.Medio;

        [Header("Distâncias")]
        public float closeRange  = 2.0f;   // soco/chute
        public float midRange    = 5.5f;   // pode usar especial
        public float retreatHP   = 0.25f;  // abaixo disso = Retreat

        // ── tunáveis por dificuldade ──────────────────────────
        float reactionDelay;   // delay entre decisões (ms)
        float missChance;      // probabilidade de errar o ataque
        float specialCooldownBonus; // tempo extra de cooldown (0 = usa assim que disponível)

        FighterController self;
        float specialTimer = 0f;  // contador local para forçar uso de especial

        enum State { Idle, Approach, Retreat, Punch, Kick, Special, Block, Jump }
        State state = State.Idle;

        // ── ciclo de especial garantido ──────────────────────
        // Marx GARANTE usar o especial periodicamente independente do estado
        float specialForceInterval;  // a cada N segundos força o especial

        void Awake()  { self = GetComponent<FighterController>(); }

        // ── Facing ───────────────────────────────────────────
        // Marx compares his X to the opponent's X every frame and turns to face them.
        // FighterController.FaceDirection() performs the actual flip: it mirrors
        // transform.localScale.x when facingMode = FlipScaleX (flat sprites), or rotates
        // the root ±90° around Y when facingMode = RotateY (3D models, the default).
        void Update()
        {
            if (!self || self.IsDead || !self.opponent) return;

            float diff = self.opponent.position.x - transform.position.x;
            if (Mathf.Abs(diff) < 0.01f) return;           // overlapping: keep current facing
            self.FaceDirection(diff > 0f ? 1 : -1);
        }

        void OnEnable()
        {
            ConfigDifficulty();
            StartCoroutine(Think());
            StartCoroutine(SpecialForcer()); // loop separado para garantir uso do especial
        }

        void ConfigDifficulty()
        {
            switch (difficulty)
            {
                case Difficulty.Facil:
                    reactionDelay = 0.80f;
                    missChance    = 0.30f;
                    specialForceInterval = 8f;
                    break;
                case Difficulty.Medio:
                    reactionDelay = 0.40f;
                    missChance    = 0.12f;
                    specialForceInterval = 4f;
                    break;
                case Difficulty.Dificil:
                    reactionDelay = 0.15f;
                    missChance    = 0.04f;
                    specialForceInterval = 2f;
                    break;
            }
        }

        // ── Loop de decisão principal ─────────────────────────
        IEnumerator Think()
        {
            while (true)
            {
                if (self.IsDead) yield break;
                yield return new WaitForSeconds(reactionDelay + Random.Range(0f, 0.12f));
                if (!self || !self.opponent) continue;

                float dist = Mathf.Abs(transform.position.x - self.opponent.position.x);
                state = ChooseState(dist);
                yield return ExecuteState(dist);
            }
        }

        // ── Loop dedicado ao especial ─────────────────────────
        // Garante que Marx usa o especial regularmente
        IEnumerator SpecialForcer()
        {
            yield return new WaitForSeconds(2f); // aguarda o início do round
            while (true)
            {
                yield return new WaitForSeconds(specialForceInterval + Random.Range(-0.5f, 0.5f));
                if (self.IsDead) yield break;
                if (!self.SpecialReady) { yield return new WaitUntil(() => self.SpecialReady || self.IsDead); }
                if (self.IsDead) yield break;

                // Aproxima até range médio se necessário
                float dist = Mathf.Abs(transform.position.x - self.opponent.position.x);
                if (dist > midRange * 1.3f)
                {
                    float apTime = 0.4f;
                    float t = 0;
                    while (t < apTime && !self.IsDead)
                    {
                        int dir = transform.position.x < self.opponent.position.x ? 1 : -1;
                        self.AIMoveDir(dir);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    self.AIMoveDir(0);
                }

                // Dispara o especial
                if (Random.value >= missChance * 0.5f && !self.IsDead)
                    self.AISpecial();

                yield return new WaitForSeconds(0.5f);
            }
        }

        State ChooseState(float dist)
        {
            float hp = self.HPRatio;

            // Recua se estiver mal de vida e perto
            if (hp <= retreatHP && dist < midRange) return State.Retreat;

            // Bloqueia aleatoriamente (mais frequente no dificil)
            float blockChance = difficulty == Difficulty.Dificil ? 0.3f : 0.15f;
            if (Random.value < blockChance && dist < closeRange) return State.Block;

            if (dist < closeRange)
            {
                // Perto: soco ou chute, às vezes especial se disponível
                if (self.SpecialReady && Random.value < 0.35f) return State.Special;
                return Random.value < 0.55f ? State.Punch : State.Kick;
            }

            if (dist < midRange)
            {
                // Médio: especial se disponível (alta chance), senão aproxima
                if (self.SpecialReady && Random.value < 0.55f) return State.Special;
                return Random.value < 0.65f ? State.Approach : State.Jump;
            }

            // Longe: aproxima, às vezes pula para chegar mais rápido
            return Random.value < 0.7f ? State.Approach : State.Jump;
        }

        IEnumerator ExecuteState(float dist)
        {
            switch (state)
            {
                // ── Idle: pausa breve ──
                case State.Idle:
                    yield return new WaitForSeconds(Random.Range(0.08f, 0.25f));
                    break;

                // ── Approach: anda em direção ao oponente ──
                case State.Approach:
                {
                    float dur = Random.Range(0.2f, 0.5f);
                    float t = 0;
                    while (t < dur && !self.IsDead)
                    {
                        int dir = transform.position.x < self.opponent.position.x ? 1 : -1;
                        self.AIMoveDir(dir);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    self.AIMoveDir(0);
                    break;
                }

                // ── Retreat: recua ──
                case State.Retreat:
                {
                    float dur = Random.Range(0.3f, 0.5f);
                    float t = 0;
                    while (t < dur && !self.IsDead)
                    {
                        int dir = transform.position.x < self.opponent.position.x ? -1 : 1;
                        self.AIMoveDir(dir);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    self.AIMoveDir(0);
                    break;
                }

                // ── Punch ──
                case State.Punch:
                    if (Random.value >= missChance) self.AIPunch();
                    yield return new WaitForSeconds(0.5f);
                    break;

                // ── Kick ──
                case State.Kick:
                    if (Random.value >= missChance) self.AIKick();
                    yield return new WaitForSeconds(0.55f);
                    break;

                // ── Special: FOICE + MARTELO ──
                case State.Special:
                    if (self.SpecialReady && Random.value >= missChance * 0.5f)
                        self.AISpecial();
                    yield return new WaitForSeconds(0.8f);
                    break;

                // ── Block ──
                case State.Block:
                    self.AISetBlock(true);
                    yield return new WaitForSeconds(Random.Range(0.3f, 0.65f));
                    self.AISetBlock(false);
                    break;

                // ── Jump: salta para fechar distância ──
                case State.Jump:
                    if (self.IsGrounded) self.AIJump();
                    yield return new WaitForSeconds(0.55f);
                    break;
            }
        }
    }
}
