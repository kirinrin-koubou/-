using UnityEngine;

namespace SmashBattle
{
    /// <summary>
    /// Runs a fighter's signature special move. Specials are multi-phase, frame-driven
    /// state machines that emit attack boxes, effects and movement over their lifetime.
    /// Damage/knockback values are scaled by the owner's attack multiplier and mirror
    /// the JS source balance.
    /// </summary>
    public class SpecialMoveController : MonoBehaviour
    {
        private Fighter _owner;
        private SpecialType _type;

        private bool _active;
        private int _phase;
        private int _phaseTimer;
        private int _hitCounter;
        private Vector2 _targetPos;

        public bool Active => _active;

        /// <summary>Binds this controller to its owning fighter.</summary>
        public void Init(Fighter owner)
        {
            _owner = owner;
            if (_owner != null && _owner.Definition != null)
                _type = _owner.Definition.specialType;
        }

        /// <summary>Starts the special, targeting the given opponent.</summary>
        public void Begin(Fighter opponent)
        {
            if (_owner == null) return;
            if (_owner.Definition != null) _type = _owner.Definition.specialType;
            _active = true;
            _phase = 0;
            _phaseTimer = 0;
            _hitCounter = 0;
            if (opponent != null) _targetPos = opponent.Center;

            // Instant-resolution specials (buffs) finish immediately.
            if (_type == SpecialType.Balance)
            {
                DoBalance();
                _active = false;
            }
        }

        private float Atk => _owner != null ? _owner.AttackMultiplier() : 1f;

        /// <summary>Advances the active special by one frame, if any.</summary>
        public void UpdateSpecial(Fighter opp)
        {
            if (!_active || _owner == null) return;
            _phaseTimer++;

            switch (_type)
            {
                case SpecialType.Sakura:      Sakura(opp); break;
                case SpecialType.Thunder:     Thunder(opp); break;
                case SpecialType.Rain:        Rain(opp); break;
                case SpecialType.Solar:       Solar(opp); break;
                case SpecialType.Moon:        Moon(opp); break;
                case SpecialType.Jump:        JumpSpecial(opp); break;
                case SpecialType.Speed:
                case SpecialType.Swap:        SpeedSwap(opp); break;
                case SpecialType.Ranged:      Ranged(opp); break;
                case SpecialType.Combo:       Combo(opp); break;
                case SpecialType.Chain:       Chain(opp); break;
                case SpecialType.Grab:        Grab(opp); break;
                case SpecialType.Curse:       Curse(opp); break;
                case SpecialType.Heavy:       Heavy(opp); break;
                case SpecialType.SnowCastle:  SnowCastle(opp); break;
                case SpecialType.Chameleon:   Chameleon(opp); break;
                case SpecialType.GiantBomb:   GiantBomb(opp); break;
                case SpecialType.HeartBurst:  HeartBurst(opp); break;
                case SpecialType.TulipChain:  TulipChain(opp); break;
                case SpecialType.Demon:       Demon(opp); break;
                default:                      _active = false; break;
            }
        }

        private void End() => _active = false;

        private void Hit(Fighter opp, float dmg, float kb, string type = "special", bool piercing = false)
        {
            if (opp == null) return;
            float bx = opp.Center.x - 40f;
            float by = opp.Center.y - 40f;
            var box = new AttackBox(new Rect(bx, by, 80f, 80f), dmg, kb, type, piercing, piercing ? 6 : 3, "special:" + _type);
            if (AttackSystem.CheckHit(_owner, opp, box))
            {
                opp.TakeHit(_owner, dmg, kb, "special:" + _type);
                BattleManager.Instance?.SpawnEffect(opp.Center, _owner.Definition.color, 90f, 0.4f);
            }
        }

        private void Fx(Vector2 pos, Color c, float size, float life)
            => BattleManager.Instance?.SpawnEffect(pos, c, size, life);

        // ---------------------------------------------------------------------
        // Movement helper: rush toward a point.
        // ---------------------------------------------------------------------
        private bool RushTo(Vector2 target, float speed)
        {
            Vector2 to = target - _owner.Center;
            if (to.magnitude < speed) { _owner.GamePosition += to; return true; }
            _owner.GamePosition += to.normalized * speed;
            return false;
        }

