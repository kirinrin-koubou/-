using System.Collections.Generic;

namespace SmashBattle
{
    /// <summary>
    /// A single equippable skill move (X+C equipped, triggered via V/B slots).
    /// </summary>
    [System.Serializable]
    public class SkillMoveData
    {
        public string id;
        public string name;
        public string emoji;
        public float power;     // Base damage of the hit
        public float smash;     // Knockback magnitude
        public string range;    // "close" | "zone"
        public string type;     // "fight" | "fire" | "steel" | "dark" | "water" | "nature" | "wing" | "fairy"
        public float dmgMul;    // Extra per-move multiplier
        public float heal;      // Self heal (reduces own %) applied on use
        public float debuff;    // Damage-over-time / weaken applied to target
        public float boost;     // Self attack boost applied on use
        public bool selfOnly;   // If true the move targets self (buff/heal), no hitbox
        public int cooldown;    // Cooldown in frames

        public SkillMoveData(string id, string name, string emoji, float power, float smash,
            string range, string type, float dmgMul = 1f, float heal = 0f, float debuff = 0f,
            float boost = 0f, bool selfOnly = false, int cooldown = 180)
        {
            this.id = id;
            this.name = name;
            this.emoji = emoji;
            this.power = power;
            this.smash = smash;
            this.range = range;
            this.type = type;
            this.dmgMul = dmgMul;
            this.heal = heal;
            this.debuff = debuff;
            this.boost = boost;
            this.selfOnly = selfOnly;
            this.cooldown = cooldown;
        }

        /// <summary>Final damage including the move's own multiplier and its type multiplier.</summary>
        public float ResolvedDamage =>
            power * dmgMul * SkillDatabase.TypeMultiplier(type);
    }

    /// <summary>
    /// Static registry of all skill moves and skill type multipliers.
    /// </summary>
    public static class SkillDatabase
    {
        /// <summary>
        /// Per-type damage multipliers applied to every skill of that type.
        /// </summary>
        public static readonly Dictionary<string, float> SKILL_TYPES = new Dictionary<string, float>
        {
            { "fight",  1.7f },
            { "steel",  1.6f },
            { "fire",   1.5f },
            { "dark",   1.5f },
            { "water",  1.3f },
            { "nature", 1.3f },
            { "wing",   1.3f },
            { "fairy",  1.2f },
        };

        /// <summary>Returns the type multiplier (defaults to 1.0 for unknown types).</summary>
        public static float TypeMultiplier(string type)
        {
            if (type != null && SKILL_TYPES.TryGetValue(type, out var m)) return m;
            return 1.0f;
        }

        private static List<SkillMoveData> _all;

        /// <summary>All available skill moves (~30).</summary>
        public static List<SkillMoveData> AllSkills
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        /// <summary>Find a skill by id; returns null if not found.</summary>
        public static SkillMoveData GetById(string id)
        {
            foreach (var s in AllSkills)
                if (s.id == id) return s;
            return null;
        }

