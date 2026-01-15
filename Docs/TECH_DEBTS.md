# Soul-Knight 技术债与改造路线

## 影响维护/扩展的技术债 (优先级排序)

### 🔴 1. 关卡推进逻辑缺失 (高优先级)

**问题描述:**
- `MemoryModel.Stage` 初始化为 5,但未找到 `Stage++` 的调用位置
- Boss 死亡后无法自动推进到下一关
- 关卡循环无法正常运行

**影响范围:**
- 游戏主循环无法完成
- 玩家无法体验关卡 1-4
- BigStage 2/3/4+ 的区域名称无法触发

**证据位置:**
- `Assets/Script/Model/MemoryModel.cs:12` - Stage 默认值为 5
- `Assets/Script/Command/MemoryModelCommand.cs` - 只有 Get 方法,无 Add 方法
- 全局搜索 `Stage++` / `AddStage` 无结果

**最小修复路线:**
1. 在 `MemoryModelCommand.cs` 添加方法:
```csharp
public void AddStage(int addition) {
    model.Stage += addition;
}
public void ResetStage() {
    model.Stage = 1;
}
```

2. 找到 Boss 死亡回调位置 (推测在 `Assets/Script/Character/Boss/*.cs`)
3. 在 Boss 死亡时调用:
```csharp
MemoryModelCommand.Instance.AddStage(1);
// 显示 "关卡完成" UI
// 生成传送门或直接重载场景
```

4. 在游戏开始时调用 `ResetStage()` 确保从关卡 1 开始

**工作量:** 1-2 小时

---

### 🔴 2. 硬编码字符串污染 (高优先级)

**问题描述:**
- 大量字符串硬编码在代码中 (GameObject 名称、Tag、LayerMask)
- 修改 Unity 场景后容易导致 `NullReferenceException`
- 难以重构和维护

**典型案例:**
```csharp
// RoomController.cs:44
RoomInstances = GameObject.Find("Generator").GetComponent<RoomPostProcessing>().GetRoomInstances();

// GameLoopBattle.cs:21-22
m_Generator = GameObject.Find("Generator").GetComponent<DungeonGeneratorGrid2D>();
finder = GameObject.Find("AStarPath").GetComponent<AstarPath>();

// PanelBattle.cs:31
TimeLine = GameObject.Find("TimeLine").GetComponent<PlayableDirector>();

// RoomController.cs:26
m_FinishAnim = UnityTool.Instance.GetGameObjectFromCanvas("Finish").GetComponent<Animator>();
```

**影响范围:**
- 场景重命名/重构困难
- 容易出现运行时错误 (GameObject 未找到)
- 难以跨场景复用代码

**最小修复路线:**
1. 创建 `GameObjectReferences.cs` 单例:
```csharp
public class GameObjectReferences : MonoBehaviour {
    public static GameObjectReferences Instance;

    [SerializeField] private DungeonGeneratorGrid2D generator;
    [SerializeField] private AstarPath aStarPath;
    [SerializeField] private PlayableDirector timeLine;
    [SerializeField] private Animator finishAnim;

    public DungeonGeneratorGrid2D Generator => generator;
    public AstarPath AStarPath => aStarPath;
    // ...
}
```

2. 在 Unity Scene 中创建空 GameObject "GameReferences",挂载此脚本
3. 在 Inspector 中拖拽引用 (零 `Find()` 调用)
4. 逐步替换 `GameObject.Find()` 为 `GameObjectReferences.Instance.Generator`

**替代方案 (更彻底):**
- 使用 Unity Addressables 系统
- 使用依赖注入框架 (VContainer / Zenject)

**工作量:** 4-6 小时 (全部替换)

---

### 🟠 3. Model/Command 职责混乱 (中优先级)

