## Project Overview

这是一个基于 Unity 2022.3.62f2c1 的自走棋（AutoChess）游戏项目。集成了 A* 寻路系统和 UniTask 异步任务库。请使用全中文与我沟通；代码、路径、命令和标识符可保留原文。修改或新增的方法和字段需要添加注释。

## Architecture

### Core Systems

- **GameManager**: 全局单例管理器，运行在 Unity 主线程，负责加载棋子配置、创建 `NavigationManager` 和 `RenderManager`、调度 `SimulatorBurst` Job、推进 BattleManager 固定时钟、消费渲染快照并输出线程日志
- **BattleManager**: 战斗系统的核心管理器，处理战斗流程
  - 构造时初始化 `CampManager`、`ChessBoard`、`BulletManager` 和空的 `ChessConfigRepository`（不自动从 Resources 加载）
  - `RegisterChess(campId, configId, position)` 按配置工厂创建棋子并立即返回 `UniqueId`；出生点由注册参数写入
  - 通过 `Tick()` 统一驱动活动子弹，通过 `Dispose()` 释放事件监听和池对象
- **CampManager**: 阵营管理系统，维护所有阵营数据
  - 使用 `Dictionary<int, CampGroup>` 存储阵营分组
  - 每个 `CampGroup` 使用 `Dictionary<int, HashSet<ChessController>>` 按 `ConfigId` 分组管理棋子实例
  - 另有 `UniqueId -> ChessController` 索引，提供 `TryGetChess(uniqueId)` 与 `UnregisterChess(uniqueId)`
  - `GetAllChesses` / `GetEnemyChesses` 先按 `AgentId` 排序，相等再比 `UniqueId`
- **ChessConfigRepository**: 按 `ConfigId` 索引棋子静态配置的运行时仓库
  - `Register` 写入配置副本，重复 `ConfigId` 抛错；`ConfigId = 0` 合法
  - `TryGet` 供主线程和战斗线程只读查询
  - `LoadFromResources("Configs/ChessBaseData")` 仅允许主线程调用；`GameManager.Awake` 在启动战斗线程前加载

### Chess System

棋子系统由棋子控制器、配置/运行时数据和有限状态机组成：

- **ChessController**: 棋子控制器，持有棋子的完整状态
  - 包含 `ChessBaseData`（静态配置）和 `ChessRuntimeData`（运行时数据）
  - 持有 `ChessTargetSelector` 用于目标选择；`TargetSelectMode` 覆盖最近/最远/最低血/最高血/最长射程/最高攻击，`Select(mode)` 分发到对应 `Find*`，非法枚举保持原目标
  - 持有 `ChessDamageReceiver`，通过 `DamageReceiver` 接收伤害并触发死亡状态检查
  - 持有 `AttackController`，通过 `AttackController.Tick()` 统一驱动攻击冷却、前摇和命中
  - 公开 `UniqueId`（运行时实例身份）和 `ConfigId`（静态配置身份）
  - 公开构造函数 `new ChessController(baseData)` 自行分配单调递增的 `UniqueId`，单测可继续直接创建
  - 工厂走 `internal ChessController(int uniqueId, ChessBaseData data)`，使用预分配 ID 且禁止二次分配
  - 构造时创建并启动 `Chess.FSM.FSM`，注册 `MovementState`、`AttackState`、`DeadState` 及相关状态转换，默认状态为 `MovementState`
  - 战斗循环通过 `ChessController.Tick()` 统一驱动当前状态、移动和攻击
- **ChessBaseData**: 棋子的静态配置数据（从 ScriptableObject 导出）
  - 当前字段：`ConfigId`、`Name`、`MaxHp`、`Atk`、`Range`、`MoveSpeed`、`AtkSpeed`、`WindUpTime`、`BulletSpeed`
  - `Range` 为棋盘 XZ 平面上的浮点攻击距离
  - `Clone()` 复制静态配置，避免多个棋子实例共享可变数据
  - `ChessBaseDataSo` 使用 `[FormerlySerializedAs("ChessId")]` 将旧字段迁移为 `ConfigId`，并与 `ChessBaseData` 保持一一对应；修改后必须重新导出二进制配置