        private static void Build()
        {
            _all = new List<SkillMoveData>
            {
                // Fight type (close-range physical)
                new SkillMoveData("ironfist", "Iron Fist", "\U0001F44A", 14f, 9f, "close", "fight", 1.0f, cooldown: 150),
                new SkillMoveData("uppercut", "Uppercut", "\U0001F94A", 16f, 12f, "close", "fight", 1.1f, cooldown: 200),
                new SkillMoveData("dropkick", "Drop Kick", "\U0001F45F", 13f, 8f, "close", "fight", 1.0f, cooldown: 160),
                new SkillMoveData("suplex", "Suplex", "\U0001F938", 18f, 14f, "close", "fight", 1.2f, cooldown: 240),

                // Steel type (heavy hits)
                new SkillMoveData("hammer", "War Hammer", "\U0001F528", 20f, 15f, "close", "steel", 1.1f, cooldown: 260),
                new SkillMoveData("buzzsaw", "Buzz Saw", "\U0001FA9A", 12f, 6f, "close", "steel", 1.0f, cooldown: 140),
                new SkillMoveData("anchor", "Anchor Toss", "⚓", 17f, 13f, "zone", "steel", 1.05f, cooldown: 220),
                new SkillMoveData("railgun", "Rail Gun", "\U0001F52B", 15f, 10f, "zone", "steel", 1.1f, cooldown: 210),

                // Fire type
                new SkillMoveData("fireball", "Fireball", "\U0001F525", 13f, 8f, "zone", "fire", 1.0f, cooldown: 150),
                new SkillMoveData("flameburst", "Flame Burst", "\U0001F30B", 16f, 11f, "zone", "fire", 1.1f, cooldown: 200),
                new SkillMoveData("meteor", "Meteor", "☄", 22f, 16f, "zone", "fire", 1.3f, cooldown: 300),
                new SkillMoveData("emberdash", "Ember Dash", "\U0001F684", 12f, 7f, "close", "fire", 1.0f, cooldown: 150),

                // Dark type
                new SkillMoveData("shadowclaw", "Shadow Claw", "\U0001F43E", 14f, 9f, "close", "dark", 1.0f, debuff: 4f, cooldown: 180),
                new SkillMoveData("voidblast", "Void Blast", "\U0001F311", 17f, 12f, "zone", "dark", 1.1f, cooldown: 220),
                new SkillMoveData("curseskill", "Hex", "\U0001F52E", 11f, 6f, "zone", "dark", 1.0f, debuff: 8f, cooldown: 200),
                new SkillMoveData("nightmare", "Nightmare", "\U0001F47B", 19f, 13f, "zone", "dark", 1.2f, cooldown: 280),

                // Water type
                new SkillMoveData("watergun", "Water Jet", "\U0001F4A6", 11f, 7f, "zone", "water", 1.0f, cooldown: 140),
                new SkillMoveData("tidalwave", "Tidal Wave", "\U0001F30A", 16f, 12f, "zone", "water", 1.1f, cooldown: 240),
                new SkillMoveData("bubble", "Bubble Trap", "\U0001FAE7", 9f, 5f, "zone", "water", 1.0f, debuff: 3f, cooldown: 160),
                new SkillMoveData("frostbite", "Frost Bite", "❄", 13f, 8f, "close", "water", 1.05f, debuff: 5f, cooldown: 190),

                // Nature type
                new SkillMoveData("vinewhip", "Vine Whip", "\U0001F33F", 12f, 7f, "close", "nature", 1.0f, cooldown: 150),
                new SkillMoveData("leafstorm", "Leaf Storm", "\U0001F343", 15f, 10f, "zone", "nature", 1.1f, cooldown: 210),
                new SkillMoveData("thornarmor", "Thorn Armor", "\U0001F33B", 0f, 0f, "close", "nature", 1.0f, boost: 0.15f, selfOnly: true, cooldown: 360),
                new SkillMoveData("photosynth", "Photosynthesis", "\U0001F33E", 0f, 0f, "close", "nature", 1.0f, heal: 18f, selfOnly: true, cooldown: 420),

                // Wing type
                new SkillMoveData("aircutter", "Air Cutter", "\U0001F4A8", 12f, 8f, "zone", "wing", 1.0f, cooldown: 150),
                new SkillMoveData("dive", "Sky Dive", "\U0001F985", 16f, 11f, "close", "wing", 1.1f, cooldown: 220),
                new SkillMoveData("tornado", "Tornado", "\U0001F32A", 14f, 10f, "zone", "wing", 1.05f, cooldown: 200),

                // Fairy type
                new SkillMoveData("sparkle", "Sparkle", "✨", 11f, 6f, "zone", "fairy", 1.0f, cooldown: 140),
                new SkillMoveData("blessing", "Blessing", "\U0001F9DA", 0f, 0f, "close", "fairy", 1.0f, heal: 22f, boost: 0.1f, selfOnly: true, cooldown: 480),
                new SkillMoveData("dazzle", "Dazzle Beam", "\U0001F31F", 15f, 9f, "zone", "fairy", 1.1f, debuff: 4f, cooldown: 230),
            };
        }
    }
}
