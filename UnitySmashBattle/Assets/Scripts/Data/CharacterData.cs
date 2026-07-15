using System.Collections.Generic;
using UnityEngine;

namespace SmashBattle
{
    /// <summary>
    /// Core numeric stats for a fighter. Defaults mirror the original JS balance.
    /// </summary>
    [System.Serializable]
    public struct CharacterStats
    {
        public float hp;      // Reference HP value (display / scaling only; combat uses damage %)
        public float atk;     // Attack multiplier
        public float def;     // Defense multiplier (knockback divisor)
        public float spd;     // Movement speed
        public float weight;  // Weight (knockback divisor; heavier = harder to launch)
        public float jump;    // Jump velocity

        public CharacterStats(float hp, float atk, float def, float spd, float weight, float jump)
        {
            this.hp = hp;
            this.atk = atk;
            this.def = def;
            this.spd = spd;
            this.weight = weight;
            this.jump = jump;
        }

        /// <summary>Returns the default baseline stat block.</summary>
        public static CharacterStats Default => new CharacterStats(100f, 1.0f, 1.0f, 5.0f, 1.0f, 14.0f);
    }

    /// <summary>
    /// Identifies the unique special move (Z+X+down or X+C equipped) for a character.
    /// </summary>
    public enum SpecialType
    {
        Sakura,
        Thunder,
        Rain,
        Solar,
        Moon,
        Jump,
        Speed,
        Swap,
        Ranged,
        Combo,
        Chain,
        Balance,
        SnowCastle,
        HeartBurst,
        TulipChain,
        GiantBomb,
        Chameleon,
        Grab,
        Curse,
        Heavy,
        Demon
    }

    /// <summary>
    /// Passive trait abilities. Multiple traits may be stacked on a character.
    /// </summary>
    public enum TraitType
    {
        None,
        Rapid,        // Faster attack rate
        Power,        // Bonus damage
        Guard,        // Stronger shield
        Heal,         // Heals (reduces %) over time
        Berserker,    // More power at high damage
        Counter,      // Reflects part of incoming knockback
        Reflect,      // Reflects projectiles
        Vampire,      // Heals on hit
        Regenerate,   // Slow passive regen
        Explosive,    // Extra splash on smashes
        PowerSmash,   // Smash attacks deal more
        DoubleSmash,  // Smashes hit twice
        SkillBoost,   // Skill moves deal more
        Elemental,    // Bonus elemental damage
        Nature,       // Nature-type bonus
        LateBlocker,  // Block window holds longer
        Aerial,       // Stronger / extra air actions
        Reckless,     // More damage dealt and taken
        Tank,         // Reduced knockback taken
        Lightweight,  // Extra jumps, faster fall recovery
        Sniper        // Ranged specials reach further
    }

    /// <summary>
    /// Seasonal / signature flourish abilities that augment a character's kit.
    /// </summary>
    public enum SeasonalAbility
    {
        None,
        SakuraBurst,
        ElectricDash,
        SnowCastle,
        HeartBurst,
        TulipChain,
        SolarFlare,
        MoonVeil,
        AutumnGale,
        WinterFreeze
    }

    /// <summary>
    /// Full immutable definition of a playable character.
    /// </summary>
    [System.Serializable]
    public class CharacterDefinition
    {
        public string id;
        public string name;
        public string emoji;
        public Color color;
        public CharacterStats stats;
        public SpecialType specialType;
        public List<TraitType> traits;
        public SeasonalAbility seasonalAbility;

        public CharacterDefinition(
            string id,
            string name,
            string emoji,
            Color color,
            CharacterStats stats,
            SpecialType specialType,
            List<TraitType> traits,
            SeasonalAbility seasonalAbility = SeasonalAbility.None)
        {
            this.id = id;
            this.name = name;
            this.emoji = emoji;
            this.color = color;
            this.stats = stats;
            this.specialType = specialType;
            this.traits = traits ?? new List<TraitType>();
            this.seasonalAbility = seasonalAbility;
        }

        /// <summary>Returns true if this character has the given trait.</summary>
        public bool HasTrait(TraitType t) => traits != null && traits.Contains(t);
    }

