# Animancer Core API Notes

## Source Map

- `../Package.json`: local package metadata. This repo has Animancer `8.0.0` targeting Unity `2022.3`.
- `../Runtime/AnimancerComponent.cs`: main component, graph initialization, `Play`, `TryPlay`, `Stop`, `Evaluate`, disable behavior.
- `../Runtime/NamedAnimancerComponent.cs`: registers clips by `AnimationClip.name`, supports auto play.
- `../Runtime/HybridAnimancerComponent.cs`: bridge for Animator Controllers plus Animancer clip playback.
- `../Runtime/SoloAnimation.cs`: one-animation component for passive objects.
- `../Runtime/Interfaces/ITransition.cs`: transition contract used by `Play(ITransition)`.
- `../Runtime/Utilities/Transitions`: `ClipTransition`, `ControllerTransition`, mixer transitions, transition assets.
- `../Runtime/Core/Nodes`: `AnimancerLayer`, `AnimancerState`, `ClipState`, graph and state dictionary behavior.
- `../Runtime/Core/Events`: Animancer events, end events, named event callbacks.
- `../Runtime/Mixer States`: 1D, 2D, directional, cartesian, and manual mixer state implementations.
- `../Runtime/Utilities/FSM`: optional Animancer FSM utilities and `StateBehaviour`.
- `../Runtime/Utilities/Redirect Root Motion`: root-motion redirect components.
- `../Samples~`: official local examples that match the installed source better than online snippets.

Online docs consulted while building this skill:

- Introduction: `https://kybernetik.com.au/animancer/docs/introduction/`
- Component types: `https://kybernetik.com.au/animancer/docs/manual/playing/component-types`
- Transitions: `https://kybernetik.com.au/animancer/docs/manual/transitions`
- Layers: `https://kybernetik.com.au/animancer/docs/manual/blending/layers`
- Mixers: `https://kybernetik.com.au/animancer/docs/manual/blending/mixers`
- Animancer Events: `https://kybernetik.com.au/animancer/docs/manual/events/animancer`
- Animator Controllers: `https://kybernetik.com.au/animancer/docs/manual/animator-controllers`
- Root Motion: `https://kybernetik.com.au/animancer/docs/manual/other/root-motion`
- FSM: `https://kybernetik.com.au/animancer/docs/manual/fsm`

## Component Roles

- `AnimancerComponent` is the default runtime entry. It wraps `AnimancerGraph`, connects to an `Animator`, and exposes `Layers`, `States`, `Parameters`, and named `Events`.
- `NamedAnimancerComponent` changes default keys from clip references to clip names, can register an animation array in `Awake`, and can play the first clip automatically in `OnEnable`.
- `HybridAnimancerComponent` is for playing a main `RuntimeAnimatorController` through a `ControllerTransition` while still allowing separate Animancer clips. It wraps many Animator-like APIs.
- `SoloAnimation` is for a single passive clip when no gameplay script is needed.

Default setup for a normal character:

1. GameObject has an `Animator`.
2. Add `AnimancerComponent` and assign the `Animator`.
3. Leave `Animator.runtimeAnimatorController` empty unless controller use is intentional.
4. Project code owns state decisions and calls `_Animancer.Play(...)`.

## Play And State Facts

- `Play(AnimationClip)` and `Play(AnimancerState)` immediately stop other states on the same layer and return the state.
- `Play(clip, fadeDuration, mode)` fades in the target and fades out other active states on that layer.
- `Play(ITransition)` gets or creates a state from the transition, plays it with transition fade settings, then applies transition details such as speed, start time, and events.
- `TryPlay(key)` only plays an already registered state or transition library entry. It returns null when missing.
- `Stop(key)` stops and rewinds the registered state. `Stop()` stops all states in the graph.
- `Evaluate()` applies the current graph pose immediately. `Evaluate(deltaTime)` advances then applies; use cautiously because this is manual graph evaluation.
- `AnimancerState.Play()` only affects that state; `AnimancerLayer.Play(...)` affects the whole layer. Prefer layer/component play for normal exclusive states.

State timing:

- `Time` and `NormalizedTime` set the current point in the animation.
- Replaying the same state does not automatically rewind it.
- `Duration` changes speed so the full animation takes the requested seconds.
- `RemainingDuration` changes speed based on current time until `NormalizedEndTime`.
- `MoveTime` preserves events/root motion better than assigning time directly when scrubbing forward, but should not be called repeatedly in one frame.

