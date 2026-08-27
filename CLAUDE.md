# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

这是一个基于 Unity 2022.3.62f2c1 的自走棋（AutoChess）游戏项目。集成了 A* 寻路系统和 UniTask 异步任务库。请使用全中文与我沟通；代码、路径、命令和标识符可保留原文

## Architecture

### Core Systems

- **GameManager**: 全局单例管理器，负责协调各个子系统
- **BattleManager**: 战斗系统的核心管理器，处理战斗流程
- **CampManager**: 阵营管理系统，维护所有阵营数据
  - 使用 `Dictionary<int, CampGroup>` 存储阵营分组
  - 每个 `CampGroup` 使用 `Dictionary<int, HashSet<ChessController>>` 按棋子ID分组管理棋子实例

### Chess System

棋子系统由三个核心类组成：

- **ChessController**: 棋子控制器，持有棋子的完整状态
  - 包含 `ChessBaseData`（静态配置）和 `ChessRuntimeData`（运行时数据）
  - 持有 `ChessTargetSelector` 用于目标选择
- **ChessBaseData**: 棋子的静态配置数据（从 ScriptableObject 导出）
- **ChessRuntimeData**: 棋子的运行时可变数据（HP、位置等）

### Config Pipeline

配置数据使用两阶段流程：

1. **Editor阶段**: 在 `Assets/Configs/` 创建 `ChessBaseDataSo` (ScriptableObject)
2. **Export阶段**: 通过 `Tools > Configs > Generate Binary Configs` 菜单导出二进制配置到 `Assets/Resources/Configs/`
3. **Runtime阶段**: 游戏运行时加载 `.bytes` 二进制文件反序列化为 `ChessBaseData`

导出逻辑位于 `ConfigBinaryExporter.cs`，使用 `BinaryFormatter` 序列化。

## Key Directories

- `Assets/Scripts/Battle/`: 战斗系统核心代码
  - `Chess/`: 棋子相关逻辑
  - `Camp/`: 阵营管理逻辑
- `Assets/Scripts/Config/`: 运行时配置数据结构
- `Assets/Scripts/Editor/`: 编辑器工具和 ScriptableObject 定义
- `Assets/Scripts/Utils/`: 通用工具类（如 Singleton）
- `Assets/Tests/Editor/`: Unity EditMode 测试
- `Assets/Configs/`: ScriptableObject 配置资源（源文件）
- `Assets/Resources/Configs/`: 导出的二进制配置（运行时加载）
- `Assets/Plugins/UniTask/`: UniTask 异步库
- `Packages/com.arongranberg.astar/`: A* 寻路包

## Development Workflow

### Running the Project

在 Unity Hub 中打开项目，等待导入完成后点击 Play 按钮运行场景 `Assets/Scenes/SampleScene.unity`。

### Unity Tests

- 项目通过 `com.unity.feature.development` 集成 Unity Test Framework，测试使用 NUnit API。
- 当前测试位于 `Assets/Tests/Editor/`，均为 EditMode 测试；`ConfigBinaryExporterTests` 覆盖配置的递归导出、字段序列化和重复导出覆盖行为。目前没有 PlayMode 测试。
- 在 Unity 编辑器中选择 `Window > General > Test Runner`，切换到 `EditMode` 页签后点击 `Run All`。
- 也可以在项目根目录使用 PowerShell 批量运行测试（按本机 Unity Hub 的实际安装位置调整 `$UnityEditor`）：

```powershell
$UnityEditor = "C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe"
& $UnityEditor -batchmode -nographics `
    -projectPath (Get-Location).Path `
    -runTests -testPlatform EditMode `
    -testResults (Join-Path (Get-Location) "TestResults.xml") `
    -logFile (Join-Path (Get-Location) "UnityTest.log")
```

- 命令执行后检查退出码、`TestResults.xml` 和 `UnityTest.log`；运行命令行测试前应关闭正在打开同一项目的 Unity 编辑器，避免项目锁冲突。
- 新增编辑器测试时放入 `Assets/Tests/Editor/`，使用 `[SetUp]`/`[TearDown]` 清理测试创建的资源。涉及 `AssetDatabase` 或配置导出的测试必须使用专用临时目录（例如 `__TestName`），不得修改或删除 `Assets/Configs/` 与 `Assets/Resources/Configs/` 下的正式数据。
- 修改编辑器工具、配置导出逻辑或序列化数据结构后，至少运行全部 EditMode 测试；若以后新增 PlayMode 测试，则放入 `Assets/Tests/PlayMode/` 并使用 `-testPlatform PlayMode` 单独运行。

### Generating Configs

修改棋子配置后必须重新导出：
1. 在 Unity 编辑器中选择 `Tools > Configs > Generate Binary Configs`
2. 检查 Console 输出确认导出成功
3. 导出的 `.bytes` 文件会自动刷新到 `Assets/Resources/Configs/`

### Namespace Conventions

- `AutoChess`: 游戏全局管理器
- `AutoChess.Utils`: 通用工具类
- `AutoChess.Editor`: 编辑器工具
- `Chess`: 棋子系统
- `Camp`: 阵营系统
- `Config`: 配置数据结构
- `Editor`: ScriptableObject 和编辑器专用类型

### Code Organization

- 每个功能模块使用独立的命名空间
- ScriptableObject 定义放在 `Editor` 命名空间
- 运行时数据结构放在对应的功能命名空间（如 `Config`, `Chess`）
- 编辑器工具类放在 `AutoChess.Editor` 命名空间并使用 `#if UNITY_EDITOR` 条件编译

## Dependencies

关键依赖包：
- `com.arongranberg.astar`: A* 寻路系统
- UniTask: 高性能异步任务库

## Common Pitfalls

- 修改 `ChessBaseDataSo` 后忘记重新导出配置会导致运行时数据不一致
- `BinaryFormatter` 已在 .NET 5+ 中标记为过时，但 Unity 2022 仍然支持
- 阵营系统使用两层字典结构，操作时注意空引用检查
