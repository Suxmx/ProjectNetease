# KCC Implementation Patterns

## Baseline Architecture

The examples separate responsibilities:

- Player/input component reads Unity input and camera state, then builds an input struct.
- Character controller component implements `ICharacterController` and owns movement state.
- Camera component follows a `CameraFollowPoint` and handles camera collision/zoom separately.
- AI can reuse the same character controller by calling another `SetInputs` overload with AI movement vectors.

When adding new gameplay code, keep KCC callbacks deterministic and input-free where possible. Read inputs in `Update`, store intent, and consume it when KCC calls `UpdateVelocity` or `UpdateRotation`.

## General 3C Controller Blueprint

Use this section as the default shape for new 3C work. It is a framework for decisions, not a fixed character design.

- Input/player component: reads Unity input, camera orientation, lock-on targets, AI commands, or network commands; converts them into an intent struct.
- Character controller component: implements `ICharacterController`, owns movement state, and exposes methods such as `SetInputs(...)`, `TransitionToState(...)`, `AddVelocity(...)`, `AddImpulse(...)`, or `ApplyKnockback(...)`.
- Camera component: follows a target such as `CameraFollowPoint`, performs camera collision separately, and ignores the character colliders.
- Animation/visual component: consumes state and velocity for presentation; root motion must be converted into KCC velocity/rotation instead of moving the transform directly.

Minimum project-facing interface shape. A real controller must also implement every callback in `ICharacterController`.

```csharp
/// <summary>
/// Stores one frame of player or AI movement intent before KCC simulation consumes it.
/// </summary>
public struct CharacterIntent
{
    public Vector3 MoveWorld;
    public Vector3 LookWorld;
    public bool JumpPressed;
    public bool CrouchHeld;
    public bool DashPressed;
}

/// <summary>
/// Describes mutually-exclusive locomotion modes for the character controller.
/// </summary>
public enum ECharacterState
{
    Default,
    Dash,
    Climb,
    Knockback,
}

/// <summary>
/// Receives intent and external impulses, then answers KCC movement callbacks.
/// </summary>
public sealed class ExampleKccCharacter : MonoBehaviour
{
    /// <summary>
    /// Stores input intent for the next KCC simulation tick.
    /// </summary>
    public void SetInputs(in CharacterIntent intent) { }

    /// <summary>
    /// Queues an additive velocity to be consumed from UpdateVelocity.
    /// </summary>
    public void AddVelocity(Vector3 velocity) { }
}
```

Callback responsibilities:

- `SetInputs(...)`: normalize input, project camera-relative movement, record requests such as jump/crouch/dash; do not directly move the motor.
- `BeforeCharacterUpdate`: refresh trigger-derived context such as water, ladder, wall volumes, or timers that must exist before grounding.
- `PostGroundingUpdate`: detect land and leave-ground transitions from `GroundingStatus` and `LastGroundingStatus`.
- `UpdateRotation`: decide character orientation for the current state.
- `UpdateVelocity`: decide the final base velocity for the current state; consume jump requests, dash movement, climbing movement, knockback, and queued impulses here.
- `AfterCharacterUpdate`: clear expired requests, update state timers, run uncrouch clearance checks, and perform transitions that depend on the final transient pose.
- `OnMovementHit`: collect hit normals, wall candidates, charge stop hits, or combat collision context; do not make transform changes here.
- `IsColliderValidForCollisions` and `ProcessHitStabilityReport`: keep them narrow; use only for collision filtering or stability overrides that KCC needs while solving.

## Ground And Air Movement

The example movement pattern:

1. Project camera forward onto `Motor.CharacterUp` to build camera-relative planar movement.
2. Store `_moveInputVector` and `_lookInputVector` in `SetInputs`.
3. In `UpdateRotation`, rotate toward `_lookInputVector` using `Quaternion.LookRotation(..., Motor.CharacterUp)`.
4. In `UpdateVelocity`:
   - If `Motor.GroundingStatus.IsStableOnGround`, reorient current velocity with `Motor.GetDirectionTangentToSurface(currentVelocity, GroundNormal)`, compute target ground velocity from input reoriented onto the ground plane, then smooth with exponential lerp.
   - If airborne, add acceleration on the input plane, cap air speed, project away from unstable obstruction normals, add gravity, then apply drag.

