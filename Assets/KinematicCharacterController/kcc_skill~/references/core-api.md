# KCC Core API Notes

## Source Map

- `../UserGuide.pdf`: package overview, expected architecture, simulation loop, important usage constraints.
- `../Walkthrough.pdf`: step-by-step feature patterns and references to matching `../Walkthrough/*/Scripts` code.
- `../Core/KinematicCharacterMotor.cs`: central capsule motor and collision/grounding solver.
- `../Core/ICharacterController.cs`: callback contract for custom character controllers.
- `../Core/PhysicsMover.cs` and `../Core/IMoverController.cs`: kinematic moving-body contract.
- `../Core/KinematicCharacterSystem.cs`: registration, simulation order, interpolation.
- `../ExampleCharacter/Scripts`: complete example player, camera, and controller.
- `../Examples/Scripts`: moving platforms, AI input, teleport, planet gravity, stress test, manual simulation examples.

## Package Roles

- `KinematicCharacterMotor` solves movement, capsule sweeps, sliding, grounding, steps, ledges, rigidbody interaction, and final transient pose.
- `ICharacterController` is the game-specific behavior surface. Implement this for movement rules, state, input interpretation, filtering, and callbacks.
- `KinematicCharacterSystem` registers all motors and movers, ticks them in the required order, and handles optional interpolation.
- `PhysicsMover` represents a kinematic Rigidbody that characters can stand on or be pushed by.
- `IMoverController` provides the target position and rotation for a `PhysicsMover`.
- `ExampleCharacter`, `Examples`, and `Walkthrough` are learning resources, not required package runtime.

For gameplay architecture, state priority, and reusable 3C ability recipes, read `implementation-patterns.md`. Keep this file focused on package contracts and API facts.

## Character Object Setup

Use this baseline for a custom character:

1. Create a character GameObject with `KinematicCharacterMotor`.
2. Add a controller component implementing `ICharacterController`.
3. Add `public KinematicCharacterMotor Motor;` and assign it in the inspector or via `GetComponent`.
4. Set `Motor.CharacterController = this` in `Awake` or `Start`.
5. Keep render meshes under a child such as `MeshRoot`; remove extra colliders under the character to avoid self-decollision.
6. Keep character and all parents at lossy scale `(1,1,1)`. Scale visual children only.
7. Never make the character a child of a moving transform. Use `PhysicsMover` for moving platforms.

## Callback Order And Responsibilities

`KinematicCharacterMotor.UpdatePhase1`:

- Calls `BeforeCharacterUpdate(deltaTime)`.
- Applies pending `MoveCharacter` movement.
- Solves initial overlaps.
- Updates `LastGroundingStatus`, probes/snaps ground, and updates `GroundingStatus`.
- Calls `PostGroundingUpdate(deltaTime)` after grounding.
- Handles attached rigidbody / moving-platform velocity.

`KinematicCharacterMotor.UpdatePhase2`:

- Calls `UpdateRotation(ref currentRotation, deltaTime)`.
- Applies pending `RotateCharacter`.
- Solves overlaps from rotation and mover displacement.
- Calls `UpdateVelocity(ref BaseVelocity, deltaTime)`.
- Moves from velocity using KCC collision solving.
- Calls `OnDiscreteCollisionDetected` if enabled and overlaps remain.
- Calls `AfterCharacterUpdate(deltaTime)`.

Use callbacks like questions from the motor:

- `UpdateRotation`: what orientation should the character have now?
- `UpdateVelocity`: what velocity should the character have now?
- `IsColliderValidForCollisions`: should this collider block this character?
- `OnGroundHit`: ground probing detected a ground hit.
- `OnMovementHit`: movement sweep hit a collider.
- `ProcessHitStabilityReport`: final chance to adjust hit stability classification.
- `OnDiscreteCollisionDetected`: overlap/discrete collision event, only when `DiscreteCollisionEvents` is enabled.

## Simulation System

With default `KinematicCharacterSystem.Settings.AutoSimulation = true`, simulation runs in `FixedUpdate`:

1. `PreSimulationInterpolationUpdate(deltaTime)` caches pre-simulation poses.
2. `Simulate(deltaTime, CharacterMotors, PhysicsMovers)` runs:
   - `PhysicsMover.VelocityUpdate` for all movers.
   - `KinematicCharacterMotor.UpdatePhase1` for all motors.
   - Moves all `PhysicsMover` bodies to target transient poses.
   - `KinematicCharacterMotor.UpdatePhase2` for all motors.
3. `PostSimulationInterpolationUpdate(deltaTime)` returns transforms to initial tick poses and prepares interpolation.
4. `LateUpdate` runs custom interpolation when `Settings.Interpolate` is true.

