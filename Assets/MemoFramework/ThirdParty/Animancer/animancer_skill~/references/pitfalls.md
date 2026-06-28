# Animancer Pitfalls And Debugging

## Version And Documentation

- Local package version is `8.0.0`.
- Online docs may describe newer APIs or Pro-only behavior. If a symbol is not in `../Runtime`, do not use it.
- Prefer local Samples under `../Samples~` for code patterns.
- Do not add compatibility wrappers for docs-only APIs unless the user explicitly asks for migration support.

## Animation Does Not Play

Check in code and ask the user to verify in Unity:

- `AnimancerComponent` has a valid `Animator`.
- Target `AnimationClip` is assigned, non-legacy, and compatible with the avatar/rig.
- `Animator.runtimeAnimatorController` is empty unless using a deliberate native or hybrid controller setup.
- The GameObject and Animator are enabled.
- The graph/layer is not faded to zero.
- The requested key exists before using `TryPlay`.
- The state is not immediately interrupted by another `Play` call each frame.

## Animation Replays From The Middle

Animancer reuses states. `Play` does not automatically rewind an already-created state.

Use one of:

- `state.Time = 0` after immediate play.
- `_animancer.Play(clip, fade, FadeMode.FromStart)` for cross-fade restart.
- `ClipTransition.NormalizedStartTime = 0` in the inspector.
- A transition configured to start from the beginning.

## Fades Look Wrong

Common causes:

- First play on a zero-weight base layer skips fade by design.
- Code sets `state.Time = 0` during a cross fade instead of using `FadeMode.FromStart`.
- Transition `FadeDuration` is ignored because a call overload overrides it.
- The same state is already fading in with less remaining fade time, so Animancer keeps the shorter existing fade.
- Layer weight or mask, not state weight, is the visible issue.

## Events Conflict Or Do Not Fire

- Do not put instance-specific callbacks directly on a shared `TransitionAsset`.
- Use `state.Events(this)` for per-instance callbacks.
- End events do not fire when interrupted before the end.
- End events fire every frame after the end while still playing; transition to another state or stop/fade out after handling.
- For looping clips, normal event times must be in `[0, 1)`.
- If using Unity `AnimationEvent`s and Animancer events together, watch for duplicate callbacks.

## Wrong Animator Controller Behavior

There are three valid modes:

- Pure Animancer: `Animator.runtimeAnimatorController == null`, use `AnimancerComponent`.
- Native controller plus Animancer overlay: controller assigned to `Animator`, controller parameters go through `Animator`.
- Hybrid: controller assigned to `HybridAnimancerComponent.Controller`, controller parameters go through `HybridAnimancerComponent`/`ControllerState`.

Do not silently mix native and hybrid controller ownership. If a scene already has a controller assigned, explain the migration or ask which mode the user wants.

## Layer Or Mask Problems

- Accessing `Layers[1]` creates a layer; be intentional about indexes.
- Masked layers affect only bones included in the `AvatarMask`.
- Temporary layers usually need `StartFade(0, duration)` on completion.
- Additive layers need additive-authored clips.
- Facial/upper-body overlays often need later script execution than base layer initialization.

## Mixer Problems

- `LinearMixerTransition` thresholds must be sorted ascending.
- Animations, thresholds, speeds, and synchronization arrays must have matching intent.
- Feed 2D mixer parameters in character local space, not world space, for character-relative locomotion.
- Smooth parameters when gameplay input changes abruptly.
- Cache `Parameter<T>` for per-frame updates if the code is hot.

## Root Motion Problems

- Root motion requires `Animator.applyRootMotion` and an `OnAnimatorMove` path.
- Redirect components apply root motion to a chosen target, but they may conflict with character motors or networking.
- With KCC/custom motors, never treat direct transform movement as automatically correct. Convert root motion into the movement system's expected input or ask the user to validate the chosen authority.

## Unity-Side Checks To Hand To The User

Ask the user to inspect:

- Component type: `AnimancerComponent`, `NamedAnimancerComponent`, or `HybridAnimancerComponent`.
- Assigned `Animator`, clips, transitions, transition assets, avatar masks, and controller references.
- Animator Controller field on the `Animator`.
- Clip import rig type, legacy flag, loop flag, root motion settings, and avatar compatibility.
- Runtime Animancer Inspector state list: current layer weights, active states, state time, and target weights.
- Console warnings from Animancer optional warnings.