**问题描述:**
- `MemoryModelCommand` 既有数据访问 (`GetBigStage()`) 又有业务逻辑 (`GetAreaDisplayName()`)
- `ArchiveCommand` / `PlayerCommand` 等 Command 类实际上是 **数据访问层**,而非命令模式
- 违反单一职责原则

**影响范围:**
- 代码理解困难 (Command 命名误导)
- 业务逻辑分散在 Model/Command/Controller 三层
- 难以单元测试

**典型案例:**
```csharp
// MemoryModelCommand 既访问数据又包含显示逻辑
public string GetAreaDisplayName() {
    switch (GetBigStage()) {
        case 1: return "教学楼";
        case 2: return "天台";
        // ...
    }
}
```

**最小修复路线:**
1. 将 `GetAreaDisplayName()` / `GetStageDisplayName()` 移到新类:
```csharp
public static class StageDisplayHelper {
    public static string GetAreaDisplayName(int bigStage) {
        switch (bigStage) {
            case 1: return "教学楼";
            case 2: return "天台";
            // ...
        }
    }
}
```

2. 重命名 Command 类为 Query/Repository:
   - `MemoryModelCommand` → `MemoryModelQuery` 或 `GameStateRepository`
   - `PlayerCommand` → `PlayerDataQuery`
   - `WeaponCommand` → `WeaponDataQuery`

3. 保留真正的命令 (SceneModelCommand.LoadScene) 为 Command

**工作量:** 2-3 小时 (重命名 + 移动方法)

---

### 🟠 4. 场景强耦合 (中优先级)

**问题描述:**
- 每个场景有独立的 Facade/Controller,但数据共享困难
- `MiddleScene` 选择的角色需要传递到 `BattleScene`
- 依赖 `MemoryModel` 作为唯一数据中转站

**影响范围:**
- 无法独立测试单个场景
- 场景切换时数据传递不透明
- 难以实现场景预加载/异步加载

**典型案例:**
```csharp
// MiddleScene 设置玩家
MemoryModel.PlayerAttr = selectedPlayerAttr;

// BattleScene 读取玩家
PlayerFactory.CreatePlayer(MemoryModel.PlayerAttr.Type);
```

**最小修复路线:**
1. 创建 `GameSession` 单例:
```csharp
public class GameSession : MonoBehaviour {
    public static GameSession Instance;

    public PlayerAttribute SelectedPlayer { get; set; }
    public PetType SelectedPet { get; set; }
    public int CurrentStage { get; set; }
    public int Money { get; set; }

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
```

2. 替换 `MemoryModel` 为 `GameSession`:
```csharp
// MiddleScene
GameSession.Instance.SelectedPlayer = selectedPlayerAttr;

// BattleScene
PlayerFactory.CreatePlayer(GameSession.Instance.SelectedPlayer.Type);
```

3. `GameSession` 负责序列化/反序列化存档

**工作量:** 3-4 小时

---

### 🟠 5. UI 数据绑定为手动更新 (中优先级)

**问题描述:**
- `PanelBattle.OnUpdate()` 每帧手动读取 `GetPlayer().m_Attr.CurrentHp` 更新 UI
- 数据变化时无法自动通知 UI
- 浪费 CPU (每帧更新即使数据未变化)

**影响范围:**
- 性能浪费 (每帧多次属性读取)
- 代码冗余 (多个 Panel 重复更新逻辑)
- 难以实现复杂 UI 联动

**典型案例:**
```csharp
// PanelBattle.OnUpdate() [每帧]
SliderHp.value = GetPlayer().m_Attr.CurrentHp / (float)GetPlayer().m_Attr.m_ShareAttr.MaxHp;
TextHp.text = GetPlayer().m_Attr.CurrentHp + "/" + GetPlayer().m_Attr.m_ShareAttr.MaxHp;
```