Use `Motor.CharacterUp`, `CharacterForward`, and `CharacterRight` instead of assuming `Vector3.up` unless the feature is intentionally world-up only.

For sprinting/running, keep the same movement path and change the target speed or acceleration based on intent and state. Avoid adding a second movement solver for sprint unless the behavior has different collision, rotation, or input rules.

## Jumping, Grace Times, And Impulses

Do not apply jump velocity directly from input handling. Store a jump request and process it in `UpdateVelocity`.

Typical jump state:

- `_jumpRequested`
- `_jumpConsumed`
- `_jumpedThisFrame`
- `_timeSinceJumpRequested`
- `_timeSinceLastAbleToJump`
- `JumpPreGroundingGraceTime`
- `JumpPostGroundingGraceTime`

When jump is allowed:

1. Choose `jumpDirection = Motor.CharacterUp`, or unstable ground normal if sliding jumps are allowed.
2. Call `Motor.ForceUnground()` before changing velocity.
3. Add `(jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp)`.
4. Optionally add forward scalable velocity.
5. Reset jump request/consume flags.

For launch impulses, store `_internalVelocityAdd`, consume it in `UpdateVelocity`, and call `ForceUnground` first if the impulse should lift the character off the ground.

For double jump, allow a second branch when the first jump has been consumed and the character is not grounded. For wall jump, set a wall-jump flag from `OnMovementHit` when hitting an eligible wall, then consume it in `UpdateVelocity`.

## Landing And Leaving Ground

Use `PostGroundingUpdate` because it runs immediately after the motor updates `GroundingStatus`:

```csharp
if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
{
    OnLanded();
}
else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
{
    OnLeaveStableGround();
}
```

Use this for animation events, sound, dust, or gameplay state transitions.

## Crouching

In input handling, record desired crouch state. When entering crouch, call:

```csharp
Motor.SetCapsuleDimensions(radius, crouchedHeight, crouchedHeight * 0.5f);
MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
```

Handle uncrouch in `AfterCharacterUpdate`, not immediately on key release:

1. Temporarily set standing capsule dimensions.
2. Call `Motor.CharacterOverlap(Motor.TransientPosition, Motor.TransientRotation, buffer, Motor.CollidableLayers, QueryTriggerInteraction.Ignore)`.
3. If blocked, restore crouched dimensions.
4. If clear, restore visual scale and exit crouch.

Prefer `CharacterOverlap` over raw `Physics.OverlapCapsule` so KCC filtering and ignored colliders are respected.

## Arbitrary Gravity And Planet Movement

Store gravity as a vector, not just a scalar. For movement and air control, project with `Motor.CharacterUp` and add `Gravity * deltaTime`.

For orientation:

- Rotate from current up to `-Gravity.normalized` in `UpdateRotation`.
- For slope-aligned visuals, rotate toward `Motor.GroundingStatus.GroundNormal` when stable and toward `-Gravity.normalized` while airborne.
- If rotating around capsule bottom, cache the bottom hemi center before rotation and call `Motor.SetTransientPosition(...)` after changing rotation.

`../Examples/Scripts/PlanetManager.cs` shows a `PhysicsMover` planet that updates character gravity toward the planet center.

## Character States

Use an enum-based state machine when movement logic starts branching. For new repo code, use an `E` prefix such as `ECharacterState`.

Recommended structure:

- `CurrentCharacterState`
- `TransitionToState(ECharacterState newState)`
- `OnStateEnter(ECharacterState state, ECharacterState fromState)`
- `OnStateExit(ECharacterState state, ECharacterState toState)`
- `switch (CurrentCharacterState)` in `SetInputs`, `UpdateRotation`, `UpdateVelocity`, `AfterCharacterUpdate`, `OnMovementHit`, `AddVelocity`, and `ProcessHitStabilityReport`.

