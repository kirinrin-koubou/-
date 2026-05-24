using System.Collections.Generic;
using UnityEngine;

namespace SmashBattle
{
    /// <summary>
    /// Logical input snapshot for a single frame. Filled either by the human input
    /// reader, the mobile control overlay, or the CPU AI.
    /// </summary>
    public struct FighterInput
    {
        public bool left;
        public bool right;
        public bool up;
        public bool down;
        public bool jab;        // Z
        public bool smash;      // X
        public bool special;    // Z+X+down macro
        public bool skill;      // X+C equipped move
        public bool combo;      // Z+X
        public bool shield;     // Space
        public bool trait;      // C
        public bool skillSlot1; // V
        public bool skillSlot2; // B

        public static FighterInput None => default;
    }

    /// <summary>
    /// High-level behaviour states used by both the player feedback and the CPU AI.
    /// </summary>
    public enum FighterState
    {
        Idle,
        Approach,
        Attack,
        Retreat,
        Recover,
        Hitstun,
        Shielding,
        Special
    }

    /// <summary>
    /// Core fighter behaviour. Owns physics, damage/knockback, attacks, shielding,
    /// buffs, traits, jumping, dashing, special dispatch and (for the CPU) AI.
    ///
    /// All combat math runs in game-space (the original 900x600 canvas coordinate
    /// space), matching the JS source. <see cref="BattleManager.GameToWorld"/> maps
    /// the logical position to a Unity transform for rendering.
    /// </summary>
    public class Fighter : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Configuration
        // ---------------------------------------------------------------------
        [Header("Identity")]
        [SerializeField] private bool isPlayer = true;
        [SerializeField] private string characterId = "sakura";

        [Header("Body (game units)")]
        [SerializeField] private float bodyWidth = 44f;
        [SerializeField] private float bodyHeight = 60f;

        [Header("Physics")]
        [SerializeField] private float gravity = 0.6f;
        [SerializeField] private float maxFallSpeed = 18f;
        [SerializeField] private float groundFriction = 0.8f;
        [SerializeField] private float airDrag = 0.96f;
        [SerializeField] private int maxJumps = 2;

        [Header("Equipped Skills")]
        [SerializeField] private string skillSlot1Id = "ironfist";
        [SerializeField] private string skillSlot2Id = "fireball";

        // ---------------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------------
        public bool IsPlayer => isPlayer;

        /// <summary>Damage percent. Higher % => more knockback received.</summary>
        public float Damage { get; private set; }

        /// <summary>Remaining lives.</summary>
        public int Stocks { get; private set; }

        /// <summary>+1 facing right, -1 facing left.</summary>
        public int Facing { get; private set; } = 1;

        public bool OnGround { get; private set; }
        public bool IsDead => Stocks <= 0;

        /// <summary>Logical position in game-space (top-left origin, Y grows down).</summary>
        public Vector2 GamePosition;
        public Vector2 Velocity;

        public int Hitstun { get; private set; }
        public int FrozenTimer { get; private set; }
        public bool Invincible => _invincibleTimer > 0;
        public FighterState State { get; private set; } = FighterState.Idle;

        public CharacterDefinition Definition { get; private set; }

        // Buff system (Balance special).
        public float BuffAtkMul { get; private set; } = 1f;
        public float BuffKbMul { get; private set; } = 1f;
        private int _buffTimer;

        // Skill buff system (e.g. Thorn Armor / Blessing boosts).
        public float SkillBuff { get; private set; } = 0f;
        private int _skillBuffTimer;

        // ---------------------------------------------------------------------
        // Internal timers / counters
        // ---------------------------------------------------------------------
        private int _invincibleTimer;
        private int _attackCooldown;
        private int _jumpsLeft;
        private int _shieldHealth;
        private const int ShieldMax = 120;
        private bool _shielding;
        private int _dashTimer;
        private int _comboStep;
        private int _comboWindow;
        private int _skillCooldown1;
        private int _skillCooldown2;
        private float _regenAccumulator;

