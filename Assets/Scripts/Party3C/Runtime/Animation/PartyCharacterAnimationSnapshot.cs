using System;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Contains the base motion animation data sent from an owner to remote proxies.
    /// </summary>
    [Serializable]
    public readonly struct PartyCharacterAnimationSnapshot
    {
        /// <summary>
        /// Creates a base motion animation snapshot.
        /// </summary>
        public PartyCharacterAnimationSnapshot(EPartyCharacterBaseMotionState baseState, bool isGrounded, float normalizedSpeed, float verticalSpeed, Vector2 localMove, int sequence)
        {
            BaseState = baseState;
            IsGrounded = isGrounded;
            NormalizedSpeed = Mathf.Clamp01(normalizedSpeed);
            VerticalSpeed = verticalSpeed;
            LocalMove = Vector2.ClampMagnitude(localMove, 1f);
            Sequence = sequence;
        }

        /// <summary>
        /// Gets the replicated base motion state.
        /// </summary>
        public EPartyCharacterBaseMotionState BaseState { get; }

        /// <summary>
        /// Gets whether the owner was grounded when this snapshot was produced.
        /// </summary>
        public bool IsGrounded { get; }

        /// <summary>
        /// Gets planar movement speed normalized for locomotion blending.
        /// </summary>
        public float NormalizedSpeed { get; }

        /// <summary>
        /// Gets signed vertical speed relative to the character up axis.
        /// </summary>
        public float VerticalSpeed { get; }

        /// <summary>
        /// Gets planar velocity expressed in actor local X/Z axes.
        /// </summary>
        public Vector2 LocalMove { get; }

        /// <summary>
        /// Gets a monotonically increasing local snapshot sequence.
        /// </summary>
        public int Sequence { get; }

        /// <summary>
        /// Returns a copy with the provided snapshot sequence.
        /// </summary>
        public PartyCharacterAnimationSnapshot WithSequence(int sequence)
        {
            return new PartyCharacterAnimationSnapshot(BaseState, IsGrounded, NormalizedSpeed, VerticalSpeed, LocalMove, sequence);
        }

        /// <summary>
        /// Returns true when the visual difference is large enough to replicate.
        /// </summary>
        public bool DiffersFrom(in PartyCharacterAnimationSnapshot other, float speedTolerance, float verticalSpeedTolerance, float localMoveTolerance)
        {
            if (BaseState != other.BaseState || IsGrounded != other.IsGrounded)
                return true;

            if (Mathf.Abs(NormalizedSpeed - other.NormalizedSpeed) > speedTolerance)
                return true;

            if (Mathf.Abs(VerticalSpeed - other.VerticalSpeed) > verticalSpeedTolerance)
                return true;

            return Vector2.SqrMagnitude(LocalMove - other.LocalMove) > localMoveTolerance * localMoveTolerance;
        }
    }
}
