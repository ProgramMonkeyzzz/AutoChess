# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 2022.3.62f2c1 project. Game content belongs under `Assets/`; the current scene is `Assets/Scenes/SampleScene.unity`. Third-party runtime code is in `Assets/Plugins/UniTask/`, while the embedded A* Pathfinding package is in `Packages/com.arongranberg.astar/`. Package and editor configuration is in `Packages/manifest.json`, `Packages/packages-lock.json`, and `ProjectSettings/`. Keep every Unity asset beside its `.meta` file, and place gameplay code in a named `Assets/<Feature>/` folder instead of vendor packages.

## Build, Test, and Development Commands

There is no checked-in build script; use the Unity Editor version pinned in `ProjectSettings/ProjectVersion.txt`.

- Open the repository folder in Unity Hub, allow imports to finish, and press Play to run the scene.
- Use **File > Build Settings > Build** (or **Build And Run**) for a local player. Add scenes to Build Settings first; `EditorBuildSettings.asset` currently lists none.
- Run **Window > General > Test Runner** and execute EditMode or PlayMode tests when they are added.
- For automation, use Unity batch mode, for example: `Unity.exe -batchmode -projectPath . -runTests -testPlatform editmode -testResults test-results.xml -quit`.

## Coding Style & Naming Conventions

Use C# with four-space indentation, braces on their own line, and one type per file. Follow Unity conventions: `PascalCase` for types, methods, and public members; `camelCase` for locals and private fields (use `[SerializeField] private` for Inspector fields); and descriptive scene, prefab, and ScriptableObject names. Align namespaces with feature folders and preserve serialized field names unless migrating intentionally. Run the IDE formatter/analyzer; no repository-wide formatter or linter is configured.

## Testing Guidelines

Add Unity Test Framework assemblies in feature-specific `Tests/Editor` or `Tests/Runtime` folders. Name files and methods for behavior, such as `BoardSetupTests.cs` and `CreatesStartingBoard`. Cover gameplay logic in EditMode tests and scene/component integration in PlayMode tests. There is no coverage threshold; include a focused test or explain the omission in the PR.

## Commit & Pull Request Guidelines

Existing commits use short, imperative, lowercase subjects (for example, `add DOTS` and `add plugins`). Keep commits focused and use the same concise style, expanding the body only when rationale is non-obvious. PRs should describe the user-visible change, list validation performed (Editor version, tests, and target platform), link an issue when applicable, and include screenshots or a short capture for scene, UI, or visual changes. Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated solution files, or local builds.
