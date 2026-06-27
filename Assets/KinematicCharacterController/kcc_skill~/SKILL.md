---
name: kcc
description: Build, modify, debug, or review Unity gameplay code that uses this repo's KinematicCharacterController package. Use for tasks involving KinematicCharacterMotor, ICharacterController, PhysicsMover, IMoverController, KinematicCharacterSystem, grounding, jumping, crouching, moving platforms, root motion, manual simulation/network prediction, collision filtering, or the KCC ExampleCharacter/Examples/Walkthrough code.
---

# KCC

## Overview

Use this skill when working with the Kinematic Character Controller package under `Assets/KinematicCharacterController`. KCC supplies low-level capsule movement, grounding, sliding, rigidbody interaction, and moving-platform handling; game-specific input, camera, animation, and movement rules belong in project controller code.

For 3C work, abstract the problem into input intent, mutually-exclusive locomotion states, velocity/rotation decisions, and KCC callback responsibilities. Do not treat any example controller as the one required architecture.

## Workflow

1. Inspect the existing controller, player input, camera, and prefab wiring before changing code.
2. Keep `Core/` package changes rare. Prefer custom code that implements `ICharacterController` or `IMoverController`.
3. Put velocity decisions in `UpdateVelocity(ref currentVelocity, deltaTime)` and rotation decisions in `UpdateRotation(ref currentRotation, deltaTime)`.
4. Use `KinematicCharacterMotor` APIs for movement state changes. Do not move the character by writing to `transform.position` during simulation.
5. If adding C# code in this repo, follow the local conventions: XML comments for new classes/functions, one nontrivial class per file, `E` prefix for new enums, and `I` prefix for new interfaces.
6. If prefab, scene, compile, or menu-item work is needed, explain the required Unity Editor operation to the user instead of attempting it outside Unity.

## Reference Routing

- Read `references/core-api.md` when touching KCC setup, simulation order, motor settings, callbacks, mover behavior, collision queries, or networking/manual simulation.
- Read `references/implementation-patterns.md` when implementing or debugging 3C controllers, state-machine extensions, grounded/air movement, jumping, crouching, dash/charge, wall abilities, knockback, ladders, swimming, noclip, root motion, arbitrary gravity, moving platforms, AI, or navmesh steering.
- For exact behavior, verify against source files in `../Core`, `../ExampleCharacter`, `../Examples`, and `../Walkthrough`. The PDFs summarized here are `../UserGuide.pdf` and `../Walkthrough.pdf`.

## Core Rules

- Assign `Motor.CharacterController = this` from the custom controller before simulation, usually in `Awake` or `Start`.
- Keep input collection outside the controller when possible. The examples use a player component to build input structs, then call `SetInputs`.
- Use `Motor.GroundingStatus` and `Motor.LastGroundingStatus` for grounded logic. Use `PostGroundingUpdate` for landing/leaving-ground events.
- Call `Motor.ForceUnground()` before jumps or launch impulses that should break ground snapping.
- Use `Motor.SetCapsuleDimensions(...)` for runtime capsule resizing, and test uncrouch clearance with `Motor.CharacterOverlap(...)`.
- Use `Motor.SetPosition(...)` or `Motor.SetPositionAndRotation(...)` for teleports. Use `MoveCharacter`/`RotateCharacter` only when a scheduled collision-solved move is intended.
- Use `PhysicsMover` plus `IMoverController.UpdateMovement(...)` for moving platforms and kinematic moving bodies that characters stand on or are pushed by.
- Keep the character transform and all parents at lossy scale `(1,1,1)`. Do not parent a KCC character under a moving transform.

## Validation

Use focused code inspection and Unity-safe reasoning first. When behavior depends on inspector values, prefabs, scene wiring, or runtime collisions, ask the user for observed data or tell them exactly what to check in Unity.