        // Active attack boxes for the current frame window.
        private readonly List<AttackBox> _activeBoxes = new List<AttackBox>();
        public IReadOnlyList<AttackBox> ActiveBoxes => _activeBoxes;

        private Fighter _opponent;
        private SpecialMoveController _special;
        private FighterInput _input;

        // CPU AI scratch state.
        private int _aiDecisionTimer;
        private FighterState _aiState = FighterState.Idle;

        // ---------------------------------------------------------------------
        // Unity lifecycle
        // ---------------------------------------------------------------------
        private void Awake()
        {
            Definition = CharacterDatabase.GetById(characterId);
            _special = GetComponent<SpecialMoveController>();
            if (_special == null) _special = gameObject.AddComponent<SpecialMoveController>();
            _special.Init(this);
        }

        private void Start()
        {
            // Resolve opponent from the battle manager.
            var bm = BattleManager.Instance;
            if (bm != null)
                _opponent = isPlayer ? bm.Cpu : bm.Player;
        }

        // Combat math advances on a fixed step so frame counts mirror the 60fps JS loop.
        private void FixedUpdate()
        {
            if (BattleManager.Instance != null && !BattleManager.Instance.BattleActive)
            {
                ApplyTransform();
                return;
            }
            if (IsDead) return;

            GatherInput();
            TickTimers();

            if (FrozenTimer > 0)
            {
                FrozenTimer--;
                ApplyPhysics();
                ApplyTransform();
                return;
            }

            if (Hitstun > 0)
            {
                State = FighterState.Hitstun;
                Hitstun--;
                ApplyPhysics();
                ResolveOutgoingHits();
                ApplyTransform();
                return;
            }

            HandleMovement();
            HandleActions();
            _special.UpdateSpecial(_opponent);
            ApplyPhysics();
            ResolveOutgoingHits();
            PassiveTraits();
            ApplyTransform();
        }

        // ---------------------------------------------------------------------
        // Match management (called by BattleManager)
        // ---------------------------------------------------------------------
        /// <summary>Resets all combat state for the start of a fresh match.</summary>
        public void ResetForMatch(int stocks, bool asPlayer)
        {
            isPlayer = asPlayer;
            Stocks = stocks;
            Damage = 0f;
            Hitstun = 0;
            FrozenTimer = 0;
            _invincibleTimer = 60;
            Velocity = Vector2.zero;
            _jumpsLeft = maxJumps;
            _shieldHealth = ShieldMax;
            _shielding = false;
            _buffTimer = 0;
            BuffAtkMul = 1f;
            BuffKbMul = 1f;
            SkillBuff = 0f;
            _skillBuffTimer = 0;
            _activeBoxes.Clear();
            State = FighterState.Idle;

            var bm = BattleManager.Instance;
            float ground = bm != null ? bm.GroundY : 480f;
            float spawnX = asPlayer ? 320f : 580f;
            GamePosition = new Vector2(spawnX, ground - bodyHeight);
            Facing = asPlayer ? 1 : -1;
        }

        /// <summary>Removes one stock and zeroes momentum.</summary>
        public void LoseStock()
        {
            Stocks = Mathf.Max(0, Stocks - 1);
            Velocity = Vector2.zero;
            Hitstun = 0;
        }

        /// <summary>Returns the fighter to the stage after a KO.</summary>
        public void Respawn(Vector2 gamePos)
        {
            GamePosition = gamePos;
            Velocity = Vector2.zero;
            Damage = 0f;
            Hitstun = 0;
            FrozenTimer = 0;
            _invincibleTimer = 90;
            _jumpsLeft = maxJumps;
            State = FighterState.Idle;
        }