## Transitions

`ITransition` provides:

- `FadeDuration`
- `FadeMode`
- `Key`
- `CreateState()`
- `Apply(AnimancerState state)`

Common transition types:

- `ClipTransition`: serialized clip, fade duration, speed, normalized start time, and events.
- `ControllerTransition`: serialized `RuntimeAnimatorController`, parameter bindings, and controller stop behavior.
- `TransitionAsset`: ScriptableObject wrapper for an `ITransitionDetailed`; useful when multiple objects reuse the same transition setup.
- `TransitionAssetBase` / `TransitionAssetReference`: polymorphic asset references.
- `LinearMixerTransition`: 1D thresholds and default parameter, with optional speed extrapolation.
- `MixerTransition2D`: 2D mixer choosing cartesian or directional behavior.
- `ManualMixerTransition`: child animations and direct weight control.

Transition selection:

- Inline `ClipTransition` is safest when one component owns the transition and its callbacks.
- `TransitionAsset` is better when designers reuse the same animation config across prefabs or states.
- When a `TransitionAsset` is shared, do not store instance-specific callbacks on the asset's transition events.

## Events

Animancer events are stored in `AnimancerEvent.Sequence`.

- `state.OwnedEvents` creates or clones a sequence so the state owns it.
- `state.SharedEvents` may be shared by multiple states.
- `state.Events(owner)` and `state.Events(owner, out events)` assert state ownership and are the safer default for instance-level callbacks.
- `events.OnEnd` is an end event; it is different from named events.
- `NamedEventDictionary` maps event names to callbacks for named event binding.

Important event behavior:

- End events trigger every frame after the configured normalized end time as long as the animation is playing.
- End events do not run when another animation interrupts before the end.
- Looping animation events must use normalized times in `[0, 1)`.
- If importing Unity `AnimationClip.events` into Animancer events, disable or account for `Animator.fireEvents` to avoid duplicate triggers.

## Layers

- `Layers[0]` is the base layer. Accessing `Layers[1]` creates the second layer.
- Use `Layers.Add()` when code should append a new layer without depending on an index.
- Use `layer.Mask = avatarMask` for body-part isolation.
- Use `layer.IsAdditive = true` only for additive clips designed for additive blending.
- Use `layer.StartFade(0, duration)` to fade out temporary layers such as upper-body actions or facial overlays.
- Each layer has its own `CurrentState`; layer play calls do not change other layers unless graph configuration or masks make them visually overlap.

## Mixers And Parameters

- `AnimancerComponent.Parameters` stores dynamic parameters used by mixers and controllers.
- Cache `Parameter<T>` when reading/writing often; name lookup through `Parameters.GetFloat/SetValue` is simpler but slower.
- `SmoothedFloatParameter` and `SmoothedVector2Parameter` are available for smoothed locomotion inputs.
- For 1D locomotion, use `LinearMixerTransition` with sorted thresholds and a speed parameter.
- For 2D locomotion, use `MixerTransition2D` and feed local-space movement direction into X/Y parameters.
- Keep animations, thresholds, speeds, and synchronization arrays aligned.

## Animator Controllers

Use one of these paths deliberately:

- Native Animator Controller: controller assigned directly to `Animator`. Animancer can temporarily play layers and fade them out, but controller parameters go through `Animator`.
- Hybrid Animancer: controller assigned to `HybridAnimancerComponent.Controller`, `Animator.runtimeAnimatorController` is empty, and controller parameters go through `HybridAnimancerComponent.SetBool/SetFloat/...`.
- Controller state: play a `ControllerTransition` through normal Animancer APIs.

Avoid mixing native controller assignment and `HybridAnimancerComponent.Controller` unless the task explicitly needs to compare or migrate behavior.

## Root Motion

- Root motion depends on `Animator.applyRootMotion` and `OnAnimatorMove`.
- `RedirectRootMotionToTransform`, `RedirectRootMotionToRigidbody`, and `RedirectRootMotionToCharacterController` can apply animator deltas to a different target.
- For custom controllers, KCC, prediction, rollback, or networked characters, decide which system owns movement before applying root motion.
- `AnimancerLayer.AverageVelocity` and transition `AverageVelocity` can help estimate motion, but they are not a substitute for project movement authority.