- **ChessRuntimeData**: 棋子的运行时可变数据（HP、位置等）
- **ChessMovementController**: 单个棋子的直接移动控制器
  - 在无障碍矩形棋盘中保存直接移动、目标追踪和 `FleeTarget` 远离命令，并生成纯数据导航快照
  - 不再依赖 `IAstarAI`、`AstarPath` 或 `RVOController`
  - 动态避让由主线程 `NavigationManager` 持有的 `SimulatorBurst` 统一处理
- **AttackController**: 单个棋子的自动攻击控制器
  - 仅对 `TargetSelector.SelectedChessController` 中仍存活、处于 `BaseData.Range` 内且当前未在移动的目标自动起手；`AttackTarget()` 仍可在移动中强制开打
  - `AtkSpeed` 表示每秒攻击次数，命中后的攻击间隔为 `1 / AtkSpeed`
  - 命中前必须完成 `WindUpTime` 前摇；攻击启动时校验射程，前摇期间及结束时仅重新校验目标存活且仍为当前索敌目标，不再检查射程
  - `AtkSpeed <= 0` 或任一 `AtkSpeed`、`Range`、`WindUpTime` 为负值时不攻击
  - `BulletSpeed > 0` 时发送 `BulletSpawnEvent`，否则直接结算即时伤害

### Bullet System

- **BulletManager**: 监听 `BulletSpawnEvent`，负责子弹注册、逐帧追踪、命中伤害和池化回收
  - `Tick(float deltaTime)` 使用 `Vector2.MoveTowards` 在棋盘 XZ 平面更新活动子弹
  - 目标存活时持续追踪最新位置；目标失效后飞向最后有效位置并无伤害回收
  - 创建时快照攻击者当前攻击力，命中使用快照；飞行期间攻击者攻击力变化不影响伤害；攻击者死亡不取消已发射子弹
  - `Dispose()` 反注册事件监听并回收所有活动子弹
- **BulletRuntimeData**: 实现 `IPool`，由 `ObjectPool` 获取和回收，保存创建时锁定的伤害快照；`Initialize` 分配新的 `BulletId`，`Dispose` 清零
- **BulletSpawnEvent**: 攻击控制器与子弹管理器之间的发射事件，携带攻击者、目标和子弹速度

### Render System

表现层位于 `Assets/Scripts/Battle/Render/`，命名空间为 `AutoChess.Render`，只在 Unity 主线程运行：

- `BattleManager` 每个固定步在阵营和子弹 Tick 之后发布 `BattleRenderFrame`，只保留最新一帧
- 快照仅包含存活棋子和活动子弹的 `UniqueId`/`BulletId`、阵营、XZ 位置和 XZ 朝向；死亡棋子与已命中回收的子弹不会进入快照
- `GameManager.RegisterChess(campId, configId, position)` 在主线程查配置、创建导航代理并立刻创建棋子视图，成功返回 `UniqueId`；查不到配置或代理无效则返回 `0` 且不建代理/视图
- `GameManager.UnregisterChess(uniqueId)` 先在主线程摘掉代理和视图，再排队从战斗中注销
- `GameManager` 在消费导航输入后把最新帧交给 `RenderManager.ApplyFrame`
- `RenderManager` 从 `Resources/Prefabs/ChessView` 与 `Resources/Prefabs/BulletView` 实例化视图，按快照 upsert，并销毁快照中消失的棋子/子弹
- `ChessView`/`BulletView` 将棋盘坐标映射为世界坐标 `(x, height, z)`，并在 XZ 平面设置朝向
- SampleScene 含 100x100 的 `ChessBoard` Plane 和俯视相机，不自动生成棋子


### Chess FSM

棋子状态机位于 `Assets/Scripts/Battle/Chess/FSM/`，命名空间为 `Chess.FSM`：