        // ---------------------------------------------------------------------
        // Geometry
        // ---------------------------------------------------------------------
        /// <summary>The fighter's body rect in game-space (for hit detection).</summary>
        public Rect GetBodyRect()
        {
            return new Rect(GamePosition.x, GamePosition.y, bodyWidth, bodyHeight);
        }

        /// <summary>Centre point of the body in game-space.</summary>
        public Vector2 Center => GamePosition + new Vector2(bodyWidth * 0.5f, bodyHeight * 0.5f);

        public float BodyWidth => bodyWidth;
        public float BodyHeight => bodyHeight;

        // ---------------------------------------------------------------------
        // Input
        // ---------------------------------------------------------------------
        /// <summary>Externally injected input (mobile controls / test harness).</summary>
        public void SetExternalInput(FighterInput input) => _externalInput = input;
        private FighterInput _externalInput;
        private bool _useExternalInput;

        /// <summary>Enables or disables external input override.</summary>
        public void UseExternalInput(bool value) => _useExternalInput = value;

        private void GatherInput()
        {
            if (!isPlayer)
            {
                _input = ComputeAIInput();
                return;
            }

            if (_useExternalInput)
            {
                _input = _externalInput;
                return;
            }

            // Keyboard fallback (desktop).
            var i = FighterInput.None;
            i.left = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            i.right = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
            i.up = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            i.down = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            i.jab = Input.GetKey(KeyCode.Z);
            i.smash = Input.GetKey(KeyCode.X);
            i.shield = Input.GetKey(KeyCode.Space);
            i.trait = Input.GetKey(KeyCode.C);
            i.skillSlot1 = Input.GetKey(KeyCode.V);
            i.skillSlot2 = Input.GetKey(KeyCode.B);
            i.combo = i.jab && i.smash;
            i.special = i.jab && i.smash && i.down;
            i.skill = i.smash && i.trait;
            _input = i;
        }

        // ---------------------------------------------------------------------
        // Movement & physics
        // ---------------------------------------------------------------------
        private void HandleMovement()
        {
            float spd = Definition.stats.spd;
            if (HasTrait(TraitType.Rapid)) spd *= 1.08f;

            // Game-space speed scale (JS used pixel/frame velocities).
            float moveVel = spd;

            if (_shielding)
            {
                // No walking while shielding; bleed horizontal momentum.
                Velocity.x *= 0.6f;
                return;
            }

            if (_input.left)
            {
                Velocity.x = -moveVel;
                Facing = -1;
                if (State != FighterState.Attack) State = FighterState.Approach;
            }
            else if (_input.right)
            {
                Velocity.x = moveVel;
                Facing = 1;
                if (State != FighterState.Attack) State = FighterState.Approach;
            }
            else if (OnGround)
            {
                Velocity.x *= groundFriction;
                if (Mathf.Abs(Velocity.x) < 0.1f) { Velocity.x = 0f; if (State == FighterState.Approach) State = FighterState.Idle; }
            }

            // Jump (with double jump).
            if (_input.up && !_prevUp)
            {
                if (_jumpsLeft > 0)
                {
                    float jp = Definition.stats.jump;
                    if (HasTrait(TraitType.Lightweight) || HasTrait(TraitType.Aerial)) jp *= 1.08f;
                    Velocity.y = -jp; // negative = upward in game-space
                    _jumpsLeft--;
                    OnGround = false;
                    AudioManager.Instance?.PlaySound("jump");
                }
            }
            _prevUp = _input.up;

            // Dash: tap a direction while already moving fast.
            if (_dashTimer > 0) _dashTimer--;
        }
        private bool _prevUp;

