using UnityEngine;
using UnityEngine.UI;

namespace SmashBattle
{
    /// <summary>
    /// Drives the on-screen HUD: each fighter's damage %, stock icons, the match
    /// timer, the mobile control overlay and the win/loss modal.
    ///
    /// Wire the references in the inspector. Text fields can be UnityEngine.UI.Text
    /// (legacy) — for TextMeshPro swap the field types accordingly.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        [Header("Battle Reference")]
        [SerializeField] private BattleManager battle;

        [Header("Player HUD")]
        [SerializeField] private Text playerDamageText;
        [SerializeField] private Image[] playerStockIcons;
        [SerializeField] private Text playerNameText;

        [Header("CPU HUD")]
        [SerializeField] private Text cpuDamageText;
        [SerializeField] private Image[] cpuStockIcons;
        [SerializeField] private Text cpuNameText;

        [Header("Timer")]
        [SerializeField] private Text timerText;

        [Header("Mobile")]
        [SerializeField] private GameObject mobileControlsRoot;
        [SerializeField] private bool autoDetectMobile = true;

        [Header("Result Modal")]
        [SerializeField] private GameObject resultModal;
        [SerializeField] private Text resultText;
        [SerializeField] private Button restartButton;

        [Header("Damage Display")]
        [Tooltip("Color gradient as damage rises (low -> high).")]
        [SerializeField] private Color lowDamageColor = Color.white;
        [SerializeField] private Color highDamageColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private float highDamageThreshold = 150f;

        private bool _modalShown;

        private void Awake()
        {
            if (battle == null) battle = BattleManager.Instance;

            if (resultModal != null) resultModal.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartPressed);

            if (mobileControlsRoot != null)
            {
                bool showMobile = autoDetectMobile ? Application.isMobilePlatform : true;
                mobileControlsRoot.SetActive(showMobile);
            }
        }

        private void Update()
        {
            if (battle == null) battle = BattleManager.Instance;
            if (battle == null) return;

            UpdateFighterHUD(battle.Player, playerDamageText, playerStockIcons, playerNameText);
            UpdateFighterHUD(battle.Cpu, cpuDamageText, cpuStockIcons, cpuNameText);
            UpdateTimer();
            UpdateResultModal();
        }

        private void UpdateFighterHUD(Fighter f, Text dmgText, Image[] icons, Text nameText)
        {
            if (f == null) return;

            if (dmgText != null)
            {
                dmgText.text = Mathf.RoundToInt(f.Damage) + "%";
                float t = Mathf.Clamp01(f.Damage / Mathf.Max(1f, highDamageThreshold));
                dmgText.color = Color.Lerp(lowDamageColor, highDamageColor, t);
            }

            if (nameText != null && f.Definition != null)
                nameText.text = f.Definition.emoji + " " + f.Definition.name;

            if (icons != null)
            {
                for (int i = 0; i < icons.Length; i++)
                {
                    if (icons[i] != null)
                        icons[i].enabled = i < f.Stocks;
                }
            }
        }

        private void UpdateTimer()
        {
            if (timerText == null) return;
            int total = Mathf.CeilToInt(battle.TimeRemaining);
            int m = total / 60;
            int s = total % 60;
            timerText.text = string.Format("{0}:{1:00}", m, s);
        }

        private void UpdateResultModal()
        {
            if (resultModal == null) return;

            if (battle.BattleOver && !_modalShown)
            {
                _modalShown = true;
                resultModal.SetActive(true);
                if (resultText != null)
                {
                    bool playerWon = battle.Winner == battle.Player;
                    resultText.text = playerWon ? "YOU WIN!" : "YOU LOSE";
                    resultText.color = playerWon ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
                }
            }
            else if (!battle.BattleOver && _modalShown)
            {
                _modalShown = false;
                resultModal.SetActive(false);
            }
        }

        private void OnRestartPressed()
        {
            if (battle != null) battle.StartBattle();
            if (resultModal != null) resultModal.SetActive(false);
            _modalShown = false;
        }
    }
}
