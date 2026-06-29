using Hoshino;
using Slate;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Timeline clip that temporarily disables selected KCC motor controls during a skill.
    /// </summary>
    [SkillClipType(1011u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class MotorRestrictionClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.15f;

        [SkillCustomData] public EPartyKccMotorRestriction Restrictions = EPartyKccMotorRestriction.Jump | EPartyKccMotorRestriction.Dash;

        public override float length
        {
            get => _length;
            set => _length = Mathf.Max(0.0167f, value);
        }

        public override bool isValid => Restrictions != EPartyKccMotorRestriction.None;

        public override string info => Restrictions != EPartyKccMotorRestriction.None ? Restrictions.ToString() : base.info;

        /// <summary>
        /// Clamps the restriction interval length after Slate validates the clip.
        /// </summary>
        protected override void OnAfterValidate()
        {
            _length = Mathf.Max(0.0167f, _length);
        }
    }
}