- **FSM**: 绑定到一个 `ChessController`，按状态类型注册状态，按源状态类型注册转换
- **StateBase**: 状态基类，保存可选的 Enter、Stay、Exit 三个 `BehaviorTreeRunner`
  - `OnStateEnter` / `OnStateStay` / `OnStateExit` 是不可重写的模板方法，派生状态通过对应的 `Core` 方法实现自身逻辑
  - Enter 树在状态自身进入逻辑后执行一次，Stay 树在状态自身停留逻辑后使用当前 `deltaTime` 持续推进，Exit 树在状态自身退出清理前执行一次
  - 状态退出时停止全部行为树并取消异步节点，重新进入时重置三棵树
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
6. `Tick(float deltaTime)` 将时间步传给当前状态的 Stay 行为树；无参 `Tick()` 使用零时间步

当前仓库已实现正式的 `MovementState`、`AttackState` 和 `DeadState`，攻击计时由 `AttackController` 负责。

### Behavior Tree System

行为树框架位于 `Assets/Scripts/BehaviorTree/`，命名空间为 `BehaviorTree`，核心逻辑不依赖 `UnityEngine` 或 `UnityEditor`：

- `BehaviorTreeDefinition` 与各类 `NodeData` 只保存树结构、策略和参数；运行时逻辑由 `BehaviorTreeRegistry` 按 `LogicId` 绑定
- `BehaviorTreeRunner` 管理 `Start`、`Tick`、`Stop` 和 `Reset`；树到达 `Success` 或 `Failure` 后保持终态，重新执行前必须 `Reset` 并再次 `Start`
- `SequenceNode`、`SelectorNode` 按顺序驱动子节点，`ParallelNode` 可分别配置 All/Any 成功和失败策略，失败判定优先
- `ActionNode` 返回 `Running`、`Success` 或 `Failure`；异步 Action 继承 `AsyncActionNode` 并使用 `UniTask<ActionResult>`，不得使用 `Task`
- `Blackboard` 使用固定 key 和首次写入的泛型类型传递运行时数据；普通非可空值类型使用池化 `ValueTypeWrap<T>` 保存并在 `Remove`/`Clear` 时回收，`Nullable<T>` 保持原有语义；黑板、注册表和节点状态不参与行为树定义序列化
- `BehaviorTreeBinarySerializer` 仅用于可信本地数据，使用 `BinaryFormatter` 序列化带版本号的行为树定义
- 棋子行为树节点位于 `Assets/Scripts/Battle/Chess/Behavior/`，命名空间 `Chess.Behavior`。Conditional LogicId：`HasTarget`、`IsTargetInRange`、`IsSelfInTargetRange`、`IsTargetDead`。Action LogicId：`AttackTarget`、`MoveToTarget`、`MoveAwayFromTarget`、`SelectTarget`。从 `BehaviorTreeContext.Owner` 读取 `ChessController`；无索敌目标时目标判定/攻击/移动全部失败。`SelectTarget` 的 Int 参数 `Mode` 为 `TargetSelectMode`（0=最近，1=最远，2=最低血，3=最高血，4=最长射程，5=最高攻击），缺参视为最近；选中敌人才 Success，无敌人或非法枚举为 Failure。射程 Conditional 使用 `GetCurrentRange`；死亡目标仍算存在目标并按距离判定。Action 只发一次命令，成功后立即 Success，不 Running 等待到达或命中。构建时必须 `ChessBehaviorTreeLogic.Register(registry)`，且 runner 的 owner 传该棋子

### Behavior Tree Unity Editor

