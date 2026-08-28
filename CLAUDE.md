## Project Overview

这是一个基于 Unity 2022.3.62f2c1 的自走棋（AutoChess）游戏项目。集成了 A* 寻路系统和 UniTask 异步任务库。请使用全中文与我沟通；代码、路径、命令和标识符可保留原文。修改或新增的方法和字段需要添加注释。

## Architecture

### Core Systems

- **GameManager**: 全局单例管理器，负责协调各个子系统
- **BattleManager**: 战斗系统的核心管理器，处理战斗流程
- **CampManager**: 阵营管理系统，维护所有阵营数据
  - 使用 `Dictionary<int, CampGroup>` 存储阵营分组
  - 每个 `CampGroup` 使用 `Dictionary<int, HashSet<ChessController>>` 按棋子ID分组管理棋子实例

### Chess System

棋子系统由棋子控制器、配置/运行时数据和有限状态机组成：

- **ChessController**: 棋子控制器，持有棋子的完整状态
  - 包含 `ChessBaseData`（静态配置）和 `ChessRuntimeData`（运行时数据）
  - 持有 `ChessTargetSelector` 用于目标选择
  - 持有 `ChessDamageReceiver`，通过 `DamageReceiver` 接收伤害并触发死亡状态检查
  - 持有 `AttackController`，通过 `AttackController.Tick()` 统一驱动攻击冷却、前摇和命中
  - 构造时创建并启动 `Chess.FSM.FSM`，注册 `MovementState`、`AttackState`、`DeadState` 及相关状态转换，默认状态为 `MovementState`
  - 战斗循环通过 `ChessController.Tick()` 统一驱动当前状态、移动和攻击
- **ChessBaseData**: 棋子的静态配置数据（从 ScriptableObject 导出）
  - 当前字段：`ChessId`、`Name`、`MaxHp`、`Atk`、`Range`、`MoveSpeed`、`AtkSpeed`、`WindUpTime`、`HitApplySpeed`
  - `ChessBaseDataSo` 与 `ChessBaseData` 的字段必须保持一一对应，修改后必须重新导出二进制配置
- **ChessRuntimeData**: 棋子的运行时可变数据（HP、位置等）
- **ChessMovementController**: 单个棋子的 A* 移动控制器
  - 通过注入 `IAstarAI` 控制固定 XZ 坐标移动或追踪另一个棋子
  - `ChessController` 每帧调用 `ChessMovementController.Tick()`，将 A* 世界坐标同步到 `CurrentPosition`
  - 棋子预制体需要配置 `RVOController`，半径固定为 `0.5f`
- **AttackController**: 单个棋子的自动攻击控制器
  - 仅攻击 `TargetSelector.SelectedChessController` 中仍存活且处于 `BaseData.Range` 内的目标
  - `AtkSpeed` 表示每秒攻击次数，命中后的攻击间隔为 `1 / AtkSpeed`
  - 命中前必须完成 `WindUpTime` 前摇；前摇结束会再次校验目标，失效时取消本次攻击
  - `AtkSpeed <= 0` 或任一 `AtkSpeed`、`Range`、`WindUpTime` 为负值时不攻击

### Chess FSM

棋子状态机位于 `Assets/Scripts/Battle/Chess/FSM/`，命名空间为 `Chess.FSM`：

- **FSM**: 绑定到一个 `ChessController`，按状态类型注册状态，按源状态类型注册转换
- **StateBase**: 状态基类，生命周期为 `OnStateEnter` / `OnStateStay` / `OnStateExit`
- **StateTransition** / **StateTransition<State1, State2>**: 从 `State1` 转到 `State2`，由 `CanTransition()` 决定是否触发
- **MovementState**: 所有棋子的默认 FSM 初始状态，退出时停止并清理移动控制器
- **AttackState**: 攻击状态，进入时停止移动，退出时取消攻击前摇并保留已有攻击冷却
- **DeadState**: 棋子 HP 归零后的终止状态，进入时停止并清理移动命令，不注册返回 `MovementState` 的转换
- **MovementToDeadTransition**: 当棋子运行时 HP 小于等于零时允许从 `MovementState` 切换到 `DeadState`
- **MovementToAttackTransition** / **AttackToMovementTransition** / **AttackToDeadTransition**: 管理攻击开始、移动中断和死亡条件下的状态切换

使用约定：