**最小修复路线:**
1. 使用现有的 `BindableProperty<T>` (代码中已有):
```csharp
// PlayerAttribute.cs
public BindableProperty<int> CurrentHp = new BindableProperty<int>(100);

// PanelBattle.OnInit()
GetPlayer().m_Attr.CurrentHp.OnValueChanged += (newHp) => {
    SliderHp.value = newHp / (float)GetPlayer().m_Attr.m_ShareAttr.MaxHp;
    TextHp.text = newHp + "/" + GetPlayer().m_Attr.m_ShareAttr.MaxHp;
};
```

2. 将所有需要 UI 绑定的属性改为 `BindableProperty<T>`

**替代方案 (更现代):**
- 使用 UniRx (Reactive Extensions for Unity)
- 使用 Unity UI Toolkit + Data Binding

**工作量:** 6-8 小时 (改造所有属性)

---

### 🟡 6. EventCenter 类型安全性差 (低优先级)

**问题描述:**
- `EventCenter` 使用字符串/枚举注册事件,泛型参数易出错
- 事件参数类型在运行时才检查
- 难以追踪事件的发送者和接收者

**影响范围:**
- 运行时类型转换错误
- 难以重构 (修改事件参数需要全局搜索)
- 事件调用链不透明

**典型案例:**
```csharp
// 发送事件
EventCenter.Instance.NotisfyObserver(EventType.OnPlayerClick, gameObject);

// 接收事件 (类型易错)
EventCenter.Instance.RegisterObserver<GameObject>(EventType.OnPlayerClick, (obj) => {
    // 如果发送者传错类型,运行时崩溃
});
```

**最小修复路线:**
1. 使用强类型事件类:
```csharp
public class OnPlayerClickEvent {
    public GameObject ClickedPlayer;
}

public static class EventBus {
    public static event Action<OnPlayerClickEvent> OnPlayerClick;

    public static void Publish(OnPlayerClickEvent evt) {
        OnPlayerClick?.Invoke(evt);
    }
}

// 使用
EventBus.Publish(new OnPlayerClickEvent { ClickedPlayer = gameObject });
EventBus.OnPlayerClick += (evt) => { ... };
```

**工作量:** 8-10 小时 (重写事件系统)

---

### 🟡 7. Factory 模式滥用 (低优先级)

**问题描述:**
- 多个 Factory 类 (PlayerFactory / EnemyFactory / WeaponFactory) 但职责单一
- 大部分 Factory 只是 `Instantiate()` 包装
- ResourceFactory 的 Proxy 模式增加复杂度但收益低

**影响范围:**
- 代码层次过深 (Factory → ProxyFactory → ResourcesFactory)
- 难以理解对象创建流程
- 单元测试困难

**最小修复路线:**
1. 合并简单 Factory 到 EntityFactory:
```csharp
public static class EntityFactory {
    public static IPlayer CreatePlayer(PlayerType type, Vector3 position) { ... }
    public static IEnemy CreateEnemy(EnemyType type, Vector3 position) { ... }
    public static IWeapon CreateWeapon(WeaponType type) { ... }
}
```

2. 保留复杂工厂 (如 AttributeFactory,有实际逻辑)

**工作量:** 4-5 小时

---

### 🟡 8. CSV 解析硬编码 (低优先级)

**问题描述:**
- ScriptableObject 手动解析 CSV 字符串
- 列顺序变化会导致解析错误
- 难以支持新字段

**影响范围:**
- 数据配置脆弱
- 美术/策划修改 CSV 易出错

**最小修复路线:**
1. 使用 CSV 解析库 (如 CsvHelper):
```csharp
using CsvHelper;

public void LoadPlayerData() {
    using (var reader = new StreamReader(csvPath))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture)) {
        var records = csv.GetRecords<PlayerAttribute>();
        // ...
    }
}
```

2. PlayerAttribute 添加 CSV 字段注解

**工作量:** 3-4 小时

---

### 🟡 9. MonoBehaviour 过度使用 (低优先级)

