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