        // ---------------------------------------------------------------------
        // Sakura: dash to opponent then slam.
        // ---------------------------------------------------------------------
        private void Sakura(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                Fx(_owner.Center, new Color(1f, 0.55f, 0.75f), 50f, 0.2f);
                if (RushTo(opp.Center, 28f)) { _phase = 1; _phaseTimer = 0; }
                if (_phaseTimer > 30) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 43f * Atk, 13f * Atk);
                Fx(opp.Center, new Color(1f, 0.4f, 0.7f), 120f, 0.5f);
                BattleManager.Instance?.Shake(0.3f, 14f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Thunder: cloud forms, then lightning strikes.
        // ---------------------------------------------------------------------
        private void Thunder(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                Fx(new Vector2(opp.Center.x, opp.Center.y - 160f), new Color(0.7f, 0.7f, 0.8f), 70f, 0.2f);
                if (_phaseTimer > 24) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 55f * Atk, 17f * Atk);
                Fx(opp.Center, new Color(1f, 1f, 0.4f), 130f, 0.4f);
                BattleManager.Instance?.Shake(0.35f, 16f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Rain: drizzle of drops then a final hit.
        // ---------------------------------------------------------------------
        private void Rain(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                if (_phaseTimer % 4 == 0)
                {
                    Vector2 p = new Vector2(opp.Center.x + Random.Range(-60f, 60f), opp.Center.y - Random.Range(60f, 160f));
                    Fx(p, new Color(0.5f, 0.7f, 1f), 24f, 0.3f);
                    Hit(opp, 1.5f * Atk, 1f, "special", true);
                }
                if (_phaseTimer > 60) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 24f * Atk, 10f * Atk);
                Fx(opp.Center, new Color(0.4f, 0.6f, 1f), 110f, 0.4f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Solar: piercing beam sweeps from a corner.
        // ---------------------------------------------------------------------
        private void Solar(Fighter opp)
        {
            if (_phase == 0)
            {
                Fx(new Vector2(0f, 0f), new Color(1f, 0.85f, 0.3f), 60f, 0.2f);
                if (_phaseTimer > 18) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                // Wide piercing beam across the stage.
                float reach = _owner.HasTrait(TraitType.Sniper) ? 900f : 700f;
                float bx = _owner.Facing > 0 ? _owner.Center.x : _owner.Center.x - reach;
                var box = new AttackBox(new Rect(bx, _owner.Center.y - 30f, reach, 60f), 65f * Atk, 0f, "special", true, 18, "special:solar");
                if (opp != null && AttackSystem.CheckHit(_owner, opp, box))
                {
                    opp.TakeHit(_owner, 65f * Atk, 0f, "special:solar");
                    Fx(opp.Center, new Color(1f, 0.9f, 0.4f), 100f, 0.3f);
                }
                Fx(new Vector2(_owner.Center.x + _owner.Facing * 200f, _owner.Center.y), new Color(1f, 0.9f, 0.5f), 140f, 0.3f);
                if (_phaseTimer > 18) End();
            }
        }

        // ---------------------------------------------------------------------
        // Moon: veil teleport-strike (treated like a strong special hit).
        // ---------------------------------------------------------------------
        private void Moon(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                Fx(_owner.Center, new Color(0.75f, 0.7f, 1f), 60f, 0.2f);
                if (RushTo(new Vector2(opp.Center.x - _owner.Facing * 40f, opp.Center.y), 30f) || _phaseTimer > 24)
                { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 40f * Atk, 14f * Atk);
                Fx(opp.Center, new Color(0.7f, 0.6f, 1f), 110f, 0.4f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Jump: fly above the opponent then crash down.
        // ---------------------------------------------------------------------
        private void JumpSpecial(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                Vector2 above = new Vector2(opp.Center.x, opp.Center.y - 180f);
                if (RushTo(above, 26f) || _phaseTimer > 30) { _phase = 1; _phaseTimer = 0; }
            }
            else if (_phase == 1)
            {
                // Crash straight down.
                _owner.GamePosition += new Vector2(0f, 30f);
                if (_owner.Center.y >= opp.Center.y || _phaseTimer > 20)
                {
                    Hit(opp, 24f * Atk, 13f * Atk);
                    Fx(opp.Center, new Color(0.7f, 1f, 0.7f), 110f, 0.4f);
                    BattleManager.Instance?.Shake(0.25f, 12f);
                    End();
                }
            }
        }

        // ---------------------------------------------------------------------
        // Speed / Swap: blink dash attack.
        // ---------------------------------------------------------------------
        private void SpeedSwap(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                Fx(_owner.Center, new Color(0.5f, 0.9f, 1f), 50f, 0.15f);
                if (RushTo(opp.Center, 40f) || _phaseTimer > 16) { _phase = 1; }
            }
            else
            {
                Hit(opp, 20f * Atk, 12f * Atk);
                Fx(opp.Center, new Color(0.6f, 1f, 1f), 90f, 0.3f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Ranged: fire a fast projectile-style hit toward the opponent.
        // ---------------------------------------------------------------------
        private void Ranged(Fighter opp)
        {
            if (opp == null) { End(); return; }
            float reach = _owner.HasTrait(TraitType.Sniper) ? 600f : 420f;
            float bx = _owner.Facing > 0 ? _owner.Center.x : _owner.Center.x - reach;
            var box = new AttackBox(new Rect(bx, _owner.Center.y - 20f, reach, 40f), 26f * Atk, 11f * Atk, "special", false, 10, "special:ranged");
            if (AttackSystem.CheckHit(_owner, opp, box))
            {
                opp.TakeHit(_owner, 26f * Atk, 11f * Atk, "special:ranged");
                Fx(opp.Center, new Color(0.6f, 0.9f, 0.5f), 90f, 0.3f);
            }
            Fx(new Vector2(_owner.Center.x + _owner.Facing * 120f, _owner.Center.y), new Color(0.7f, 1f, 0.6f), 50f, 0.2f);
            AudioManager.Instance?.PlaySound("shoot");
            if (_phaseTimer > 10) End();
        }

        // ---------------------------------------------------------------------
        // Combo: 10-hit flurry then a finisher.
        // ---------------------------------------------------------------------
        private void Combo(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                if (RushTo(opp.Center, 30f) || _phaseTimer > 20) { _phase = 1; _phaseTimer = 0; }
            }
            else if (_phase == 1)
            {
                if (_phaseTimer % 4 == 0)
                {
                    Hit(opp, 2.8f * Atk, 0.5f, "special", true);
                    Fx(opp.Center, new Color(1f, 0.7f, 0.4f), 40f, 0.15f);
                    _hitCounter++;
                }
                if (_hitCounter >= 10) { _phase = 2; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 9f * Atk, 6f * Atk);
                Fx(opp.Center, new Color(1f, 0.6f, 0.3f), 110f, 0.4f);
                BattleManager.Instance?.Shake(0.25f, 10f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Chain: extend a chain to the opponent, 5 hits, then a finisher.
        // ---------------------------------------------------------------------
        private void Chain(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                Fx(new Vector2((_owner.Center.x + opp.Center.x) * 0.5f, (_owner.Center.y + opp.Center.y) * 0.5f),
                    new Color(0.7f, 0.75f, 0.8f), 40f, 0.2f);
                if (_phaseTimer > 14) { _phase = 1; _phaseTimer = 0; }
            }
            else if (_phase == 1)
            {
                if (_phaseTimer % 6 == 0)
                {
                    Hit(opp, 7f * Atk, 2f, "special", true);
                    Fx(opp.Center, new Color(0.75f, 0.8f, 0.85f), 50f, 0.2f);
                    _hitCounter++;
                }
                if (_hitCounter >= 5) { _phase = 2; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 21f * Atk, 11f * Atk);
                Fx(opp.Center, new Color(0.8f, 0.85f, 0.9f), 110f, 0.4f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Balance: self buff (instant, applied in Begin()).
        // ---------------------------------------------------------------------
        private void DoBalance()
        {
            _owner.ApplyBuff(1.1f, 1.05f, 600);
            Fx(_owner.Center, new Color(0.9f, 0.9f, 0.4f), 100f, 0.6f);
        }

        // ---------------------------------------------------------------------
        // Grab: dash grab then throw.
        // ---------------------------------------------------------------------
        private void Grab(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                if (RushTo(opp.Center, 24f) || _phaseTimer > 24) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                // Throw away from the grabber.
                Hit(opp, 20f * Atk, 20f * Atk);
                Fx(opp.Center, new Color(0.85f, 0.6f, 0.4f), 100f, 0.4f);
                BattleManager.Instance?.Shake(0.25f, 12f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Curse: heavy hit that also costs the caster 10 damage.
        // ---------------------------------------------------------------------
        private void Curse(Fighter opp)
        {
            if (opp == null) { End(); return; }
            Hit(opp, 30f * Atk, 13f * Atk);
            Fx(opp.Center, new Color(0.55f, 0.4f, 0.7f), 110f, 0.4f);
            // Self damage recoil.
            _owner.TakeHit(_owner, 10f, 0f, "self");
            End();
        }

        // ---------------------------------------------------------------------
        // Heavy: two rock walls slam together to crush the opponent.
        // ---------------------------------------------------------------------
        private void Heavy(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                // Walls hit on the way in.
                Hit(opp, 9f * Atk, 3f, "special", true);
                Fx(new Vector2(opp.Center.x - 80f, opp.Center.y), new Color(0.6f, 0.6f, 0.6f), 60f, 0.2f);
                Fx(new Vector2(opp.Center.x + 80f, opp.Center.y), new Color(0.6f, 0.6f, 0.6f), 60f, 0.2f);
                if (_phaseTimer > 26) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 26f * Atk, 14f * Atk);
                Fx(opp.Center, new Color(0.7f, 0.7f, 0.7f), 130f, 0.5f);
                BattleManager.Instance?.Shake(0.4f, 18f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // SnowCastle: a statue drops, freezing and hitting the opponent.
        // ---------------------------------------------------------------------
        private void SnowCastle(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                Fx(new Vector2(opp.Center.x, opp.Center.y - 200f + _phaseTimer * 8f), new Color(0.8f, 0.9f, 1f), 70f, 0.15f);
                if (_phaseTimer > 24) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 30f * Atk, 15f * Atk);
                opp.Freeze(90);
                Fx(opp.Center, new Color(0.8f, 0.95f, 1f), 120f, 0.5f);
                BattleManager.Instance?.Shake(0.3f, 14f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Chameleon: 12 radial projectiles around the caster.
        // ---------------------------------------------------------------------
        private void Chameleon(Fighter opp)
        {
            if (opp == null) { End(); return; }
            const int count = 12;
            for (int i = 0; i < count; i++)
            {
                float ang = (Mathf.PI * 2f / count) * i;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 p = _owner.Center + dir * (40f + _phaseTimer * 12f);
                Fx(p, new Color(0.5f, 0.85f, 0.55f), 26f, 0.2f);
                var box = new AttackBox(new Rect(p.x - 18f, p.y - 18f, 36f, 36f), 7f * Atk, 4f * Atk, "special", true, 2, "special:chameleon" + i);
                if (AttackSystem.CheckHit(_owner, opp, box))
                    opp.TakeHit(_owner, 7f * Atk, 4f * Atk, "special:chameleon");
            }
            if (_phaseTimer > 16) End();
        }

        // ---------------------------------------------------------------------
        // GiantBomb: a bomb drops, then a wide shockwave.
        // ---------------------------------------------------------------------
        private void GiantBomb(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                Fx(new Vector2(opp.Center.x, opp.Center.y - 220f + _phaseTimer * 10f), new Color(0.3f, 0.3f, 0.3f), 60f, 0.15f);
                if (_phaseTimer > 24) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                // Shockwave around the landing point.
                var box = new AttackBox(new Rect(opp.Center.x - 160f, opp.Center.y - 60f, 320f, 120f), 18f * Atk, 12f * Atk, "special", false, 4, "special:bomb");
                if (AttackSystem.CheckHit(_owner, opp, box))
                    opp.TakeHit(_owner, 18f * Atk, 12f * Atk, "special:bomb");
                Fx(opp.Center, new Color(1f, 0.5f, 0.2f), 160f, 0.5f);
                BattleManager.Instance?.Shake(0.4f, 18f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // HeartBurst: a homing arrow tracks the opponent, then explodes.
        // ---------------------------------------------------------------------
        private void HeartBurst(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                // Arrow homes toward target.
                _targetPos = Vector2.Lerp(_targetPos, opp.Center, 0.25f);
                Fx(_targetPos, new Color(1f, 0.5f, 0.7f), 30f, 0.15f);
                var box = new AttackBox(new Rect(_targetPos.x - 20f, _targetPos.y - 20f, 40f, 40f), 13f * Atk, 4f * Atk, "special", true, 2, "special:heartarrow");
                if (AttackSystem.CheckHit(_owner, opp, box))
                {
                    opp.TakeHit(_owner, 13f * Atk, 4f * Atk, "special:heart");
                    _phase = 1; _phaseTimer = 0;
                }
                if (_phaseTimer > 50) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 21f * Atk, 13f * Atk);
                Fx(opp.Center, new Color(1f, 0.4f, 0.6f), 130f, 0.5f);
                BattleManager.Instance?.Shake(0.3f, 14f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // TulipChain: 5-hit ribbon chain then a launching finisher.
        // ---------------------------------------------------------------------
        private void TulipChain(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                if (_phaseTimer % 5 == 0)
                {
                    Hit(opp, 5f * Atk, 1.5f, "special", true);
                    Fx(opp.Center, new Color(1f, 0.6f, 0.8f), 44f, 0.2f);
                    _hitCounter++;
                }
                if (_hitCounter >= 5) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 14f * Atk, 16f * Atk);
                Fx(opp.Center, new Color(1f, 0.5f, 0.75f), 120f, 0.45f);
                End();
            }
        }

        // ---------------------------------------------------------------------
        // Demon: berserk rush with a devastating finisher.
        // ---------------------------------------------------------------------
        private void Demon(Fighter opp)
        {
            if (opp == null) { End(); return; }
            if (_phase == 0)
            {
                if (RushTo(opp.Center, 30f) || _phaseTimer > 22) { _phase = 1; _phaseTimer = 0; }
            }
            else
            {
                Hit(opp, 48f * Atk, 16f * Atk);
                Fx(opp.Center, new Color(0.8f, 0.2f, 0.2f), 140f, 0.5f);
                BattleManager.Instance?.Shake(0.4f, 18f);
                // Vampiric: heal a portion.
                _owner.ApplySkillBuff(0f, 0);
                End();
            }
        }
    }
}