        private void ApplyPhysics()
        {
            // Gravity (game-space Y grows downward).
            Velocity.y += gravity;
            if (Velocity.y > maxFallSpeed) Velocity.y = maxFallSpeed;
            if (!OnGround) Velocity.x *= airDrag;

            GamePosition += Velocity;

            // Ground collision against the stage platform.
            var bm = BattleManager.Instance;
            float groundY = bm != null ? bm.GroundY : 480f;
            float left = bm != null ? bm.StageLeft : 200f;
            float right = bm != null ? bm.StageRight : 700f;
            float feet = GamePosition.y + bodyHeight;

            bool overPlatform = (GamePosition.x + bodyWidth > left) && (GamePosition.x < right);
            if (overPlatform && feet >= groundY && Velocity.y >= 0f)
            {
                GamePosition.y = groundY - bodyHeight;
                Velocity.y = 0f;
                if (!OnGround)
                {
                    OnGround = true;
                    _jumpsLeft = maxJumps;
                    if (State == FighterState.Recover) State = FighterState.Idle;
                }
            }
            else
            {
                OnGround = false;
            }
        }

        private void ApplyTransform()
        {
            var bm = BattleManager.Instance;
            if (bm != null)
                transform.position = bm.GameToWorld(Center);
            // Flip sprite to face movement.
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * Facing;
            transform.localScale = s;
        }

        // ---------------------------------------------------------------------
        // Actions (attacks / shield / skills / special)
        // ---------------------------------------------------------------------
        private void HandleActions()
        {
            // Shield.
            _shielding = _input.shield && OnGround && _shieldHealth > 0;
            if (_shielding)
            {
                State = FighterState.Shielding;
                int drain = HasTrait(TraitType.Guard) ? 1 : 2;
                if (HasTrait(TraitType.LateBlocker)) drain = 1;
                _shieldHealth = Mathf.Max(0, _shieldHealth - drain);
                AudioManager.Instance?.PlaySound("shield");
                return;
            }
            else
            {
                int regen = HasTrait(TraitType.Guard) ? 2 : 1;
                _shieldHealth = Mathf.Min(ShieldMax, _shieldHealth + regen);
            }

            if (_attackCooldown > 0) return;

            // Special macro (Z+X+down).
            if (_input.special)
            {
                TriggerSpecial();
                return;
            }

            // Equipped skill (X+C) or skill slots (V/B).
            if (_input.skillSlot1) { UseSkill(0); return; }
            if (_input.skillSlot2) { UseSkill(1); return; }
            if (_input.skill) { UseSkill(0); return; }

            // Combo (Z+X) -> escalating smash chain.
            if (_input.combo)
            {
                DoComboChain();
                return;
            }

            // Smash variants (X).
            if (_input.smash)
            {
                if (_input.down) DoAttack("down_smash");
                else if (_input.left || _input.right)
                {
                    DoAttack(_dashTimer > 0 ? "dash_attack" : "smash");
                }
                else DoAttack("smash");
                return;
            }

            // Jab chain (Z): jab -> jab2 -> jab3 within window.
            if (_input.jab)
            {
                DoJabChain();
                return;
            }
        }

        private void DoJabChain()
        {
            if (_comboWindow > 0 && _comboStep == 1) { DoAttack("jab2"); _comboStep = 2; }
            else if (_comboWindow > 0 && _comboStep == 2) { DoAttack("jab3"); _comboStep = 0; }
            else { DoAttack("jab"); _comboStep = 1; }
            _comboWindow = 22;
        }

        private void DoComboChain()
        {
            if (_comboWindow > 0 && _comboStep == 1) { DoAttack("smash2"); _comboStep = 2; }
            else if (_comboWindow > 0 && _comboStep == 2) { DoAttack("smash3"); _comboStep = 0; }
            else { DoAttack("strong_side"); _comboStep = 1; }
            _comboWindow = 26;
        }

