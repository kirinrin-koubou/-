using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmashBattle
{
    /// <summary>
    /// A single virtual button that reports its held state. Attach to a UI Image and
    /// register it with <see cref="MobileControls"/> in the inspector.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        /// <summary>True while the button is being pressed.</summary>
        public bool Pressed { get; private set; }

        public void OnPointerDown(PointerEventData e) => Pressed = true;
        public void OnPointerUp(PointerEventData e) => Pressed = false;
        public void OnPointerExit(PointerEventData e) => Pressed = false;

        private void OnDisable() => Pressed = false;
    }

    /// <summary>
    /// Collects virtual D-pad + action button state each frame and feeds a
    /// <see cref="FighterInput"/> into the controlled fighter. The D-pad sits
    /// bottom-left and the action buttons bottom-right (set up in the scene).
    ///
    /// Button mapping (matches the original control scheme):
    ///   Jab=Z, Smash=X, Special=Z+X+down macro, Skill=X+C, Combo=Z+X,
    ///   Shield=Space, Trait=C, SkillSlot1=V, SkillSlot2=B.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Fighter target;

        [Header("D-Pad (bottom-left)")]
        [SerializeField] private VirtualButton left;
        [SerializeField] private VirtualButton right;
        [SerializeField] private VirtualButton up;
        [SerializeField] private VirtualButton down;

        [Header("Action Buttons (bottom-right)")]
        [SerializeField] private VirtualButton jab;        // Z
        [SerializeField] private VirtualButton smash;      // X
        [SerializeField] private VirtualButton special;    // Z+X+down macro
        [SerializeField] private VirtualButton skill;      // X+C
        [SerializeField] private VirtualButton combo;      // Z+X
        [SerializeField] private VirtualButton shield;     // Space
        [SerializeField] private VirtualButton trait;      // C
        [SerializeField] private VirtualButton skillSlot1; // V
        [SerializeField] private VirtualButton skillSlot2; // B

        private void Start()
        {
            if (target == null && BattleManager.Instance != null)
                target = BattleManager.Instance.Player;

            if (target != null)
                target.UseExternalInput(true);
        }

        private void Update()
        {
            if (target == null) return;
            target.SetExternalInput(BuildInput());
        }

        /// <summary>Reads all virtual buttons into a single input snapshot.</summary>
        public FighterInput BuildInput()
        {
            var i = FighterInput.None;
            i.left = Held(left);
            i.right = Held(right);
            i.up = Held(up);
            i.down = Held(down);

            i.jab = Held(jab);
            i.smash = Held(smash);
            i.shield = Held(shield);
            i.trait = Held(trait);
            i.skillSlot1 = Held(skillSlot1);
            i.skillSlot2 = Held(skillSlot2);

            // Dedicated combo/skill/special buttons OR their key-combo equivalents.
            i.combo = Held(combo) || (i.jab && i.smash);
            i.skill = Held(skill) || (i.smash && i.trait);
            i.special = Held(special) || (i.jab && i.smash && i.down);

            return i;
        }

        private static bool Held(VirtualButton b) => b != null && b.Pressed;

        /// <summary>Reassign the controlled fighter at runtime.</summary>
        public void SetTarget(Fighter f)
        {
            if (target != null) target.UseExternalInput(false);
            target = f;
            if (target != null) target.UseExternalInput(true);
        }
    }
}
