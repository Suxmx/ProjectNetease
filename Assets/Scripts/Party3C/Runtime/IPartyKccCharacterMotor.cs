using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Provides the gameplay-facing control surface for a party KCC character.
    /// </summary>
    public interface IPartyKccCharacterMotor
    {
        EPartyKccCharacterState CurrentState { get; }
        Vector3 CurrentVelocity { get; }
        int CurrentDashCharges { get; }
        int MaxDashCharges { get; }
        bool IsStableOnGround { get; }

        /// <summary>
        /// Stores player, AI, or network-owned movement intent for the next KCC simulation tick.
        /// </summary>
        void SetInputs(in PartyKccCharacterInputs inputs);

        /// <summary>
        /// Applies a high-priority knockback velocity and locks regular movement for the supplied time.
        /// </summary>
        void ApplyKnockback(Vector3 velocity, float lockTime);

        /// <summary>
        /// Queues an additive velocity to be consumed during the next KCC velocity update.
        /// </summary>
        void AddExternalVelocity(Vector3 velocity);

        /// <summary>
        /// Changes the dash charge cap and fills any newly added capacity.
        /// </summary>
        void SetDashChargeLimit(int dashChargeLimit);
    }
}