        /// <summary>
        /// Creates the attack box(es) for the named attack type with the canonical
        /// damage/knockback values ported from the JS source.
        /// </summary>
        public void DoAttack(string type)
        {
            float atkMul = AttackMultiplier();
            float dmg, kb;
            string hitType = "normal";
            int reach = 56;
            int height = (int)bodyHeight;
            int life = 4;

            switch (type)
            {
                case "jab":          dmg = 7f * atkMul;  kb = 4f * atkMul; reach = 50; break;
                case "jab2":         dmg = 8f * atkMul;  kb = 5f * atkMul; reach = 52; break;
                case "jab3":         dmg = 12f * atkMul; kb = 8f * atkMul; hitType = "smash"; reach = 60; break;
                case "smash":        dmg = 16f * atkMul; kb = 9f * atkMul; hitType = "smash"; reach = 66; break;
                case "smash2":       dmg = 11f * atkMul; kb = 7f * atkMul; hitType = "smash"; reach = 60; break;
                case "smash3":       dmg = 20f * atkMul; kb = 12f * atkMul; hitType = "smash"; reach = 74; break;
                case "down_smash":   dmg = 13f * atkMul; kb = 8f * atkMul; hitType = "smash"; reach = 70; height = 28; break;
                case "strong_side":  dmg = 10f * atkMul; kb = 6f * atkMul; reach = 64; break;
                case "dash_attack":  dmg = 12f * atkMul; kb = 7f * atkMul; reach = 70; break;
                default:             dmg = 7f * atkMul;  kb = 4f * atkMul; break;
            }

            // Smash trait bonuses.
            if (hitType == "smash")
            {
                if (HasTrait(TraitType.PowerSmash)) { dmg *= 1.25f; kb *= 1.2f; }
            }

            float by = type == "down_smash" ? GamePosition.y + bodyHeight - height : GamePosition.y + 8f;
            float bx = Facing > 0 ? GamePosition.x + bodyWidth : GamePosition.x - reach;
            if (type == "down_smash") bx = GamePosition.x - reach * 0.4f;
            float bw = type == "down_smash" ? bodyWidth + reach : reach;

            var box = new AttackBox(new Rect(bx, by, bw, height), dmg, kb, hitType, false, life, type);
            EmitBox(box);

            // DoubleSmash trait: a second delayed-equivalent box.
            if (hitType == "smash" && HasTrait(TraitType.DoubleSmash))
            {
                var box2 = new AttackBox(new Rect(bx, by, bw, height), dmg * 0.6f, kb * 0.6f, hitType, false, life, type + "_2");
                EmitBox(box2);
            }

            State = FighterState.Attack;
            _attackCooldown = AttackCooldownFor(type);
            AudioManager.Instance?.PlaySound(hitType == "smash" ? "smash" : "jab");
        }

        /// <summary>Triggers this fighter's signature special move.</summary>
        public void TriggerSpecial()
        {
            // Balance special applies a self buff instead of an attack.
            State = FighterState.Special;
            _special.Begin(_opponent);
            _attackCooldown = 40;
        }

        private void UseSkill(int slot)
        {
            int cd = slot == 0 ? _skillCooldown1 : _skillCooldown2;
            if (cd > 0) return;

            string id = slot == 0 ? skillSlot1Id : skillSlot2Id;
            var skill = SkillDatabase.GetById(id);
            if (skill == null) return;

            if (slot == 0) _skillCooldown1 = skill.cooldown;
            else _skillCooldown2 = skill.cooldown;

            // Self-only buff/heal skills.
            if (skill.selfOnly)
            {
                if (skill.heal > 0f) Damage = Mathf.Max(0f, Damage - skill.heal);
                if (skill.boost > 0f) ApplySkillBuff(skill.boost, 600);
                AudioManager.Instance?.PlaySound("shield");
                return;
            }

            float mul = HasTrait(TraitType.SkillBoost) ? 1.2f : 1f;
            float dmg = skill.ResolvedDamage * mul * AttackMultiplier();
            float kb = skill.smash * mul * AttackMultiplier();

            int reach = skill.range == "zone" ? 300 : 64;
            float bx = Facing > 0 ? GamePosition.x + bodyWidth : GamePosition.x - reach;
            float by = GamePosition.y + 6f;
            var box = new AttackBox(new Rect(bx, by, reach, bodyHeight), dmg, kb, "skill:" + skill.type, false, 6, "skill:" + skill.id);
            EmitBox(box);

            _attackCooldown = 24;
            State = FighterState.Attack;
            AudioManager.Instance?.PlaySound(skill.range == "zone" ? "shoot" : "smash");
        }

