using System.Collections.Generic;
using UnityEngine;

namespace SmashBattle
{
    /// <summary>
    /// Holds the blast-zone bounds in game-space (matching the original 900x600 canvas).
    /// </summary>
    [System.Serializable]
    public struct BlastZone
    {
        public float left;
        public float right;
        public float top;
        public float bottom;

        /// <summary>Default bounds: left=-100, right=1000, top=-150, bottom=700.</summary>
        public static BlastZone Default => new BlastZone
        {
            left = -100f,
            right = 1000f,
            top = -150f,
            bottom = 700f
        };

        /// <summary>Returns true if the position is outside the blast bounds.</summary>
        public bool IsOutside(Vector2 pos)
        {
            return pos.x < left || pos.x > right || pos.y < top || pos.y > bottom;
        }
    }

    /// <summary>
    /// A short-lived visual effect spawned by combat events.
    /// </summary>
    public class BattleEffect
    {
        public Vector2 pos;
        public Color color;
        public float size;
        public float life;
        public float maxLife;
        public Vector2 vel;
    }

    /// <summary>
    /// Central battle controller: tracks fighters, stocks, the timer, blast zones,
    /// camera shake, and transient effects.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Header("Fighters")]
        [SerializeField] private Fighter player;
        [SerializeField] private Fighter cpu;

        [Header("Stage")]
        [SerializeField] private float groundY = 480f;       // Game-space ground height
        [SerializeField] private float stageLeft = 200f;     // Visible platform edges (game units)
        [SerializeField] private float stageRight = 700f;
        [SerializeField] private Camera battleCamera;

        [Header("Match Settings")]
        [SerializeField] private int startingStocks = 3;
        [SerializeField] private float matchTimeSeconds = 99f;

        [Header("World Mapping")]
        [Tooltip("World units per game unit. Game space is 900x600.")]
        [SerializeField] private float worldUnitsPerGameUnit = 0.02f;

        public BlastZone Blast { get; private set; } = BlastZone.Default;
        public float GroundY => groundY;
        public float StageLeft => stageLeft;
        public float StageRight => stageRight;
        public Fighter Player => player;
        public Fighter Cpu => cpu;
        public bool BattleActive { get; private set; }
        public bool BattleOver { get; private set; }
        public Fighter Winner { get; private set; }
        public float TimeRemaining { get; private set; }

        private readonly List<BattleEffect> _effects = new List<BattleEffect>();
        public IReadOnlyList<BattleEffect> Effects => _effects;

        private float _shakeTime;
        private float _shakeMagnitude;
        private Vector3 _cameraBasePos;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (battleCamera == null) battleCamera = Camera.main;
            if (battleCamera != null) _cameraBasePos = battleCamera.transform.position;
            StartBattle();
        }

        /// <summary>Resets state and begins a new match.</summary>
        public void StartBattle()
        {
            BattleActive = true;
            BattleOver = false;
            Winner = null;
            TimeRemaining = matchTimeSeconds;
            _effects.Clear();
            AttackSystem.Reset();

            if (player != null) player.ResetForMatch(startingStocks, true);
            if (cpu != null) cpu.ResetForMatch(startingStocks, false);
        }

        private void Update()
        {
            if (!BattleActive) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                EndBattleByTime();
                return;
            }

            CheckBlastZones();
            UpdateEffects(Time.deltaTime);
            UpdateCameraShake(Time.deltaTime);
            CheckVictory();
        }

        /// <summary>
        /// Kills any fighter whose position has crossed the blast bounds.
        /// </summary>
        public void CheckBlastZones()
        {
            CheckFighterBlast(player);
            CheckFighterBlast(cpu);
        }

        private void CheckFighterBlast(Fighter f)
        {
            if (f == null || f.IsDead) return;
            if (Blast.IsOutside(f.GamePosition))
            {
                f.LoseStock();
                Shake(0.6f, 18f);
                SpawnEffect(f.GamePosition, new Color(1f, 0.9f, 0.3f), 60f, 0.5f);
                AudioManager.Instance?.PlaySound("ko");
                if (f.Stocks > 0)
                    f.Respawn(new Vector2((stageLeft + stageRight) * 0.5f, groundY - 200f));
            }
        }

        private void CheckVictory()
        {
            if (BattleOver) return;
            bool playerDead = player != null && player.Stocks <= 0;
            bool cpuDead = cpu != null && cpu.Stocks <= 0;
            if (playerDead || cpuDead)
            {
                Winner = playerDead ? cpu : player;
                EndBattle();
            }
        }

        private void EndBattleByTime()
        {
            // Lower damage % wins on time-out.
            if (player != null && cpu != null)
                Winner = player.Damage <= cpu.Damage ? player : cpu;
            EndBattle();
        }

        private void EndBattle()
        {
            BattleActive = false;
            BattleOver = true;
        }

        /// <summary>
        /// Spawns a visual effect in game-space at the given position.
        /// </summary>
        public void SpawnEffect(Vector2 pos, Color color, float size, float life)
        {
            _effects.Add(new BattleEffect
            {
                pos = pos,
                color = color,
                size = size,
                life = life,
                maxLife = life,
                vel = Vector2.zero
            });
        }

        private void UpdateEffects(float dt)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var e = _effects[i];
                e.life -= dt;
                e.pos += e.vel * dt;
                if (e.life <= 0f) _effects.RemoveAt(i);
            }
        }

        /// <summary>Triggers a camera shake of the given duration and magnitude.</summary>
        public void Shake(float duration, float magnitude)
        {
            _shakeTime = Mathf.Max(_shakeTime, duration);
            _shakeMagnitude = Mathf.Max(_shakeMagnitude, magnitude);
        }

        private void UpdateCameraShake(float dt)
        {
            if (battleCamera == null) return;
            if (_shakeTime > 0f)
            {
                _shakeTime -= dt;
                float m = _shakeMagnitude * worldUnitsPerGameUnit;
                Vector3 offset = new Vector3(
                    Random.Range(-m, m),
                    Random.Range(-m, m),
                    0f);
                battleCamera.transform.position = _cameraBasePos + offset;
                if (_shakeTime <= 0f)
                {
                    battleCamera.transform.position = _cameraBasePos;
                    _shakeMagnitude = 0f;
                }
            }
        }

        /// <summary>
        /// Converts a game-space position (900x600 origin top-left) to a Unity world position.
        /// </summary>
        public Vector3 GameToWorld(Vector2 gamePos)
        {
            // Flip Y because game-space has Y growing downward.
            float wx = (gamePos.x - 450f) * worldUnitsPerGameUnit;
            float wy = (300f - gamePos.y) * worldUnitsPerGameUnit;
            return new Vector3(wx, wy, 0f);
        }
    }
}