Useful state patterns from the Walkthrough:

- Charge: cache forward charge velocity on enter, apply it in `UpdateVelocity`, stop on wall hit in `OnMovementHit` or timeout in `AfterCharacterUpdate`.
- Noclip: on enter call `SetCapsuleCollisionsActivation(false)`, `SetMovementCollisionsSolvingActivation(false)`, and `SetGroundSolvingActivation(false)`; restore all on exit.
- Swimming: detect water triggers in `BeforeCharacterUpdate`, disable ground solving while swimming, smoothly move in 3D, and project velocity against the water surface when trying to leave.
- Ladder: overlap interaction triggers, find a ladder component, snap toward closest point on the ladder segment, disable movement collision solving and ground solving while climbing, then release at top/bottom points.

## State Priority And Extension Rules

Keep locomotion states mutually exclusive, and keep posture/request data separate unless the posture has its own collision or movement rules.

- Locomotion state examples: `Default`, `Dash`, `Climb`, `Swim`, `Noclip`, `Knockback`.
- Posture/request examples: crouch held, jump requested, sprint held, buffered attack, queued impulse, recently found wall.
- High-priority forced states such as `Knockback` may interrupt `Dash`, `Climb`, and `Default`.
- Ability states such as `Dash` or `Climb` should define whether they can be interrupted by jump, external velocity, landing, or combat.
- Default movement should remain the fallback state after temporary states finish.

When adding any new 3C ability, decide these points before coding:

- Enter condition: input, trigger, collision, resource, cooldown, layer, or component requirement.
- Exit condition: time, collision loss, resource depletion, landing, jump, release input, obstruction, or animation event.
- Velocity source: target velocity, acceleration, additive impulse, root motion, or `Motor.GetVelocityForMovePosition(...)`.
- Rotation policy: toward camera, movement, wall, ladder, gravity, lock-on target, or unchanged.
- Collision policy: normal KCC solving, disabled movement solving, disabled ground solving, custom stability, or custom filtering.
- Input policy: full input, constrained input, buffered input, or locked input.
- Interrupt policy: what this state can interrupt, and what can interrupt it.

## General 3C Ability Recipes

These recipes describe reusable choices. Pick the smallest behavior that matches the user request instead of copying an entire walkthrough controller.

### Basic Locomotion

- Build movement intent in world space or character-up space, usually from camera-relative input.
- On stable ground, reorient current velocity and input along `Motor.GroundingStatus.GroundNormal`, then smooth toward the target velocity.
- In air, accelerate on the character-up plane, cap air speed, project away from unstable obstruction normals, apply gravity, then apply drag.
- Implement sprint as a speed/acceleration modifier on this path unless sprint has unique state rules.
- References: `../ExampleCharacter/Scripts/ExampleCharacterController.cs`, `../Walkthrough/2- Basic Movement and Gravity/Scripts/MyCharacterController.cs`.

### Jumping And Launches

- Store jump as a request with optional buffer and coyote timers; consume it in `UpdateVelocity`.
- Before applying upward jump or launch velocity, call `Motor.ForceUnground(...)` when ground snapping should be broken.
- For double jump, allow a second branch after the first jump is consumed and while not stable on ground.
- For wall jump, store a valid wall normal from `OnMovementHit`, then use that normal as the jump direction in `UpdateVelocity`.
- For external launchers, expose an additive velocity method and consume the queued velocity from `UpdateVelocity`.
- References: `../Walkthrough/3- Jumping`, `../Walkthrough/5- Adding velocities and impulses`.

### Crouching

