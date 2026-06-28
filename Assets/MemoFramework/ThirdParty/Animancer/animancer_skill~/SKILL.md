---
name: animancer
description: Build, modify, review, or debug Unity animation code that uses this repo's Animancer package. Use when a task mentions Animancer, AnimancerComponent, NamedAnimancerComponent, HybridAnimancerComponent, ClipTransition, TransitionAsset, ControllerTransition, AnimancerState, Animancer Events, layers, mixers, root motion, Animator Controller integration, animation FSMs, or files under Assets/MemoFramework/ThirdParty/Animancer.
---

# Animancer

Use this skill for the Animancer package under `Assets/MemoFramework/ThirdParty/Animancer`. This repo currently contains Animancer `8.0.0`; online Animancer docs are useful background, but local source and samples are the API authority when they differ.

Animancer controls a Unity `Animator` through Playables so gameplay code can play clips, transitions, mixers, and controller states directly. Do not treat it as requiring Animator Controller state machines unless the task explicitly uses `HybridAnimancerComponent`, `ControllerTransition`, or controller parameters.

## First Pass

1. Inspect existing project code, target scripts, serialized fields, prefab/scene expectations, and nearby animation ownership before changing code.
2. Prefer project gameplay/presenter scripts over edits inside `Runtime/`, `Editor/`, or `Samples~`. Change Animancer package source only for a clear package-level defect.
3. Keep `Animator.runtimeAnimatorController` empty for normal `AnimancerComponent` usage. Use `HybridAnimancerComponent` or `ControllerTransition` when controller compatibility is intentional.
4. Pick the smallest animation representation that fits: raw `AnimationClip` for tiny local playback, `ClipTransition` for inspector-configured single-owner clips, `TransitionAsset` for reusable animation configuration, and mixer transitions for blended locomotion.
5. If adding C# code in this project, follow local conventions: XML comments for new classes/functions, one nontrivial class per file, `E` prefix for new enums, and `I` prefix for new interfaces.
6. Do not compile, run, open Unity menus, edit prefabs, or perform runtime verification. If prefab wiring, clip import settings, avatar masks, controller setup, menu items, or play-mode checks are needed, tell the user exactly what to do in Unity.

## Reference Routing

- Read `references/core-api.md` when touching setup, `AnimancerComponent`, keys, states, transitions, events, layers, mixers, controllers, parameters, or root motion.
- Read `references/implementation-patterns.md` when implementing common features such as idle/move/action playback, one-shot actions, animation state presenters, layers, mixers, end events, shared transition assets, or FSM integration.
- Read `references/pitfalls.md` when debugging animation not playing, wrong fades, event conflicts, looping/end behavior, Animator Controller conflicts, root motion, or version/API uncertainty.
- For exact behavior, verify against local source in `../Runtime` and local samples in `../Samples~`. Useful online entry points are the Animancer introduction and manual pages under `https://kybernetik.com.au/animancer/docs/`, but treat them as potentially newer than local 8.0.0.

## Core Rules

- `AnimancerComponent.Play(...)` returns an `AnimancerState`. Repeated `Play` calls are safe, but they continue from the state's current time unless code resets time or uses a start-time transition/fade mode.
- Use `_Animancer.Play(clip, fadeDuration, FadeMode.FromStart)` or a `ClipTransition` with a normalized start time when an action must restart while cross fading.
- Use `state.Speed`, `state.Time`, `state.NormalizedTime`, `state.Duration`, and `state.RemainingDuration` for runtime timing control. Keep gameplay state separate from raw animation state where possible.
- Use transition events on non-shared inline transitions. For shared `TransitionAsset`s, assign callbacks on the returned `AnimancerState` with `state.Events(owner)` so prefab instances do not overwrite each other.
- Remember `OnEnd` fires every frame after the end while the state is still playing and does not fire when the animation is interrupted before the end. Use gameplay state exit/cancel logic for guaranteed cleanup.
- Use `AnimancerLayer`s for independent animation outputs, masks, upper-body actions, facial expressions, or additive overlays. Fade out a layer when a temporary masked action finishes.
- Use `LinearMixerTransition` for 1D parameters such as speed, `MixerTransition2D` for local movement direction, and `ManualMixerTransition` only when code directly owns child weights.
- With KinematicCharacterController or other movement systems, do not blindly apply root motion to transforms. Route root motion into the movement owner or ask the user for the desired movement authority.

## Validation

Use code inspection and Unity-safe reasoning. If behavior depends on serialized fields, avatar masks, transition assets, clip import settings, prefab references, animation events, or runtime state, ask the user for observed data or give exact Unity checks to perform.
