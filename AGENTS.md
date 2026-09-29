# Repository Guidelines

## Project Structure & Module Organization

The Unity/Tuanjie project is in `Valley Rampart/`: gameplay code is in `Assets/_Game/Core`, `Assets/_Game/Systems`, and `Assets/_Game/Data`; UI is in `Assets/_Game/UI`; scenes are in `Assets/Scenes`; ScriptableObject/config assets are in `Assets/Resources`. Editor smoke probes and chain audits are in `Assets/Editor/Smoke` and `Assets/Editor/ChainAudit`. Scenario fixtures are in `harness/Scenarios`. `pixel-forge/` is a separate Node asset tool, with scripts in `server/`. Planning and handoff records are in the Chinese-named documentation directories. Read `Valley Rampart/AGENTS.md` before changing Unity code.

## Build, Test, and Development Commands

- `Unity.exe -projectPath ".\Valley Rampart"` opens the project with the editor version recorded in `Valley Rampart/ProjectSettings/ProjectVersion.txt`.
- `Unity.exe -projectPath ".\Valley Rampart" -batchmode -runTests -testPlatform editmode -testResults ".\Valley Rampart\TestResults\editmode.xml" -quit` runs Unity Test Framework edit-mode tests when present.
- In the Unity editor, run targeted probes from `Valley/验证`, `Valley/诊断`, and the Chain Audit menu; record the probe name and result in the relevant handoff or test record.
- `pwsh .\pixel-forge\start.ps1` starts the local asset tool; add `-Init` only when initializing its workspace.
- Run `git diff --check` before committing to catch whitespace errors.

## Coding Style & Naming Conventions

Use four-space indentation and the existing C# style: `PascalCase` for types, methods, and public members; `camelCase` for locals and parameters; `_camelCase` for private fields. Keep features in established system folders, pair Unity asset changes with `.meta` files, and keep tunables in ScriptableObjects/config assets. Follow the lifecycle, ownership, EventBus, and Singleton rules in `Valley Rampart/AGENTS.md`. Match surrounding JavaScript semicolon and brace style.

## Testing Guidelines

Add or update a focused editor smoke probe for gameplay changes affecting a user-visible chain. Reuse `TestFixtureApi` and existing `Valley/验证/...` menu patterns. Use Unity Test Runner attributes for isolated tests; no coverage threshold is configured, so explain untested paths in the change description.

## Commit & Pull Request Guidelines

Keep commits small and single-purpose. Recent history uses milestone/task identifiers such as `D890` and `D889`, plus occasional `docs:` or `chore:` prefixes; include the relevant task ID when one exists. Pull requests should state scope, affected paths, validation commands or probes, and limitations. Include before/after screenshots for UI or art changes and link the related planning or handoff record.

## Repository Hygiene

Do not commit Unity-generated `Library/`, `Temp/`, `Logs/`, `obj/`, build output, editor caches, or secrets. Keep generated logs and local tool workspaces outside tracked paths; avoid mixing unrelated documentation, art, and code changes.
