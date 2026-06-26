---
name: memo-framework
description: "Build, modify, review, or debug the MemoFramework Unity core. Use when a task touches Assets/MemoFramework/Framework, MF/MemoFrameworkEntry/MemoFrameworkComponent, GameState/FSM, EventComponent/MFEventArgs, MFRefPool, ObjectPoolComponent/IObject, BlackboardComponent, CutsceneComponent, TimerManager, InputComponent/InputData, DebuggerComponent, or Tools/MemoFramework/Setup optional package installation."
---

# MemoFramework Unity Core

Use this skill for the MemoFramework main framework under `Assets/MemoFramework/Framework`. For installed optional packages, use `memo-framework-net` for `MemoFramework_Net` and `memo-framework-skill` for `MemoFramework_Skill`.

## First Pass

1. Inspect the local project before editing:
   - `rg --files Assets/MemoFramework/Framework -g '*.cs' -g '*.asmdef'`
   - `rg -n "class MF|MemoFrameworkEntry|GameStateComponent|EventComponent|ObjectPoolComponent|MFRefPool|BlackboardComponent|CutsceneComponent|TimerManager" Assets/MemoFramework/Framework`
2. Avoid treating third-party folders as framework-owned code. The framework includes bundled vendor assets under `Assets/MemoFramework/ThirdParty`; only change them when the requested bug is inside that vendor code.
3. Check asmdef boundaries before adding scripts. Core runtime is `MF`; editor inspectors/setup are separate editor assemblies; timer and FSM code have their own asmdefs.
4. If the task requires importing unitypackages, generating code, changing prefabs/scenes, clicking menu items, or assigning serialized fields, tell the user the exact Unity Editor action instead of claiming it was done.

## Architecture

- `MemoFramework.Extension.MF` is the scene/root facade. In `Start`, it keeps the root alive when it has no parent, then exposes static accessors: `MF.Base`, `MF.Event`, `MF.Input`, `MF.ObjectPool`, `MF.GameState`, `MF.Cutscene`, `MF.Blackboard`.
- `MF.GetOrAdd<T>()` first asks `MemoFrameworkEntry.GetComponent<T>()`. If missing, it creates a child GameObject named after the component and adds the component.
- `MemoFrameworkComponent.Awake()` registers itself in `MemoFrameworkEntry`. New framework components must derive `MemoFrameworkComponent` and call `base.Awake()` if overriding.
- `MemoFrameworkEntry.GetComponent(Type)` matches exact component type, not assignable base types.
- `Tools/MemoFramework/Setup` installs core dependencies and optional packages. Core dependencies include Settings Manager, Cinemachine, Input System, and Addressables. Optional packages are relocated to `Assets/MemoFramework/InstalledOptionalPackage`.

## Core Systems

Game state:
- `GameStateComponent` has serialized `m_LauncherTypeName`. At startup it resolves the type with `MFUtils.Assembly.GetType`, creates an `MFLauncher`, calls `InitGameStatesFsm`, waits one frame, then calls `GameStateFsm.Init()`.
- Register states and the start state only inside `MFLauncher.InitGameStatesFsm`; `PushGameState` and `SetAsStartState` throw after the FSM starts.
- Use `GameStateBase` for game-level states. Override `OnStateEnter`, `OnStateUpdate`, and `OnStateExit` through the inherited `MFStateBase` pattern when creating concrete states.
- Transition helpers include `AddTransition(from, to, condition)` and `RequestStateChange(stateName, forceInstant)`.

Events and references:
- Event payloads derive `MFEventArgs`, implement `Clear`, and are normally created with `MFRefPool.Acquire<T>()`.
- `MF.Event.Fire(sender, args)` queues the event and dispatches next `Update`; `FireNow` dispatches immediately and is not thread-safe.
- The event pool releases the `MFEventArgs` after dispatch. Handlers should copy values they need later and should not retain the event args instance.
- Duplicate subscriptions throw. Always unsubscribe with the same handler instance.
- Any class used with `MFRefPool` must implement `IReference.Clear()`.

Object pool:
- Register pools through `MF.ObjectPool.CreateObjectPool(name, prefab)` or `CreateObjectPool(name, Func<GameObject>)`.
- Pooled prefabs must have a component implementing `MemoFramework.ObjectPool.IObject`; spawning fails and destroys the new object if it is missing.
- `Spawn` returns the spawned `Transform`, sets `IObject.Name`, then calls `OnSpawned(userData)`.
- Despawn through `MF.ObjectPool.Despawn(IObject)` or `DespawnAll(poolName)`, not raw `Destroy`, unless removing/destroying the pool itself.

Blackboard, input, cutscene, timers:
- `BlackboardComponent` stores only `int`, `float`, `string`, and `bool` dictionaries by string key. Missing keys log errors and return default values.
- `InputComponent` wraps the generated `MainInputs` input actions and writes frame state into static `InputData`. Extend `InputEvent` or `UIInputEvent` and `_eventActionDict` / `_uiEventActionDict` together.
- `CutsceneComponent` manages a `CutsceneAgent`, entering and fading through `EnterCutScene` and `FadeCutScene`. Custom transitions inherit `CutsceneAgent`.
- Timers (`CountdownTimer`, `RepeatTimer`, `FrequencyTimer`, `StopwatchTimer`) register with `TimerManager`; dispose timers when an owner is destroyed.
- `DebuggerComponent` builds built-in tabs for Information, Console, Setting, and RefPool. Custom tabs must provide prefabs with `TabBase` and, optionally, custom `TabEntryBase`.

## Editing Rules

- Keep changes scoped to the relevant subsystem. Do not add compatibility layers for old APIs unless the project still uses those APIs.
- Follow the project convention from the user instructions: new interfaces use `I` prefix, new enums use `E` prefix, and newly written classes/functions need XML summaries appropriate to their complexity.
- Prefer existing helpers (`MFUtils.Text`, `MFRefPool`, `MFLinkedList`, FSM shortcuts) over new local utility classes.
- For Unity asset work, provide the user with concrete Editor steps: component to add, serialized field to assign, menu path to click, and expected output path.