        /// <summary>Adds an active attack box to be resolved this frame window.</summary>
        public void EmitBox(AttackBox box) => _activeBoxes.Add(box);

        private void ResolveOutgoingHits()
        {
            if (_opponent == null) return;
            AttackSystem.Tick();

            for (int i = _activeBoxes.Count - 1; i >= 0; i--)
            {
                var box = _activeBoxes[i];

                if (AttackSystem.CheckHit(this, _opponent, box))
                {
                    _opponent.TakeHit(this, box.damage, box.kb, box.type);
                    OnDealtHit(box);

                    if (!box.piercing)
                    {
                        _activeBoxes.RemoveAt(i);
                        continue;
                    }
                }

                box.life--;
                if (box.life <= 0) _activeBoxes.RemoveAt(i);
                else _activeBoxes[i] = box;
            }
        }

        private void OnDealtHit(AttackBox box)
        {
            // Vampire trait: heal a fraction of damage dealt.
            if (HasTrait(TraitType.Vampire))
                Damage = Mathf.Max(0f, Damage - box.damage * 0.15f);

            // Explosive trait: splash effect on smashes.
            if (HasTrait(TraitType.Explosive) && box.type == "smash")
            {
                BattleManager.Instance?.SpawnEffect(_opponent.Center, new Color(1f, 0.6f, 0.2f), 70f, 0.3f);
                BattleManager.Instance?.Shake(0.2f, 8f);
            }
        }

        // ---------------------------------------------------------------------
        // Damage intake
        // ---------------------------------------------------------------------
        /// <summary>
        /// Applies an incoming hit: raises damage %, computes knockback via the
        /// canonical formula, and sets hitstun. Respects shield and invincibility.
        /// </summary>
        public void TakeHit(Fighter attacker, float dmg, float kb, string type)
        {
            if (IsDead || Invincible) return;

            // Shielding absorbs the hit (and drains shield health).
            if (_shielding && _shieldHealth > 0)
            {
                _shieldHealth = Mathf.Max(0, _shieldHealth - Mathf.RoundToInt(dmg * 2f));
                Velocity.x += (attacker.Facing) * kb * 0.2f;
                AudioManager.Instance?.PlaySound("shield");

                // Counter trait: reflect a slice of knockback back at the attacker.
                if (HasTrait(TraitType.Counter) && attacker != null)
                    attacker.TakeHit(this, dmg * 0.3f, kb * 0.4f, "counter");
                return;
            }

            float def = Definition.stats.def;
            if (HasTrait(TraitType.Tank)) def *= 1.25f;

            // Reckless trait takes extra damage.
            float taken = dmg;
            if (HasTrait(TraitType.Reckless)) taken *= 1.15f;
            Damage += taken;

            float weight = Definition.stats.weight;

            // Canonical knockback formula:
            //   KB = kb * (1/def) * (1/weight) * (1 + damage/80) * 0.85
            float knockback = kb * (1f / def) * (1f / weight) * (1f + Damage / 80f) * 0.85f;
            if (HasTrait(TraitType.Tank)) knockback *= 0.85f;

            // Attacker buff knockback multiplier.
            if (attacker != null) knockback *= attacker.BuffKbMul;

            // Horizontal/vertical launch. Angle ~ -0.5 rad (up & away).
            // Horizontal travel from KB modelled as 2.573 * KB^2 (gravity=0.6).
            int dir = attacker != null ? attacker.Facing : (Facing * -1);
            float angle = -0.5f; // radians, upward
            float launchX = Mathf.Cos(angle) * knockback;
            float launchY = Mathf.Sin(angle) * knockback; // negative => upward

            Velocity.x = dir * Mathf.Abs(launchX);
            Velocity.y = launchY;

            Hitstun = Mathf.RoundToInt(8f + knockback * 1.4f);
            State = FighterState.Hitstun;
            OnGround = false;

            // FX & feedback.
            BattleManager.Instance?.SpawnEffect(Center, new Color(1f, 0.85f, 0.3f), 40f + knockback, 0.25f);
            BattleManager.Instance?.Shake(0.15f + knockback * 0.01f, 6f + knockback);
            bool heavy = type == "smash" || (type != null && type.StartsWith("special"));
            AudioManager.Instance?.PlaySound(heavy ? "smash" : "jab");
        }