    /// <summary>
    /// Static registry of every playable character.
    /// </summary>
    public static class CharacterDatabase
    {
        private static List<CharacterDefinition> _all;

        /// <summary>All characters available in the roster (20+).</summary>
        public static List<CharacterDefinition> AllCharacters
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        /// <summary>Look up a character definition by its string id.</summary>
        public static CharacterDefinition GetById(string id)
        {
            foreach (var c in AllCharacters)
                if (c.id == id) return c;
            return AllCharacters[0];
        }

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.white;
        }

        private static List<TraitType> T(params TraitType[] ts) => new List<TraitType>(ts);

        private static void Build()
        {
            _all = new List<CharacterDefinition>
            {
                // --- Light, fast characters (weight 0.7-0.8) ---
                new CharacterDefinition("sakura", "Sakura", "\U0001F338", Hex("#ff8ab3"),
                    new CharacterStats(100, 1.05f, 0.9f, 6.2f, 0.75f, 16.0f),
                    SpecialType.Sakura, T(TraitType.Rapid, TraitType.Nature), SeasonalAbility.SakuraBurst),

                new CharacterDefinition("thunder", "Thunder", "⚡", Hex("#ffe066"),
                    new CharacterStats(100, 1.1f, 0.85f, 6.5f, 0.72f, 16.5f),
                    SpecialType.Thunder, T(TraitType.Rapid, TraitType.Elemental), SeasonalAbility.ElectricDash),

                new CharacterDefinition("swift", "Swift", "\U0001F4A8", Hex("#8ad7ff"),
                    new CharacterStats(100, 0.95f, 0.85f, 7.0f, 0.7f, 17.0f),
                    SpecialType.Speed, T(TraitType.Rapid, TraitType.Aerial, TraitType.Lightweight)),

                new CharacterDefinition("breeze", "Breeze", "\U0001F343", Hex("#9ee6a0"),
                    new CharacterStats(100, 0.92f, 0.88f, 6.8f, 0.74f, 16.8f),
                    SpecialType.Chameleon, T(TraitType.Aerial, TraitType.Reflect)),

                new CharacterDefinition("heart", "Heart", "\U0001F496", Hex("#ff9ecf"),
                    new CharacterStats(100, 1.0f, 0.9f, 6.0f, 0.78f, 15.8f),
                    SpecialType.HeartBurst, T(TraitType.Heal, TraitType.Vampire), SeasonalAbility.HeartBurst),

                new CharacterDefinition("tulip", "Tulip", "\U0001F337", Hex("#ff7fae"),
                    new CharacterStats(100, 0.98f, 0.9f, 6.1f, 0.78f, 15.5f),
                    SpecialType.TulipChain, T(TraitType.Nature, TraitType.Rapid), SeasonalAbility.TulipChain),

                // --- Balanced characters (weight ~1.0) ---
                new CharacterDefinition("rain", "Rain", "\U0001F327", Hex("#7fb4ff"),
                    new CharacterStats(100, 1.0f, 1.0f, 5.0f, 1.0f, 14.0f),
                    SpecialType.Rain, T(TraitType.Elemental, TraitType.Counter)),

                new CharacterDefinition("solar", "Solar", "☀", Hex("#ffc24d"),
                    new CharacterStats(100, 1.05f, 1.0f, 5.0f, 1.0f, 14.0f),
                    SpecialType.Solar, T(TraitType.Power, TraitType.Sniper), SeasonalAbility.SolarFlare),

                new CharacterDefinition("moon", "Moon", "\U0001F319", Hex("#c3b6ff"),
                    new CharacterStats(100, 1.0f, 1.05f, 5.2f, 1.0f, 14.2f),
                    SpecialType.Moon, T(TraitType.Counter, TraitType.LateBlocker), SeasonalAbility.MoonVeil),

                new CharacterDefinition("combo", "Combo", "\U0001F94B", Hex("#ff9b54"),
                    new CharacterStats(100, 1.0f, 1.0f, 5.4f, 0.95f, 14.5f),
                    SpecialType.Combo, T(TraitType.Rapid, TraitType.Reckless)),

                new CharacterDefinition("chain", "Chain", "⛓", Hex("#b0b8c4"),
                    new CharacterStats(100, 1.02f, 1.05f, 5.0f, 1.05f, 13.8f),
                    SpecialType.Chain, T(TraitType.Power, TraitType.Guard)),

                new CharacterDefinition("balance", "Balance", "⚖", Hex("#cfcf8a"),
                    new CharacterStats(100, 1.0f, 1.0f, 5.0f, 1.0f, 14.0f),
                    SpecialType.Balance, T(TraitType.Regenerate, TraitType.Guard)),

                new CharacterDefinition("swap", "Swap", "\U0001F504", Hex("#7fe0d4"),
                    new CharacterStats(100, 0.98f, 1.0f, 5.6f, 0.95f, 14.6f),
                    SpecialType.Swap, T(TraitType.Counter, TraitType.SkillBoost)),

                new CharacterDefinition("ranged", "Ranged", "\U0001F3F9", Hex("#9ad17f"),
                    new CharacterStats(100, 1.0f, 0.95f, 5.2f, 0.92f, 14.4f),
                    SpecialType.Ranged, T(TraitType.Sniper, TraitType.Reflect)),

                new CharacterDefinition("chameleon", "Chameleon", "\U0001F98E", Hex("#86d98a"),
                    new CharacterStats(100, 1.0f, 1.0f, 5.3f, 0.98f, 14.3f),
                    SpecialType.Chameleon, T(TraitType.Reflect, TraitType.Elemental)),

                new CharacterDefinition("grab", "Grappler", "\U0001F91A", Hex("#d39a6a"),
                    new CharacterStats(100, 1.05f, 1.05f, 5.0f, 1.1f, 13.5f),
                    SpecialType.Grab, T(TraitType.Power, TraitType.Tank)),

                // --- Heavy characters (weight 1.3-1.5) ---
                new CharacterDefinition("heavy", "Heavy", "\U0001FAA8", Hex("#9a9a9a"),
                    new CharacterStats(100, 1.25f, 1.25f, 3.6f, 1.5f, 11.5f),
                    SpecialType.Heavy, T(TraitType.Power, TraitType.Tank, TraitType.PowerSmash)),

                new CharacterDefinition("giantbomb", "Giant Bomb", "\U0001F4A3", Hex("#5a5a5a"),
                    new CharacterStats(100, 1.3f, 1.2f, 3.8f, 1.45f, 11.8f),
                    SpecialType.GiantBomb, T(TraitType.Explosive, TraitType.PowerSmash), SeasonalAbility.WinterFreeze),

                new CharacterDefinition("snowcastle", "Snow Castle", "☃", Hex("#cfe9ff"),
                    new CharacterStats(100, 1.2f, 1.3f, 3.5f, 1.45f, 11.0f),
                    SpecialType.SnowCastle, T(TraitType.Tank, TraitType.Guard), SeasonalAbility.SnowCastle),

                new CharacterDefinition("curse", "Curse", "\U0001F480", Hex("#8a6bb0"),
                    new CharacterStats(100, 1.35f, 1.1f, 4.0f, 1.3f, 12.0f),
                    SpecialType.Curse, T(TraitType.Berserker, TraitType.Reckless)),

                new CharacterDefinition("demon", "Demon", "\U0001F608", Hex("#c0392b"),
                    new CharacterStats(100, 1.4f, 1.15f, 4.2f, 1.35f, 12.2f),
                    SpecialType.Demon, T(TraitType.Berserker, TraitType.Power, TraitType.Vampire)),

                new CharacterDefinition("jumper", "Jumper", "\U0001F998", Hex("#a8e6a0"),
                    new CharacterStats(100, 1.0f, 1.0f, 5.0f, 0.85f, 18.0f),
                    SpecialType.Jump, T(TraitType.Aerial, TraitType.Lightweight)),

                new CharacterDefinition("doublesmash", "Twin", "⚔", Hex("#d4a5e8"),
                    new CharacterStats(100, 1.08f, 1.0f, 5.0f, 1.05f, 13.9f),
                    SpecialType.Combo, T(TraitType.DoubleSmash, TraitType.PowerSmash)),
            };
        }
    }
}