- `Assets/Scripts/Editor/BehaviorTreeGraphAsset.cs` 提供基于 NodeCanvas `GraphEditor` 的行为树编辑源资产，创建菜单为 `Tools > AutoChess > Behavior Tree > Create Asset`
- 编辑器图只用于可视化和编辑，不使用 NodeCanvas 自带行为树运行时；通过 `Validate`、`Export Bytes` 和 `Export Bytes As...` 导出本项目 `BehaviorTreeDefinition`
- 源资产默认保存在 `Assets/Configs/BehaviorTrees/`，bytes 默认保存在 `Assets/Resources/Configs/BehaviorTrees/`
- Action/Conditional 的 LogicId 和参数元数据通过 `BehaviorTreeLogicCatalog` 注册；编辑器参数当前支持 `string`、`int`、`float` 和 `bool`
- 叶节点 Inspector 用参数列表宿主创建 Odin `PropertyTree` 并跨 GraphEditor 的 Layout/Repaint 缓存；禁止把空 `List` 当作多目标传入 `PropertyTree.Create(IList)`
- `ChessBehaviorTreeLogic` 在编辑器/运行时域加载时幂等注册棋子 Action/Conditional，分类为 `Chess`

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
    - `Behavior/`: 棋子行为树 Action 与 Conditional
  - `Camp/`: 阵营管理逻辑
  - `Render/`: 主线程棋子/子弹表现层
- `Assets/Scripts/Config/`: 运行时配置数据结构
- `Assets/Scripts/BehaviorTree/`: 纯 C# 行为树运行时、数据定义、黑板和二进制序列化
- `Assets/Scripts/Editor/`: 编辑器工具和 ScriptableObject 定义
- `Assets/Scripts/Utils/`: 通用工具类（如 Singleton）
- `Assets/Tests/Editor/`: Unity EditMode 测试
- `Assets/Configs/`: ScriptableObject 配置资源（源文件）
- `Assets/Resources/Configs/`: 导出的二进制配置（运行时加载）
- `Assets/Resources/Prefabs/`: 棋子和子弹表现预制体
- `Assets/Plugins/UniTask/`: UniTask 异步库
- `Packages/com.arongranberg.astar/`: A* 寻路包

## Development Workflow

### Running the Project

在 Unity Hub 中打开项目，等待导入完成后点击 Play 按钮运行场景 `Assets/Scenes/SampleScene.unity`。

### Unity Tests

- 项目通过 `com.unity.feature.development` 集成 Unity Test Framework，测试使用 NUnit API。
- 当前测试位于 `Assets/Tests/Editor/`，均为 EditMode 测试；目前没有 PlayMode 测试。
  - `ConfigBinaryExporterTests` 覆盖配置的递归导出、字段序列化和重复导出覆盖行为
  - `ChessConfigRepositoryTests` 覆盖内存注册、重复 `ConfigId` 失败、`TryGet`、`ConfigId = 0`，以及临时 Resources 目录下的 `LoadFromResources`
  - `CampManagerTests` 覆盖工厂注册、出生点写入、按 `ConfigId` 分组、按 `UniqueId` 索引，以及 `AgentId` 相同后再按 `UniqueId` 的稳定排序
  - `FSMTests` 覆盖棋子 FSM 的启动/切换生命周期、Tick 先转换后 Stay、首个满足条件的转换优先、重复/跨实例注册校验，以及状态行为树的阶段顺序、时间步、重入和取消
  - `ChessMovementControllerTests` 覆盖移动命令、远离目标刷新/贴边、状态退出清理、伤害切换、ChessController 统一 Tick 和成功移动命令回切行为
  - `AttackControllerTests` 覆盖目标和射程校验、前摇命中、攻击冷却、移动中不自动起手、跨阶段时间步、目标失效取消及缴械配置
  - `BulletManagerTests` 覆盖事件注册、池化生命周期、追踪移动、命中伤害、失效目标和 BattleManager 驱动
  - `RenderManagerTests` 覆盖渲染快照建视图、位置朝向同步、死亡/命中清除、重复创建和 BattleManager 帧发布
  - `BehaviorTreeTests` 覆盖节点语义、生命周期、Parallel 策略、黑板、注册构建、UniTask 异步 Action 和二进制往返
  - `BehaviorTreeGraphTests` 覆盖 NodeCanvas 图节点端口、图编译、LogicCatalog、bytes 导出往返，以及叶节点空参数列表的 Odin PropertyTree 宿主绘制
  - `ChessBehaviorConditionalTests` 覆盖棋子目标 Conditional 的无目标失败、死亡判定、运行时射程和 LogicId 注册
  - `ChessBehaviorActionTests` 覆盖棋子 Action 的发令即 Success/Failure、攻击/移动副作用、`SelectTarget` 索敌和 LogicId 注册
  - `ChessTargetSelectorTests` 覆盖索敌枚举分发、运行时射程/攻击力、死亡敌人可选中和非法枚举保持原目标
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
- 测试通过后，根据修改的内容按需更新 AGENTS.md 以及 CLAUDE.md。更新完毕后检查AGENTS.md 以及 CLAUDE.md的文件大小，若超出32kb，精简其中内容使之小于32kb