        /// <summary>
        /// Predicts horizontal travel distance for a given knockback magnitude using
        /// the original projectile model (gravity=0.6, launch angle=-0.5 rad).
        /// Exposed for AI edge-guarding and tuning.
        /// </summary>
        public static float PredictTravel(float knockback) => 2.573f * knockback * knockback;

        /// <summary>Applies a freeze (e.g. SnowCastle special).</summary>
        public void Freeze(int frames)
        {
            if (Invincible) return;
            FrozenTimer = Mathf.Max(FrozenTimer, frames);
        }

        // ---------------------------------------------------------------------
        // Buffs / traits
        // ---------------------------------------------------------------------
        /// <summary>Applies the Balance-style self buff for a number of frames.</summary>
        public void ApplyBuff(float atkMul, float kbMul, int frames)
        {
            BuffAtkMul = atkMul;
            BuffKbMul = kbMul;
            _buffTimer = frames;
            BattleManager.Instance?.SpawnEffect(Center, new Color(0.9f, 0.9f, 0.4f), 80f, 0.6f);
        }

        /// <summary>Applies a temporary skill-derived attack boost.</summary>
        public void ApplySkillBuff(float amount, int frames)
        {
            SkillBuff = amount;
            _skillBuffTimer = frames;
        }

        /// <summary>Total attack multiplier from stats, buffs, traits and skill buffs.</summary>
        public float AttackMultiplier()
        {
            float m = Definition.stats.atk * BuffAtkMul * (1f + SkillBuff);
            if (HasTrait(TraitType.Power)) m *= 1.12f;
            if (HasTrait(TraitType.Reckless)) m *= 1.1f;
            // Berserker: scales with own damage taken.
            if (HasTrait(TraitType.Berserker)) m *= 1f + Mathf.Min(0.5f, Damage / 200f);
            return m;
        }

        /// <summary>Returns true if this fighter has the given passive trait.</summary>
        public bool HasTrait(TraitType t) => Definition != null && Definition.HasTrait(t);

        private void PassiveTraits()
        {
            // Heal / Regenerate: slowly reduce damage %.
            if (HasTrait(TraitType.Heal) || HasTrait(TraitType.Regenerate))
            {
                float rate = HasTrait(TraitType.Heal) ? 0.04f : 0.02f;
                _regenAccumulator += rate;
                if (_regenAccumulator >= 1f)
                {
                    Damage = Mathf.Max(0f, Damage - 1f);
                    _regenAccumulator -= 1f;
                }
            }
        }

        private void TickTimers()
        {
            if (_invincibleTimer > 0) _invincibleTimer--;
            if (_attackCooldown > 0) _attackCooldown--;
            if (_comboWindow > 0) _comboWindow--; else _comboStep = 0;
            if (_skillCooldown1 > 0) _skillCooldown1--;
            if (_skillCooldown2 > 0) _skillCooldown2--;

            if (_buffTimer > 0)
            {
                _buffTimer--;
                if (_buffTimer == 0) { BuffAtkMul = 1f; BuffKbMul = 1f; }
            }
            if (_skillBuffTimer > 0)
            {
                _skillBuffTimer--;
                if (_skillBuffTimer == 0) SkillBuff = 0f;
            }
        }

