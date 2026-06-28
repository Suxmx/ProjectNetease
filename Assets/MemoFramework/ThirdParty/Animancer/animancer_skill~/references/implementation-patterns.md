# Animancer Implementation Patterns

## Minimal Playback Component

Use raw clips for small local scripts where fade/events/start-time configuration is unnecessary.

```csharp
using Animancer;
using UnityEngine;

/// <summary>
/// Plays a single configured clip through an Animancer component when enabled.
/// </summary>
public sealed class PlayConfiguredClipOnEnable : MonoBehaviour
{
    [SerializeField] private AnimancerComponent _animancer;
    [SerializeField] private AnimationClip _animation;

    /// <summary>Starts the configured animation for this object.</summary>
    private void OnEnable()
    {
        _animancer.Play(_animation);
    }
}
```

If a replayed action must restart, set the returned state's time or use a transition with start time:

```csharp
AnimancerState state = _animancer.Play(_attack);
state.Time = 0;
```

For smooth restart:

```csharp
_animancer.Play(_attackClip, 0.15f, FadeMode.FromStart);
```

## Transition Fields

Use `ClipTransition` when designers need to tune fade duration, start time, speed, or events in the inspector.

```csharp
using Animancer;
using UnityEngine;

/// <summary>
/// Owns the basic idle and action animation transitions for a character.
/// </summary>
public sealed class SimpleActionAnimator : MonoBehaviour
{
    [SerializeField] private AnimancerComponent _animancer;
    [SerializeField] private ClipTransition _idle;
    [SerializeField] private ClipTransition _action;

    /// <summary>Initializes transition-level callbacks owned by this component.</summary>
    private void Awake()
    {
        _action.Events.OnEnd = PlayIdle;
    }

    /// <summary>Plays the idle animation using its configured transition settings.</summary>
    public void PlayIdle()
    {
        _animancer.Play(_idle);
    }

    /// <summary>Plays the action animation using its configured transition settings.</summary>
    public void PlayAction()
    {
        _animancer.Play(_action);
    }
}
```

This is safe because the inline `_action` transition belongs to one component instance.

## Shared Transition Assets

Use `TransitionAsset` for reusable animation config. Put per-instance callbacks on the returned state, not the shared asset.

```csharp
using Animancer;
using UnityEngine;

/// <summary>
/// Plays shared action transition assets without storing instance callbacks on the asset.
/// </summary>
public sealed class SharedActionAnimator : MonoBehaviour
{
    [SerializeField] private AnimancerComponent _animancer;
    [SerializeField] private TransitionAsset _action;

    /// <summary>Plays the action and registers this instance's completion callback.</summary>
    public void PlayAction()
    {
        AnimancerState state = _animancer.Play(_action);
        state.Events(this).OnEnd ??= OnActionEnd;
    }

    /// <summary>Handles the configured action reaching its Animancer end event.</summary>
    private void OnActionEnd()
    {
        // Return to locomotion or notify gameplay state.
    }
}
```

Use `??=` when the callback is stable for the state owner. Assign directly when the callback must be replaced for the current play.

## Idle Move Action Gate

Keep gameplay interruption rules separate from animation calls.

```csharp
private enum EActionAnimationState
{
    NotActing,
    Acting
}

private EActionAnimationState _currentState;

private void Update()
{
    if (_currentState == EActionAnimationState.NotActing)
        UpdateMovementAnimation();

    UpdateActionAnimation();
}
```

Use a real gameplay state machine when animation state must coordinate movement, input buffering, invulnerability, hit boxes, or network authority.

## Layers

Use a masked layer for an upper-body or facial action that should not replace base locomotion.

```csharp
private AnimancerLayer _baseLayer;
private AnimancerLayer _actionLayer;

private void Awake()
{
    _baseLayer = _animancer.Layers[0];
    _actionLayer = _animancer.Layers[1];
    _actionLayer.Mask = _upperBodyMask;
    _actionLayer.SetDebugName("Upper Body Action");
    _action.Events.OnEnd = FadeOutActionLayer;
}

private void PlayLocomotion(ITransition transition)
{
    _baseLayer.Play(transition);
}

private void PlayAction(ITransition transition)
{
    _actionLayer.Play(transition);
}

private void FadeOutActionLayer()
{
    _actionLayer.StartFade(0, _actionFadeOutDuration);
}
```

Do not use a layer when a simple exclusive state on layer 0 is enough.

## Mixers

For 1D locomotion:

1. Use `LinearMixerTransition`.
2. Assign animations in movement order.
3. Keep thresholds sorted ascending.
4. Set `ParameterName` if code will drive it through `Animancer.Parameters`.
5. Drive the parameter from gameplay speed.

```csharp
private Parameter<float> _speedParameter;

private void Awake()
{
    _animancer.Play(_locomotionMixer);
    _speedParameter = _animancer.Parameters.GetOrCreate<float>(_speedParameterName);
}

private void Update()
{
    _speedParameter.Value = _characterSpeed;
}
```

For 2D locomotion:

1. Use `MixerTransition2D`.
2. Choose `Directional` for direction around a center and `Cartesian` for X/Y blend space behavior.
3. Convert world movement to character local space before assigning X/Y.
4. Use `SmoothedVector2Parameter` when direct input changes look jittery.

## Animator Controller Integration

If keeping an Animator Controller:

- Prefer `HybridAnimancerComponent` when the controller should be played inside Animancer.
- Assign the controller to `HybridAnimancerComponent.Controller`, not `Animator.runtimeAnimatorController`.
- Set controller parameters through the hybrid component or the `ControllerState`.
- Play one-off clips through normal `Play(ClipTransition)` calls.
- Return to controller locomotion with `PlayController()`.

If the controller remains assigned to the native `Animator`, treat Animancer as a temporary overlay and set controller parameters on the `Animator` itself.

## FSM Integration

Animancer includes `Animancer.FSM`, but this project also has its own FSM framework. Inspect existing architecture before introducing a new state machine dependency.

Good Animancer FSM shape:

- Character/root object holds `AnimancerComponent` and a state machine.
- Each state owns one or more `ITransition` fields.
- `OnEnterState` or `OnEnable` plays the transition.
- End events request a state change, usually back to default/idle.
- `CanEnterState` and `CanExitState` encode interruption rules.

Avoid putting input polling, movement authority, damage rules, and animation playback all into one animation script unless the feature is genuinely tiny.

## Root Motion With Movement Systems

Before adding root motion code, identify movement authority:

- Transform-only object: a redirect-to-transform component may be sufficient.
- Rigidbody object: use `RedirectRootMotionToRigidbody` or a custom `OnAnimatorMove` that calls rigidbody movement APIs.
- CharacterController object: `RedirectRootMotionToCharacterController` calls `CharacterController.Move`.
- KCC or custom motor: do not apply `Animator.deltaPosition` directly to the transform; feed it into the motor/controller pipeline or coordinate with the KCC skill.

If the user has not specified root motion ownership, ask for the target movement system or provide Unity-side observation steps.