- Treat crouch as posture data unless it needs unique locomotion rules.
- On crouch start, resize through `Motor.SetCapsuleDimensions(...)` and adjust only visual children such as `MeshRoot`.
- On crouch release, test standing clearance in `AfterCharacterUpdate` by temporarily restoring standing dimensions and calling `Motor.CharacterOverlap(...)`.
- If blocked, restore crouched dimensions and keep crouching; if clear, restore visual scale and exit crouch.
- Reference: `../Walkthrough/6- Crouching/Scripts/MyCharacterController.cs`.

### Dash, Charge, And Short Bursts

- Use a state when the burst has duration, collision stop rules, input lock, cooldown, animation timing, or special interruption rules.
- Use a queued impulse when the burst is only an instantaneous velocity addition and regular movement should resume immediately.
- Cache dash direction and speed on state enter; usually consume them in `UpdateVelocity`.
- Stop the dash in `AfterCharacterUpdate` on timeout, or in `OnMovementHit` when hitting an obstruction in the dash direction.
- Preserve or replace vertical velocity intentionally; do not let this be accidental.
- Reference: `../Walkthrough/11- Charging state/Scripts/MyCharacterController.cs`.

### Wall Abilities

Use one wall-context pipeline and specialize it into wall jump, wall slide, wall run, wall climb, or ledge climb.

- Detect wall candidates through `OnMovementHit`, `Motor.CharacterSweep(...)`, `Motor.CharacterCollisionsRaycast(...)`, trigger volumes, or a short forward query.
- Validate candidates by layer/component, wall normal angle against `Motor.CharacterUp`, distance, facing direction, input direction, and whether the hit is unstable ground.
- Store wall context for the next simulation tick: normal, point, collider, last-seen time, and optional wall up/right axes.
- Maintain contact by projecting desired velocity onto the wall plane, adding a small inward bias only if needed, and expiring the state after a short no-wall grace time.
- For wall slide, constrain downward speed and optionally reduce gravity.
- For wall run, use velocity along the horizontal wall tangent and define how gravity is reduced or restored.
- For wall climb, use velocity along wall-up or camera-projected input and define resource, top-exit, and blocked-ceiling behavior.
- For ledge climb, query for a top position and use `Motor.GetVelocityForMovePosition(...)` or animation-root-motion conversion; do not teleport unless the design is explicitly a snap.
- Decide whether movement solving or ground solving should stay enabled. Ladders often disable them; wall abilities usually keep collision solving and may only alter velocity.
- References: wall jump in `../Walkthrough/3- Jumping/Scripts/c- Wall Jumping`, ladder anchoring in `../Walkthrough/14- Climbing Ladders`, velocity-to-position movement via `Motor.GetVelocityForMovePosition(...)`.

### Knockback And Combat Impulses

- Expose a method such as `ApplyKnockback(Vector3 velocity, float lockTime)` when combat or hazards need to push the character.
- Decide whether knockback is an additive impulse on `Default` movement or a high-priority `Knockback` state that locks/reduces input.
- If the impulse should lift the character or prevent immediate slope snap, call `Motor.ForceUnground(...)` before consuming it.
- In a `Knockback` state, set or blend velocity in `UpdateVelocity`, run a timer in `AfterCharacterUpdate`, and transition back to `Default` when the lock ends or landing rules allow.
- Define how repeated knockbacks combine: replace, add, or keep the strongest. Do not leave this implicit in shared combat code.
- References: additive velocity in `../ExampleCharacter/Scripts/ExampleCharacterController.cs`, impulses in `../Walkthrough/5- Adding velocities and impulses`.

## Collision Filtering

Use `IsColliderValidForCollisions(Collider coll)` for game-specific collision rules. Common filters:

- A list of ignored colliders.
- A component lookup on `coll.GetComponentInParent<T>()`.
- Team/faction or gameplay tags.

This affects character movement collision handling only. Camera obstruction checks need their own ignored-collider setup, as shown by `ExamplePlayer` adding character colliders to `ExampleCharacterCamera.IgnoredColliders`.

Use `ProcessHitStabilityReport` only when default stable-ground classification needs to be modified for custom geometry or gameplay rules.

