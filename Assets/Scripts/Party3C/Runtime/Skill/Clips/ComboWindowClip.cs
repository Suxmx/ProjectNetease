using Hoshino;
using Slate;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Marks a timeline interval where a buffered input can branch to another skill.
    /// </summary>
    [SkillClipType(1010u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class ComboWindowClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.15f;

        [SkillCustomData] public EPartySkillInputAction InputAction = EPartySkillInputAction.PrimaryAttack;
        [SkillCustomData] public int NextSkillId;
        [SkillCustomData] public bool CancelCurrentSkill = true;

        public override float length
        {
            get => _length;
            set => _length = Mathf.Max(0.0167f, value);
        }

        public override bool isValid => NextSkillId != 0;

        public override string info => NextSkillId != 0 ? $"{InputAction} -> {NextSkillId}" : base.info;

        /// <summary>
        /// Clamps the combo window length after Slate validates the clip.
        /// </summary>
        protected override void OnAfterValidate()
        {
            _length = Mathf.Max(0.0167f, _length);
        }
    }
}