### Generating Configs

修改棋子配置后必须重新导出：
1. 在 Unity 编辑器中选择 `Tools > Configs > Generate Binary Configs`
2. 检查 Console 输出确认导出成功
3. 导出的 `.bytes` 文件会自动刷新到 `Assets/Resources/Configs/`

### Namespace Conventions

- `AutoChess`: 游戏全局管理器
- `AutoChess.Render`: 主线程战斗表现层
- `AutoChess.Utils`: 通用工具类
- `AutoChess.Editor`: 编辑器工具
- `Chess`: 棋子系统
- `Chess.FSM`: 棋子有限状态机
- `Chess.Behavior`: 棋子行为树 Action/Conditional
- `Camp`: 阵营系统
- `Config`: 配置数据结构
- `Editor`: ScriptableObject 和编辑器专用类型
- `BehaviorTree`: 纯 C# 行为树运行时和可序列化数据定义

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
- `ChessBaseDataSo` 与 `ChessBaseData` 字段不同步时，导出/反序列化会丢失或错位数据；`BulletSpeed` 等字段必须同时修改两边并重新导出
- `BinaryFormatter` 已在 .NET 5+ 中标记为过时，但 Unity 2022 仍然支持
- 阵营系统使用两层字典结构，操作时注意空引用检查
- FSM 的状态和转换必须绑定到同一个 FSM 实例，跨实例注册或重复注册会抛异常
- `Tick()` 在 Stay 之前先处理转换，因此转换成功后 `OnStateStay` 会落在新状态上
- 状态子类不得绕过 `StateBase` 的生命周期模板，应重写 `OnStateEnterCore`、`OnStateStayCore` 或 `OnStateExitCore`
- Enter/Exit 行为树各只 Tick 一次；若返回 Running，Enter 树在离开状态时取消，Exit 树在退出回调结束时立即取消
- `ChessController` 创建后 FSM 已处于 `MovementState`；战斗循环应调用 `ChessController.Tick()`，不要再单独驱动 `ChessMovementController.Tick()`
- `BattleManager` 的棋子、FSM、攻击、伤害和子弹逻辑只在其专属工作线程执行；`GameManager` 只在 Unity 主线程调用 `SimulatorBurst`
- `NavigationManager` 的代理注册、`Tick`、Job `Complete` 和 `Dispose` 必须在创建它的 Unity 主线程调用
- 战斗线程不得访问 `Time`、`Debug`、`Transform`、`GameObject` 或 Unity 组件；日志通过 `ThreadLogQueue` 转发到主线程
- 战斗与导航使用固定 `1/60` 秒步长和最多 4 步追赶，通过输入/结果快照交换，允许一帧延迟
- 移动命令由 `ChessController.Tick()` 持续驱动，不因切换到普通状态自动停止；进入 `DeadState` 时会显式停止，成功的新移动命令会自动切回 `MovementState`
- `ChessMovementController` 的坐标使用 XZ 平面，`ChessController` 负责每帧同步 `CurrentPosition`
- 通过 `ChessController.DamageReceiver.ApplyDamage()` 结算伤害后会立即检查 HP；HP 小于等于零时切换到 `DeadState`
- 进入 `DeadState` 会停止并清除移动命令，死亡棋子拒绝新的移动命令，且不注册 `DeadState` 到 `MovementState` 的反向转换
- `ChessController.Tick()` 会按 FSM、移动、攻击顺序更新；也可调用 `Tick(float deltaTime)` 精确推进攻击时间
- 攻击伤害取攻击方 `ChessBaseData.Atk`，首次满足目标、射程且当前未在移动时自动开始前摇；`AttackTarget()` 可在移动中强制起手。前摇锁定后目标离开射程仍会完成攻击，命中后才开始下一次攻击冷却
- 合法攻击流程开始时立即从 `MovementState` 切换到 `AttackState`；`AttackState` 进入停止移动，退出取消前摇并保留冷却
- 攻击状态下成功移动命令立即切回 `MovementState`，HP 归零切换到 `DeadState`；不注册死亡状态的反向转换
- `BulletSpeed > 0` 的攻击依赖唯一 `BulletManager` 监听发射事件；没有监听器时记录错误且不回退即时伤害
- 子弹在创建时快照攻击者当前攻击力，飞行期间攻击力变化不影响命中伤害
- 子弹命中后必须从活动集合移除并归还 `ObjectPool`，不得重复结算或重复回收
- 行为树编辑源资产只在 Unity Editor 中使用，修改图后必须显式执行 `Export Bytes` 才会更新运行时 `.bytes`
- NodeCanvas 原生 `BehaviourTree` 资产与本项目 `BehaviorTreeGraphAsset` 不兼容，不要混用两套运行时
- 叶节点 Inspector 不能把参数 `List` 直接传给 `PropertyTree.Create(IList)`；空列表会抛出 `There must be at least one target` 并打乱 GraphEditor 的 GUILayout
- 未注册到 `BehaviorTreeLogicCatalog` 的 LogicId 仍可手动导出，但编辑器会显示警告；运行时必须另行注册对应工厂
- 构建棋子行为树时必须 `ChessBehaviorTreeLogic.Register(registry)`，且 `BehaviorTreeRunner` 的 owner 传该 `ChessController`；目标 Conditional/攻击/移动 Action 无索敌目标时全部失败。`SelectTarget` 按 Int `Mode` 索敌，选中敌人才 Success。射程 Conditional 走 `GetCurrentRange`；Action 发令成功后立即 Success，持续追踪/远离由移动控制器负责
- 战斗线程不得直接读写 `ChessView`/`BulletView`；表现同步只走 `BattleRenderFrame`，死亡棋子和命中子弹从快照消失后由 `RenderManager` 销毁视图
- 棋子出生点由 `RegisterChess(campId, configId, position)` 写入，调用方不必先 `new ChessController`；查不到 `ConfigId` 时主线程返回 `0`，战斗线程只记录错误并跳过
- 创建棋子时必须 `Clone` 一份 `ChessBaseData`，避免多个实例共享可变配置
- `BattleManager` 默认空仓库，测试需自行 `Configs.Register`；正式运行由 `GameManager.Awake` 调用 `LoadFromResources`
- 棋子/子弹预制体缺失时主线程报错并跳过创建
- 行为树定义只能保存数据，不得保存委托、运行时节点、黑板或 Unity 场景对象；自定义叶节点必须通过稳定 `LogicId` 注册
- Blackboard 值类型条目由 `ValueTypeWrap<T>` 承载，删除或清空时必须让包装对象重置并归还 `ObjectPool`；`Nullable<T>` 不满足包装类的非可空 `struct` 约束，沿用 object 存储
- `ParallelNode` 达到终态时会中止仍在 Running 的子节点；异步节点必须响应运行器传入的取消令牌
- 行为树异步逻辑统一使用 `UniTask`，禁止引入 `System.Threading.Tasks.Task`
- 行为树二进制反序列化仅允许读取可信本地配置，不得处理网络或用户提供的不可信数据