## Moving Platforms And Animated Movers

Use `PhysicsMover` for kinematic moving platforms:

```csharp
public sealed class MyMovingPlatform : MonoBehaviour, IMoverController
{
    public PhysicsMover Mover;

    private void Start()
    {
        Mover.MoverController = this;
    }

    public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
    {
        goalPosition = /* target position */;
        goalRotation = /* target rotation */;
    }
}
```

For Timeline/Playable animation:

1. Set `PlayableDirector.timeUpdateMode = DirectorUpdateMode.Manual`.
2. Cache transform pose before evaluation.
3. Evaluate the director at the desired time.
4. Read evaluated transform as the goal pose.
5. Restore the transform to the cached pose.
6. Return the goal pose from `UpdateMovement`.

If the camera should rotate with a mover, use `Motor.AttachedRigidbody.GetComponent<PhysicsMover>().RotationDeltaFromInterpolation` as in `ExamplePlayer`.

## Root Motion

Do not let Animator root motion move the transform directly.

Pattern from the root motion walkthrough:

- In `OnAnimatorMove`, accumulate `CharacterAnimator.deltaPosition` and `CharacterAnimator.deltaRotation`.
- In `UpdateVelocity`, when grounded, set `currentVelocity = rootMotionPositionDelta / deltaTime`, then reorient it on `Motor.GroundingStatus.GroundNormal`.
- In `UpdateRotation`, multiply current rotation by the accumulated root motion rotation delta.
- In `AfterCharacterUpdate`, reset accumulated root motion deltas.

## Frame-Perfect Visual Rotation

Interpolation can make child visuals feel one physics tick behind, especially in first-person setups. Keep the motor rotation in `UpdateRotation`, but rotate `MeshRoot` every frame after input:

1. Share one `HandleRotation(ref Quaternion rot, float deltaTime)` method between `UpdateRotation` and the visual update.
2. After applying input in `Update`, project the camera forward on `Motor.CharacterUp`.
3. Set `MeshRoot.rotation` from the same rotation logic.

The physics representation remains fixed-tick; only the visual child is frame-updated.

## Teleport, AI, Navmesh, And Stress Testing

- Teleport with `Motor.SetPositionAndRotation(...)`, as in `../Examples/Scripts/Teleporter.cs`.
- AI can call `SetInputs(ref AICharacterInputs inputs)` with movement and look vectors, as in `ExampleAIController`.
- For navmesh, query paths with Unity or another solution, then feed the KCC a velocity or input vector toward the next path point. Do not use NavMeshAgent transform movement as the final mover.
- For stress/manual simulation, see `StressTestManager`: it disables `AutoSimulation` and `Interpolate`, then calls `KinematicCharacterSystem.Simulate(...)` from its own loop.

## Debugging Checklist

Check these before changing core code:

- Is `Motor.CharacterController` assigned before play?
- Is the character or any parent scaled away from `(1,1,1)`?
- Are there extra colliders under the character mesh hierarchy causing self-decollision?
- Is jump/launch velocity applied in `UpdateVelocity` and preceded by `ForceUnground` when needed?
- Is uncrouch using `SetCapsuleDimensions` and `CharacterOverlap`?
- Does every new ability define enter, exit, velocity, rotation, collision, input, and interrupt rules?
- Is a dash/charge writing velocity in `UpdateVelocity` instead of moving the transform?
- Is wall climb/slide/run using explicit wall detection, wall-normal validation, and contact-loss exit rules?
- Is knockback consumed through a velocity/impulse path instead of direct transform displacement?
- Is a moving platform moved only through `PhysicsMover.UpdateMovement`?
- Is manual simulation accidentally running in addition to `AutoSimulation`?
- Is interpolation disabled when custom network interpolation is active?
- Are collision layers, `StableGroundLayers`, and `IsColliderValidForCollisions` rejecting an expected hit?
- Are fixed-size query buffers large enough for the scene density?
