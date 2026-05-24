using System.Collections.Generic;
using UnityEngine;

namespace SmashBattle
{
    /// <summary>
    /// A transient hitbox produced by an attack. Uses an axis-aligned Rect in
    /// game-space (the same 900x600 coordinate space used by the original JS game).
    /// </summary>
    [System.Serializable]
    public struct AttackBox
    {
        public Rect rect;       // Hit area in game units
        public float damage;    // Percent damage applied on hit
        public float kb;        // Knockback magnitude (pre-formula base)
        public string type;     // "normal" | "smash" | "special" | skill type etc.
        public bool piercing;   // If true, the box can hit through (multi-hit, no removal)
        public int life;        // Remaining frames the box is active
        public string tag;      // Optional source identifier (e.g. "jab", "special:thunder")

        public AttackBox(Rect rect, float damage, float kb, string type, bool piercing = false, int life = 3, string tag = "")
        {
            this.rect = rect;
            this.damage = damage;
            this.kb = kb;
            this.type = type;
            this.piercing = piercing;
            this.life = life;
            this.tag = tag;
        }
    }

    /// <summary>
    /// Static helpers for AABB hit detection and multi-hit cooldown tracking.
    /// </summary>
    public static class AttackSystem
    {
        // Tracks (attackerId -> (boxTag -> framesUntilCanHitAgain)) for piercing boxes.
        private static readonly Dictionary<int, Dictionary<string, int>> _multiHitCooldown =
            new Dictionary<int, Dictionary<string, int>>();

        /// <summary>
        /// Tests whether <paramref name="box"/> overlaps the defender's body rect.
        /// </summary>
        public static bool CheckHit(AttackBox box, Rect defenderBody)
        {
            return box.rect.Overlaps(defenderBody);
        }

        /// <summary>
        /// Full hit resolution between two fighters for a single attack box.
        /// Returns true if the hit landed (and was applied).
        /// </summary>
        public static bool CheckHit(Fighter attacker, Fighter defender, AttackBox box)
        {
            if (attacker == null || defender == null) return false;
            if (defender.IsDead) return false;
            if (!box.rect.Overlaps(defender.GetBodyRect())) return false;

            // Multi-hit boxes respect a per-box cooldown so they do not hit every frame.
            if (box.piercing)
            {
                int id = attacker.GetInstanceID();
                if (!_multiHitCooldown.TryGetValue(id, out var map))
                {
                    map = new Dictionary<string, int>();
                    _multiHitCooldown[id] = map;
                }
                string key = box.tag + "->" + defender.GetInstanceID();
                if (map.TryGetValue(key, out int cd) && cd > 0)
                    return false;
                map[key] = 8; // 8-frame multi-hit interval
            }

            return true;
        }

        /// <summary>Decrements all multi-hit cooldowns. Call once per fixed step.</summary>
        public static void Tick()
        {
            foreach (var kv in _multiHitCooldown)
            {
                var map = kv.Value;
                var keys = new List<string>(map.Keys);
                foreach (var k in keys)
                {
                    if (map[k] > 0) map[k]--;
                }
            }
        }

        /// <summary>Clears all tracked cooldowns (e.g. on round reset).</summary>
        public static void Reset() => _multiHitCooldown.Clear();

#if UNITY_EDITOR
        /// <summary>
        /// Draws an attack box in the editor for debugging. Game units are assumed
        /// to map 1:1 onto world units via <paramref name="originToWorld"/>.
        /// </summary>
        public static void DrawGizmo(AttackBox box, System.Func<Vector2, Vector3> originToWorld, Color color)
        {
            Gizmos.color = color;
            Vector3 bl = originToWorld(new Vector2(box.rect.xMin, box.rect.yMin));
            Vector3 br = originToWorld(new Vector2(box.rect.xMax, box.rect.yMin));
            Vector3 tl = originToWorld(new Vector2(box.rect.xMin, box.rect.yMax));
            Vector3 tr = originToWorld(new Vector2(box.rect.xMax, box.rect.yMax));
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
#endif
    }
}