1. 用同一个 `FSM` 实例构造状态和转换
2. 先 `AddState`，再 `AddTransition`（源状态和目标状态都必须已注册）
3. `Start<TState>()` 进入初始状态；重复启动会抛异常
4. 每帧调用 `Tick()`：先按注册顺序检查当前状态的转换（第一个满足条件的生效），再对切换后的当前状态调用 `OnStateStay`
5. `ChangeState<TState>()` 可显式切状态；切到当前状态时返回 `false` 且不重入生命周期

当前仓库已实现正式的 `MovementState`、`AttackState` 和 `DeadState`，攻击计时由 `AttackController` 负责。

### Config Pipeline

配置数据使用两阶段流程：

1. **Editor阶段**: 在 `Assets/Configs/` 创建 `ChessBaseDataSo` (ScriptableObject)
2. **Export阶段**: 通过 `Tools > Configs > Generate Binary Configs` 菜单导出二进制配置到 `Assets/Resources/Configs/`
3. **Runtime阶段**: 游戏运行时加载 `.bytes` 二进制文件反序列化为 `ChessBaseData`

导出逻辑位于 `ConfigBinaryExporter.cs`，使用 `BinaryFormatter` 序列化。

## Key Directories

- `Assets/Scripts/Battle/`: 战斗系统核心代码
  - `Chess/`: 棋子相关逻辑
    - `FSM/`: 棋子有限状态机
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
- 当前测试位于 `Assets/Tests/Editor/`，均为 EditMode 测试；目前没有 PlayMode 测试。
  - `ConfigBinaryExporterTests` 覆盖配置的递归导出、字段序列化和重复导出覆盖行为
  - `FSMTests` 覆盖棋子 FSM 的启动/切换生命周期、Tick 先转换后 Stay、首个满足条件的转换优先生效，以及重复/跨实例注册校验
  - `ChessMovementControllerTests` 覆盖移动命令、状态退出清理、伤害切换、ChessController 统一 Tick 和成功移动命令回切行为
  - `AttackControllerTests` 覆盖目标和射程校验、前摇命中、攻击冷却、跨阶段时间步、目标失效取消及缴械配置
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
- 修改编辑器工具、配置导出逻辑、序列化数据结构或棋子 FSM 后，至少运行全部 EditMode 测试；若以后新增 PlayMode 测试，则放入 `Assets/Tests/PlayMode/` 并使用 `-testPlatform PlayMode` 单独运行。
- 测试通过后，根据修改的内容按需更新 AGENTS.md 以及 CLAUDE.md。

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
- `Chess.FSM`: 棋子有限状态机
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
- `ChessBaseDataSo` 与 `ChessBaseData` 字段不同步时，导出/反序列化会丢失或错位数据；新增如 `HitApplySpeed` 的字段时必须同时改两边
- `BinaryFormatter` 已在 .NET 5+ 中标记为过时，但 Unity 2022 仍然支持
- 阵营系统使用两层字典结构，操作时注意空引用检查
- FSM 的状态和转换必须绑定到同一个 FSM 实例，跨实例注册或重复注册会抛异常
- `Tick()` 在 Stay 之前先处理转换，因此转换成功后 `OnStateStay` 会落在新状态上
- `ChessController` 创建后 FSM 已处于 `MovementState`；战斗循环应调用 `ChessController.Tick()`，不要再单独驱动 `ChessMovementController.Tick()`
- 移动命令由 `ChessController.Tick()` 持续驱动，不因切换到普通状态自动停止；进入 `DeadState` 时会显式停止，成功的新移动命令会自动切回 `MovementState`
- `ChessMovementController` 的坐标使用 XZ 平面，`ChessController` 负责每帧同步 `CurrentPosition`
- 通过 `ChessController.DamageReceiver.ApplyDamage()` 结算伤害后会立即检查 HP；HP 小于等于零时切换到 `DeadState`
- 进入 `DeadState` 会停止并清除移动命令，死亡棋子拒绝新的移动命令，且不注册 `DeadState` 到 `MovementState` 的反向转换
- `ChessController.Tick()` 会按 FSM、移动、攻击顺序更新；也可调用 `Tick(float deltaTime)` 精确推进攻击时间
- 攻击伤害取攻击方 `ChessBaseData.Atk`，首次满足目标和射程条件时立即开始前摇，命中后才开始下一次攻击冷却
- 合法攻击流程开始时立即从 `MovementState` 切换到 `AttackState`；`AttackState` 进入停止移动，退出取消前摇并保留冷却
- 攻击状态下成功移动命令立即切回 `MovementState`，HP 归零切换到 `DeadState`；不注册死亡状态的反向转换
- 棋子移动依赖 `RVOController` 和场景中的 `RVOSimulator`；缺少任一组件时移动命令会被拒绝
