---
name: memo-framework-net
description: "Work with the MemoFramework_Net optional package in Unity. Use when a task mentions Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Net, MemoFramework Net, FishNet bundled under MemoFramework, ParrelSync, NetworkBehaviour/NetworkObject/RPCs/prediction inside this optional package, network setup, multiplayer testing, or networking issues in this project."
---

# MemoFramework Net

Use this skill for `Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Net`. This optional package currently contains third-party `FishNet` and `ParrelSync` only; there is no MemoFramework-owned networking wrapper, runtime asmdef, or custom API in the package root.

## Reuse FishNet Skill

If the task is about FishNet gameplay, RPCs, ownership, spawning, prediction, `NetworkObject`, `NetworkBehaviour`, `NetworkTransform`, transports, or connection/debugging behavior, also use the existing `fishnet-unity` skill. Treat this skill as the MemoFramework package map and project-specific guardrail, then follow FishNet-specific rules from `fishnet-unity`.

## First Pass

1. Confirm the installed package shape:
   - `rg --files Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Net -g '*.cs' -g '*.asmdef'`
   - `Get-ChildItem Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Net`
2. Expect only:
   - `ThirdParty/FishNet`
   - `ThirdParty/ParrelSync`
3. Do not invent MemoFramework Net APIs. If code needs networking behavior, create project gameplay code outside the third-party package unless the request is explicitly to patch bundled FishNet/ParrelSync.
4. If the package is missing, tell the user to open Unity and run `Tools/MemoFramework/Setup`, then import `MemoFramework_Net`.

## Working Model

- `MemoFramework_Net.unitypackage` installs FishNet and ParrelSync into `Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Net`.
- FishNet is the networking framework. Use FishNet concepts directly: `NetworkManager`, `NetworkObject`, `NetworkBehaviour`, `ServerRpc`, `ObserversRpc`, `TargetRpc`, SyncTypes, prediction, server spawning, and ownership checks.
- ParrelSync is for local multi-client Unity Editor testing. Treat it as an editor/testing aid, not runtime gameplay infrastructure.
- Network setup normally requires Unity scene or prefab work. Provide exact user steps for NetworkManager creation, transport setup, prefab registration, scene objects, or ParrelSync clone launching.

## FishNet Guardrails

- Prefer server authority for gameplay-critical state such as damage, spawning, score, inventory, economy, and anti-cheat-sensitive movement.
- Use client authority only for prototypes, local-only tools, or trusted local feel; use FishNet prediction for competitive repeated tick simulation.
- Put RPCs and SyncTypes on `NetworkBehaviour`, not plain `MonoBehaviour`.
- Objects spawned over the network need `NetworkObject` and must be spawned/despawned through FishNet server APIs.
- Gate owner input with `IsOwner`; gate server-only behavior with the FishNet server-started property used by the installed version.
- Use FishNet callbacks (`OnStartClient`, `OnStartServer`, `OnStopClient`, `OnStopServer`, ownership callbacks) instead of assuming Unity `Awake` or `Start` has valid network state.
- Match the installed FishNet version. Search local package code when a remembered FishNet API name does not compile.

## Where To Put New Code

- Put project-specific multiplayer scripts under the project gameplay folders, not inside `ThirdParty/FishNet` or `ThirdParty/ParrelSync`.
- If adding asmdefs outside the default assembly, reference the local FishNet runtime assembly required by the installed package.
- Only patch `ThirdParty` files for a concrete third-party compatibility bug, and keep the patch minimal so package updates remain possible.

## Validation

- For C# changes, use Unity compilation as the source of truth. If compilation or generated code requires the Unity Editor, ask the user to run it and report errors.
- For network behavior, ask the user to test host/client or ParrelSync clone flows and provide logs/observations if the issue depends on runtime state.
- When finalizing, list any required Editor actions separately from code changes.