**问题描述:**
- 许多纯数据类继承 MonoBehaviour (如 `Room` 可以是纯 C# 类)
- Controller 继承 AbstractController 但不需要 MonoBehaviour 特性
- 增加 GC 压力

**影响范围:**
- 性能影响小但累积可观
- 单元测试需要 Unity 环境

**最小修复路线:**
1. 将纯数据类改为 struct 或 class:
```csharp
// 原: public class Room
public struct Room {
    public RoomInstanceGrid2D roomInstanceGrid2D;
    public int CurrentEnemyNum;
    public int WaveNum;
}
```

2. Controller 改为纯 C# 类,通过 GameMediator 调用 MonoBehaviour 方法

**工作量:** 6-8 小时

---

### 🟢 10. 缺少日志系统 (最低优先级)

**问题描述:**
- 使用 `Debug.Log()` 散落在代码中
- 无法控制日志级别
- 发布版本包含调试日志

**最小修复路线:**
1. 封装日志工具类:
```csharp
public static class GameLogger {
    public enum Level { Debug, Info, Warning, Error }
    public static Level CurrentLevel = Level.Info;

    public static void Log(string message, Level level = Level.Info) {
        if (level >= CurrentLevel) {
            Debug.Log($"[{level}] {message}");
        }
    }
}
```

2. 替换所有 `Debug.Log()` 为 `GameLogger.Log()`

**工作量:** 2-3 小时

---

## 针对需求的改造计划

### 需求: 学校地图房间连接 + 天台出生 + 教学楼 1-5 入口映射

**目标分解:**
1. 天台作为 BirthRoom
2. 教学楼 1-5 楼层,每层包含多个教室
3. 天台 → 教学楼 5F → 4F → ... → 1F (固定顺序)
4. 每层楼之间通过楼梯连接

**推荐改造顺序 (3 步):**

---

#### 第 1 步: 补充关卡推进逻辑 (必须先做)

**原因:** 无法推进关卡,后续地图设计无意义

**任务清单:**
1. 在 `MemoryModelCommand.cs` 添加 `AddStage()` / `ResetStage()` 方法
2. 找到 Boss 死亡回调,添加 `AddStage(1)` 调用
3. 在 `MiddleScene` 进入时调用 `ResetStage()` 确保从关卡 1 开始
4. 测试关卡循环: 1-1 → 1-2 → ... → 1-5 (Boss) → 2-1 → ...

**验收标准:**
- BigStage 1 → BigStage 2 → BigStage 3 自动推进
- TextArea 正确显示 "教学楼" → "天台" → "储物间"

**预计时间:** 2-3 小时

---

#### 第 2 步: 创建学校地图 LevelGraph (核心)

**原因:** 固定布局地图需要预设的房间拓扑

**任务清单:**
1. 在 Unity Editor 中创建 `LevelGraphSchool1.asset` (教学楼 1F)
   - 添加 BirthRoom (天台)
   - 添加 Corridor (楼梯)
   - 添加 5 个 EnemyRoom (教室 1-1 ~ 1-5)
   - 连接顺序: 天台 → 楼梯 → 教室 1-1 → 教室 1-2 → ... → 教室 1-5
   - 最后一个教室连接到 Boss 关 (或下楼楼梯)

2. 创建 `LevelGraphSchool2.asset` (教学楼 2F)
   - 类似结构,但 BirthRoom 改为上楼楼梯入口

3. 修改 `GungeonCustomInput.GetLevelDescription()`:
```csharp
protected override LevelDescriptionGrid2D GetLevelDescription() {
    LevelGraph selectLevelGraph;

    int bigStage = MemoryModelCommand.Instance.GetBigStage();
    int smallStage = MemoryModelCommand.Instance.GetSmallStage();

    if (bigStage == 1) {
        // 教学楼区域
        if (smallStage == 5) {
            selectLevelGraph = roomConfig.LevelGraphBoss; // Boss 关
        } else {
            // 根据 SmallStage 加载对应楼层
            selectLevelGraph = roomConfig.GetSchoolLevelGraph(smallStage);
        }
    } else if (bigStage == 2) {
        // 天台区域 (使用原有 LevelGraph)
        selectLevelGraph = roomConfig.LevelGraph;
    } else {
        // 其他区域 (随机生成)
        selectLevelGraph = roomConfig.LevelGraph;
    }

    // ... 后续生成逻辑不变
}
```

4. 扩展 `IRoomConfig`:
```csharp
public LevelGraph GetSchoolLevelGraph(int floor) {
    switch (floor) {
        case 1: return LevelGraphSchool1;
        case 2: return LevelGraphSchool2;
        case 3: return LevelGraphSchool3;
        case 4: return LevelGraphSchool4;
        default: return LevelGraph; // 降级到默认
    }
}
```

5. 可选: 为每个教室房间添加名称标识
```csharp
// CustomRoom 添加字段
public string RoomDisplayName;  // "1-1 教室"

// RoomTemplate Prefab 中添加 TextMeshPro 显示房间名称
```

**验收标准:**
- 关卡 1-1 ~ 1-4 使用固定学校布局
- 天台作为起始房间
- 教室按顺序连接 (非随机)
- 关卡 1-5 进入 Boss 关

**预计时间:** 6-8 小时

---

#### 第 3 步: 优化房间名称显示 (可选)

**原因:** 提升玩家体验,明确当前位置

**任务清单:**
1. 在 `PanelBattle` 添加 `TextRoomName` UI 元素
2. 监听玩家进入房间事件:
```csharp
EventCenter.Instance.RegisterObserver<Room>(EventType.OnPlayerEnterBattleRoom, (room) => {
    CustomRoom customRoom = room.roomInstanceGrid2D.Room as CustomRoom;
    TextRoomName.text = customRoom.RoomDisplayName;  // "教学楼 1-3 教室"
});
```

3. 为每个 RoomTemplate Prefab 配置 `RoomDisplayName`

**验收标准:**
- 进入房间时显示 "教学楼 X-X 教室"
- 天台显示 "天台"
- 楼梯显示 "楼梯间"

**预计时间:** 2-3 小时

---

## 总工作量估算

| 步骤 | 任务 | 优先级 | 预计时间 |
|------|------|--------|---------|
| **必须** | 补充关卡推进逻辑 | 🔴 高 | 2-3 小时 |
| **必须** | 创建学校地图 LevelGraph | 🔴 高 | 6-8 小时 |
| **推荐** | 优化房间名称显示 | 🟡 中 | 2-3 小时 |
| **可选** | 修复硬编码字符串 | 🟠 中 | 4-6 小时 |
| **可选** | Model/Command 职责重构 | 🟡 低 | 2-3 小时 |

**最小可行路线 (MVP):**
- 步骤 1 + 步骤 2 = **8-11 小时**

**推荐完整路线:**
- 步骤 1 + 步骤 2 + 步骤 3 + 修复硬编码字符串 = **14-20 小时**

---

## 风险评估与降级方案

### 风险 1: Edgar 插件生成失败

**概率:** 中等

**症状:**
- LevelGraph 配置错误导致生成卡死
- 房间模板 Prefab 不兼容导致碰撞体异常

**降级方案:**
- 回退到随机生成 (保留原有 LevelGraph)
- 使用更简单的房间布局 (减少房间数量)

---

### 风险 2: 关卡推进逻辑复杂度超预期

**概率:** 低

**症状:**
- Boss 死亡回调位置难以找到
- 存在多个关卡推进触发点 (传送门/完成所有房间)

**降级方案:**
- 先实现手动推进 (按 N 键调用 `AddStage(1)`)
- 后续补充自动推进逻辑

---

### 风险 3: RoomDisplayName 显示异常

**概率:** 低

**症状:**
- UI 层级错误导致文本不可见
- RoomDisplayName 为空或显示错误

**降级方案:**
- 回退到仅显示区域名称 (BigStage 映射)
- 使用 Debug.Log 输出房间信息

---

## 后续优化建议 (低优先级)

1. **引入配置表系统**
   - 使用 Excel → ScriptableObject 工具 (如 Luban)
   - 策划可视化编辑房间配置

2. **实现小地图系统**
   - 显示已探索房间
   - 标记当前位置和 Boss 房间

3. **添加房间主题变体**
   - 同一 RoomType 有多个视觉主题 (白天教室/夜晚教室)
   - 根据 BigStage 切换主题

4. **性能优化**
   - 对象池管理敌人/子弹
   - 房间预加载 (异步生成)

---

## 结论

**最紧急的技术债:**
1. 🔴 关卡推进逻辑缺失 (阻塞游戏循环)
2. 🔴 硬编码字符串污染 (维护性差)

**针对需求的最小改造:**
1. 补充关卡推进逻辑 (2-3 小时)
2. 创建学校地图 LevelGraph (6-8 小时)
3. 合计: **8-11 小时** 可完成核心功能

**推荐改造顺序:**
```
第 1 天: 修复关卡推进逻辑 + 测试关卡循环
第 2 天: 创建 LevelGraphSchool1-4 + 修改生成逻辑
第 3 天: 测试学校地图 + 优化房间名称显示
```

**长期优化方向:**
- 重构 Model/Command 职责 (2-3 小时)
- 替换硬编码字符串为引用 (4-6 小时)
- 引入数据绑定系统 (6-8 小时)

---

## 🔴 关于 SmallStage 固定为 1~5 的结构限制 (架构级约束)

### 问题描述

当前系统中，**SmallStage 被硬编码为 1~5 的固定范围**，这是一个深层次的架构约束，影响多个子系统。

**核心限制：**
- SmallStage 5 被特殊标记为 **Boss 关**
- 所有关卡生成逻辑基于 "1~4 普通关 + 第5关Boss" 的假设
- 如果要支持 1~6 或 1~N 的动态关卡数，需要修改多个强耦合点

### 当前为何只能显示 1~5

**计算公式位置：** [MemoryModelCommand.cs:30](Assets/Script/Command/MemoryModelCommand.cs#L30)

```csharp
// BigStage 计算 (每5关为一个大区域)
int BigStage = (Stage - 1) / 5 + 1;

// SmallStage 计算 (当前大区域内的小关卡编号)
int SmallStage = Stage - (BigStage - 1) * 5;
```

**示例：**
- Stage 1 → BigStage 1, SmallStage 1
- Stage 5 → BigStage 1, SmallStage 5 (Boss关)
- Stage 6 → BigStage 2, SmallStage 1
- Stage 10 → BigStage 2, SmallStage 5 (Boss关)

**结论：** 公式本身支持任意 Stage 值，但 **SmallStage == 5 被硬编码为 Boss 关判定条件**。

### 如果未来要支持 1~6 / 1~N，需要修改哪些地方

#### 🔴 强耦合点 1：地牢生成逻辑

**文件：** [GungeonCustomInput.cs:20](Assets/Script/AboutRoom/ScriptableObject/GungeonCustomInput.cs#L20)

**当前代码：**
```csharp
protected override LevelDescriptionGrid2D GetLevelDescription() {
    if (MemoryModelCommand.Instance.GetSmallStage() == 5) {
        // SmallStage == 5 → Boss 关
        selectLevelGraph = roomConfig.LevelGraphBoss;
    } else {
        // SmallStage 1-4 → 普通关卡
        selectLevelGraph = roomConfig.LevelGraph;
    }
    // ...
}
```

**问题：**
- **硬编码判断 `SmallStage == 5`**
- 如果要支持 1~6，Boss 关应该在第 6 关，但此处仍会在第 5 关触发 Boss

**修改方案：**
```csharp
// 方案 A: 配置化 Boss 关编号
int bossStageIndex = roomConfig.BossStageIndex; // 默认 5，可配置为 6
if (MemoryModelCommand.Instance.GetSmallStage() == bossStageIndex) {
    selectLevelGraph = roomConfig.LevelGraphBoss;
}

// 方案 B: 动态计算 (每个 BigStage 的最后一关为 Boss)
int stagesPerBigStage = roomConfig.StagesPerBigStage; // 默认 5，可配置为 6
int smallStage = MemoryModelCommand.Instance.GetSmallStage();
if (smallStage == stagesPerBigStage) {
    selectLevelGraph = roomConfig.LevelGraphBoss;
}
```

**影响范围：**
- 需要修改 `IRoomConfig` 接口，添加 `BossStageIndex` 或 `StagesPerBigStage` 字段
- 需要在 Unity Inspector 中配置新字段

---

#### 🔴 强耦合点 2：BigStage / SmallStage 计算公式

**文件：** [MemoryModelCommand.cs:26-30](Assets/Script/Command/MemoryModelCommand.cs#L26)

**当前代码：**
```csharp
public int GetBigStage() {
    return (model.Stage - 1) / 5 + 1;
}

public int GetSmallStage() {
    return model.Stage - (GetBigStage() - 1) * 5;
}
```

**问题：**
- **硬编码除数 5**（每个 BigStage 固定 5 关）
- 如果要支持每个 BigStage 6 关，公式会错误

**修改方案：**
```csharp
// 添加配置字段
private int stagesPerBigStage = 5; // 可配置为 6

public int GetBigStage() {
    return (model.Stage - 1) / stagesPerBigStage + 1;
}

public int GetSmallStage() {
    return model.Stage - (GetBigStage() - 1) * stagesPerBigStage;
}
```

**影响范围：**
- 需要修改 `MemoryModelCommand` 添加配置字段
- 需要提供配置接口（如从 ScriptableObject 读取）

---

#### 🟡 弱耦合点 3：UI 显示逻辑

**文件：** [PanelBattle.cs:78](Assets/Script/Panel/BattleScene/PanelBattle.cs#L78)

**当前代码：**
```csharp
TextMiddle.text = MemoryModelCommand.Instance.GetStageDisplayName();
// 内部调用: return GetBigStage() + "-" + GetSmallStage();
```

**问题：**
- UI 显示依赖 `GetSmallStage()` 的返回值
- 如果 SmallStage 范围改为 1~6，UI 会自动适配（✅ 无需修改）

**结论：** 此处为 **弱耦合**，只要 `GetSmallStage()` 返回正确值，UI 会自动显示正确。

---

#### 🟡 弱耦合点 4：区域名称显示

**文件：** [MemoryModelCommand.cs:42](Assets/Script/Command/MemoryModelCommand.cs#L42)

**当前代码：**
```csharp
public string GetAreaDisplayName() {
    switch (GetBigStage()) {
        case 1: return "教学楼";
        case 2: return "天台";
        case 3: return "储物间";
        default: return "第" + GetBigStage() + "区";
    }
}
```

**问题：**
- 区域名称与 BigStage 绑定，与 SmallStage 无关
- 如果 SmallStage 改为 1~6，此处 **无需修改**（✅ 无影响）

**结论：** 此处为 **弱耦合**，SmallStage 范围变化不影响区域名称。

---

### 哪些地方是"强耦合点"（总结）

| 耦合点 | 文件 | 耦合类型 | 修改难度 | 原因 |
|--------|------|---------|---------|------|
| **Boss 关判定** | [GungeonCustomInput.cs:20](Assets/Script/AboutRoom/ScriptableObject/GungeonCustomInput.cs#L20) | 🔴 强耦合 | 中等 | 硬编码 `SmallStage == 5` |
| **BigStage 计算公式** | [MemoryModelCommand.cs:26](Assets/Script/Command/MemoryModelCommand.cs#L26) | 🔴 强耦合 | 简单 | 硬编码除数 5 |
| **SmallStage 计算公式** | [MemoryModelCommand.cs:30](Assets/Script/Command/MemoryModelCommand.cs#L30) | 🔴 强耦合 | 简单 | 硬编码除数 5 |
| **UI 显示逻辑** | [PanelBattle.cs:78](Assets/Script/Panel/BattleScene/PanelBattle.cs#L78) | 🟡 弱耦合 | 无需修改 | 自动适配 |
| **区域名称显示** | [MemoryModelCommand.cs:42](Assets/Script/Command/MemoryModelCommand.cs#L42) | 🟡 弱耦合 | 无需修改 | 与 SmallStage 无关 |

---

### 修改路线图（如果要支持 1~6）

#### 最小改动方案（推荐）

**步骤 1：配置化 StagesPerBigStage**
- 在 `IRoomConfig` 添加字段 `public int StagesPerBigStage = 5;`
- 在 Unity Inspector 中配置为 6

**步骤 2：修改计算公式**
- 修改 `MemoryModelCommand.GetBigStage()` 和 `GetSmallStage()`
- 从 `IRoomConfig` 读取 `StagesPerBigStage` 替换硬编码的 5

**步骤 3：修改 Boss 关判定**
- 修改 `GungeonCustomInput.GetLevelDescription()`
- 将 `if (SmallStage == 5)` 改为 `if (SmallStage == roomConfig.StagesPerBigStage)`

**预计工作量：** 1-2 小时

---

#### 完整改动方案（支持每个 BigStage 不同关卡数）

如果需要更灵活的配置（例如 BigStage 1 有 6 关，BigStage 2 有 5 关）：

**步骤 1：创建 BigStageConfig**
```csharp
[Serializable]
public class BigStageConfig {
    public int BigStageIndex;
    public int StagesCount;
    public LevelGraph[] LevelGraphs;
    public LevelGraph LevelGraphBoss;
}
```

**步骤 2：重构 MemoryModelCommand**
- 添加 `GetStagesPerBigStage(int bigStage)` 方法
- 从配置表读取每个 BigStage 的关卡数

**步骤 3：重构 GungeonCustomInput**
- 根据 BigStage 动态选择 LevelGraph

**预计工作量：** 4-6 小时

---

### 当前 UI 实验的影响范围

**2026-01-15 实验改动：**
- 文件：[PanelBattle.cs:78-83](Assets/Script/Panel/BattleScene/PanelBattle.cs#L78)
- 改动：将 `TextMiddle.text` 强制显示为 `"{BigStage}-6"`
- 影响：**仅 UI 显示层**，不影响任何游戏逻辑

**验证方法：**
- ✅ Boss 关仍在 SmallStage 5 触发（未改变）
- ✅ 地牢生成逻辑未改变
- ✅ Stage 计算公式未改变
- ❌ UI 显示与真实 SmallStage 不一致（预期行为）

**回滚方法：**
```csharp
// 恢复原代码（删除 78-83 行，恢复为）
TextMiddle.text = MemoryModelCommand.Instance.GetStageDisplayName();
lastBigStage = MemoryModelCommand.Instance.GetBigStage();
ShowAreaName();
```

---

### 结论

**SmallStage 1~5 的限制是架构级约束，涉及 3 个强耦合点：**
1. 🔴 Boss 关判定（GungeonCustomInput）
2. 🔴 BigStage 计算公式（MemoryModelCommand）
3. 🔴 SmallStage 计算公式（MemoryModelCommand）

**如果要支持 1~6 或动态关卡数：**
- 最小改动：1-2 小时（配置化 + 修改 3 个强耦合点）
- 完整改动：4-6 小时（支持每个 BigStage 不同关卡数）

**当前 UI 实验不影响任何游戏逻辑，可安全回滚。**