        private int AttackCooldownFor(string type)
        {
            int baseCd;
            switch (type)
            {
                case "jab": case "jab2": baseCd = 10; break;
                case "jab3": baseCd = 16; break;
                case "smash": case "smash2": baseCd = 22; break;
                case "smash3": baseCd = 30; break;
                case "down_smash": baseCd = 26; break;
                case "dash_attack": baseCd = 24; break;
                default: baseCd = 18; break;
            }
            if (HasTrait(TraitType.Rapid)) baseCd = Mathf.RoundToInt(baseCd * 0.8f);
            return baseCd;
        }

        // ---------------------------------------------------------------------
        // CPU AI
        // ---------------------------------------------------------------------
        private FighterInput ComputeAIInput()
        {
            var input = FighterInput.None;
            if (_opponent == null) return input;

            if (_aiDecisionTimer > 0) _aiDecisionTimer--;

            float dx = _opponent.Center.x - Center.x;
            float dy = _opponent.Center.y - Center.y;
            float dist = Mathf.Abs(dx);
            int dir = dx >= 0 ? 1 : -1;

            // State transitions on a throttle so the AI doesn't twitch every frame.
            if (_aiDecisionTimer <= 0)
            {
                _aiDecisionTimer = Random.Range(8, 20);

                if (Hitstun > 0) _aiState = FighterState.Recover;
                else if (GamePosition.x < (BattleManager.Instance?.StageLeft ?? 200f) ||
                         GamePosition.x > (BattleManager.Instance?.StageRight ?? 700f))
                    _aiState = FighterState.Recover;
                else if (dist < 90f)
                    _aiState = Random.value < 0.7f ? FighterState.Attack : FighterState.Retreat;
                else if (dist < 320f)
                    _aiState = FighterState.Approach;
                else
                    _aiState = FighterState.Idle;

                // Occasionally throw out a special when close and off cooldown.
                if (dist < 200f && Random.value < 0.04f && _attackCooldown == 0)
                    _aiState = FighterState.Special;
            }

            switch (_aiState)
            {
                case FighterState.Approach:
                    if (dir > 0) input.right = true; else input.left = true;
                    if (dy < -40f && OnGround) input.up = true; // jump toward airborne foe
                    break;

                case FighterState.Attack:
                    if (dir > 0) input.right = (dist > 50f); else input.left = (dist > 50f);
                    // Pick an attack based on opponent damage %.
                    if (_opponent.Damage > 80f) { input.smash = true; }
                    else if (Random.value < 0.5f) { input.jab = true; }
                    else { input.smash = true; }
                    if (dy > 40f) input.down = true;
                    break;

                case FighterState.Retreat:
                    if (dir > 0) input.left = true; else input.right = true;
                    if (Random.value < 0.3f) input.shield = true;
                    break;

                case FighterState.Recover:
                    {
                        float midX = ((BattleManager.Instance?.StageLeft ?? 200f) +
                                      (BattleManager.Instance?.StageRight ?? 700f)) * 0.5f;
                        if (Center.x < midX) input.right = true; else input.left = true;
                        if (!OnGround) input.up = true;
                    }
                    break;

                case FighterState.Special:
                    input.jab = true; input.smash = true; input.down = true;
                    break;

                case FighterState.Idle:
                default:
                    break;
            }

            return input;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var bm = BattleManager.Instance;
            if (bm == null) return;
            Gizmos.color = Color.cyan;
            var r = GetBodyRect();
            Vector3 c = bm.GameToWorld(new Vector2(r.center.x, r.center.y));
            float s = 0.02f;
            Gizmos.DrawWireCube(c, new Vector3(r.width * s, r.height * s, 0.01f));
            foreach (var b in _activeBoxes)
                AttackSystem.DrawGizmo(b, bm.GameToWorld, Color.red);
        }
#endif
    }
}