For manual simulation or network prediction:

- Ensure the system exists with `KinematicCharacterSystem.EnsureCreation()`.
- Set `KinematicCharacterSystem.Settings.AutoSimulation = false`.
- Call `KinematicCharacterSystem.Simulate(deltaTime, KinematicCharacterSystem.CharacterMotors, KinematicCharacterSystem.PhysicsMovers)` from the owning simulation loop.
- Save and restore character state with `Motor.GetState()` and `Motor.ApplyState(...)`.
- Save and restore mover state with `PhysicsMover.GetState()` and `PhysicsMover.ApplyState(...)`.
- Disable `KinematicCharacterSystem.Settings.Interpolate` when custom interpolation or rollback rendering owns interpolation.

## Motor API That Project Code Usually Uses

- `SetPosition`, `SetRotation`, `SetPositionAndRotation`: instant teleport or state restore. `bypassInterpolation` defaults to true.
- `MoveCharacter`, `RotateCharacter`: schedule a target pose to be processed during the next motor update.
- `SetCapsuleDimensions(radius, height, yOffset)`: runtime capsule resize; updates cached capsule geometry.
- `ForceUnground(time = 0.1f)`: prevents immediate ground snapping, used before jumping or launch impulses.
- `GetDirectionTangentToSurface(direction, surfaceNormal)`: reorient movement on slopes without lateral drift.
- `CharacterOverlap`, `CharacterSweep`, `CharacterCollisionsOverlap`, `CharacterCollisionsSweep`, `CharacterCollisionsRaycast`: query through KCC's capsule shape and filtering.
- `SetCapsuleCollisionsActivation`, `SetMovementCollisionsSolvingActivation`, `SetGroundSolvingActivation`: disable parts of collision/grounding for states such as noclip, swimming, or ladders.
- `EvaluateHitStability`: classify a hit normal and ledge/step stability if a custom feature needs the same stability logic.

## Important Motor State And Settings

- `GroundingStatus`: current grounding report. Check `FoundAnyGround`, `IsStableOnGround`, `SnappingPrevented`, `GroundNormal`, `GroundCollider`, and `GroundPoint`.
- `LastGroundingStatus`: previous grounding report. Compare with current status for land/leave events.
- `BaseVelocity`: direct character velocity controlled by `UpdateVelocity`. Effective velocity also includes attached rigidbody velocity.
- `AttachedRigidbody` and `AttachedRigidbodyVelocity`: moving platform or dynamic body the character is attached to.
- `MaxStableSlopeAngle`, `StableGroundLayers`: stable-ground classification.
- `StepHandling`, `MaxStepHeight`, `AllowSteppingWithoutStableGrounding`, `MinRequiredStepDepth`: step behavior and cost.
- `LedgeAndDenivelationHandling`, `MaxStableDistanceFromLedge`, `MaxVelocityForLedgeSnap`, `MaxStableDenivelationAngle`: ledge and downward slope handling.
- `InteractiveRigidbodyHandling`, `RigidbodyInteractionType`, `SimulatedCharacterMass`, `PreserveAttachedRigidbodyMomentum`: rigidbody push/ride behavior.
- `HasPlanarConstraint`, `PlanarConstraintAxis`: 2.5D or plane-constrained movement.
- `MaxMovementIterations`, `MaxDecollisionIterations`, `CheckMovementInitialOverlaps`: robustness/performance tuning.
- This repo's code has `MaxHitsBudget = 16` and `MaxCollisionBudget = 16`; increase only with care if physics queries truncate results.

## PhysicsMover Rules

Use `PhysicsMover` for any kinematic moving object that should interact correctly with characters:

1. Add `PhysicsMover` to a GameObject with a Rigidbody.
2. Implement `IMoverController`.
3. Set `Mover.MoverController = this`.
4. Return target pose from `UpdateMovement(out goalPosition, out goalRotation, deltaTime)`.

Do not animate or move the Rigidbody/transform directly as the final movement source. If using Timeline or animation, evaluate it manually to read the desired pose, then reset the transform and return that pose to `PhysicsMover`; see `../Examples/Scripts/PlayableMover.cs` and `../Walkthrough/8- Creating a moving platform/Scripts/MyMovingPlatform.cs`.

## Common Constraints From The Docs

- Teleport with `SetPosition` or `SetPositionAndRotation`.
- Runtime capsule resizing must use `SetCapsuleDimensions`.
- Character transform and parents must remain scale `(1,1,1)`.
- The package does not solve input, camera, animation, or game-specific state for you.
- KCC physics queries use fixed-size non-GC buffers. Query-heavy scenes may need budget review.
- Root motion must be converted into velocity/rotation in KCC callbacks, not applied directly to the transform.
