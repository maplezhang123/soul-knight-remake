# Soul-Knight 房间与关卡系统彻底厘清

## 核心概念区分

### 1. 联机语义的 "RoomPack/RoomCode" 系统

**位置:** `Assets/Script/MultiPlayer/SoulKnightProtocol.cs`

**Protobuf 定义:**
```csharp
// 联机房间信息包 (纯联机用途)
public class RoomPack {
    string RoomName;       // 房间名称 (如 "Player123's Room")
    int CurrentNum;        // 当前玩家数
    int MaxNum;            // 最大玩家数
    RoomCode RoomCode;     // 房间状态
    List<PlayerPack> PlayerPacks;  // 房间内玩家列表
}

// 房间状态枚举 (纯联机用途)
public enum RoomCode {
    WaitForJoin = 0,   // 等待加入
    Playing = 1,       // 游戏中
    Full = 2,          // 已满
}
```

**使用场景:**
- **仅用于联机模式** 的房间列表 (`PanelRoomList`)
- 通过 `RequestFindRoom` / `RequestJoinRoom` / `RequestCreateRoom` 与服务器通信
- 位置: `Assets/Script/MultiPlayer/Request/*.cs`

**单机模式关系:**
- **完全无关**,单机代码不引用 `RoomPack` 或 `RoomCode`
- `PanelRoomList` 在单机模式下不可见

**结论:** 这是联机大厅系统,与游戏内地牢房间无关。

---

### 2. 游戏内地牢房间系统 (RoomType / CustomRoom)

**位置:** `Assets/Script/AboutRoom/CustomRoom.cs` 和 `Assets/Script/GameEnum.cs`

**RoomType 枚举 (游戏内房间类型):**
```csharp
public enum RoomType {
    BirthRoom,        // 出生房间 (玩家起始点)
    EnemyRoom,        // 普通敌人房间
    EliteEnemyRoom,   // 精英敌人房间 (多波敌人)
    BossRoom,         // Boss 房间
    TeleportRoom,     // 传送房间
    SecretRoom,       // 秘密房间
    TreasureRoom,     // 宝箱房间
    ShopRoom,         // 商店房间
    Corridor,         // 走廊 (连接房间)
}
```

**CustomRoom 类:**
```csharp
public class CustomRoom : RoomBase {  // 继承自 Edgar.Unity.RoomBase
    public RoomType RoomType;  // 房间类型
}
```

**使用场景:**
- 地牢生成时的房间类型标识
- 通过 Edgar 地牢生成插件生成房间布局
- 每个房间模板 (Prefab) 绑定一个 `RoomType`

**数据源:**
- 存储在 ScriptableObject: `IRoomConfig`
- 配置文件路径: `Assets/Resources/Prefabs/Room/*.asset`
  - `LevelGraph.asset` - 普通关卡图
  - `LevelGraphBoss.asset` - Boss 关卡图

---

### 3. Room 类 (运行时房间实例)

**位置:** `Assets/Script/Controller/RoomController.cs:6`

**定义:**
```csharp
public class Room {
    public RoomInstanceGrid2D roomInstanceGrid2D;  // Edgar 生成的房间实例
    public int CurrentEnemyNum;  // 当前房间敌人数量
    public int WaveNum;          // 剩余波次 (2-4)
    public int SpawnEnemyNum;    // 总生成敌人数 (3-6 + WaveNum)
}
```

**使用场景:**
- `RoomController.OnAfterRunUpdate()` 遍历所有生成的房间
- 每个 `RoomInstanceGrid2D` 包装成一个 `Room` 对象
- 管理房间内敌人生成、门控制、宝箱生成

**生命周期:**
```
BattleScene 加载
  ↓
GungeonCustomInput.GetLevelDescription() → 生成 LevelGraph
  ↓
DungeonGeneratorGrid2D.GenerateCoroutine() → 生成物理房间
  ↓
RoomPostProcessing.GetRoomInstances() → 获取所有 RoomInstanceGrid2D
  ↓
RoomController.OnAfterRunUpdate() → 遍历并包装成 Room 对象
  ├── foreach (RoomInstanceGrid2D roomInstance)
  │   └── Room room = new Room() { roomInstanceGrid2D = roomInstance }
  └── 根据 (roomInstance.Room as CustomRoom).RoomType 初始化房间:
      ├── BirthRoom → 设置玩家出生位置
      ├── EnemyRoom → SpawnEnemies(isElite=false)
      ├── EliteEnemyRoom → SpawnEnemies(isElite=true)
      ├── TreasureRoom → CreateOtherTreasureBox()
      └── BossRoom → AddBoss()
```

---

## 关卡系统 (BigStage / SmallStage / Stage)

### Stage 计算公式

**位置:** `Assets/Script/Command/MemoryModelCommand.cs`

```csharp
// MemoryModel.Stage 为主数据 (默认值 5)
int Stage = MemoryModel.Stage;  // 1-∞

// BigStage (大关/区域)
int BigStage = (Stage - 1) / 5 + 1;
// 示例:
// Stage 1-5 → BigStage 1
// Stage 6-10 → BigStage 2
// Stage 11-15 → BigStage 3

// SmallStage (小关/关卡内编号)
int SmallStage = Stage - (BigStage - 1) * 5;
// 示例:
// Stage 1 → SmallStage 1
// Stage 5 → SmallStage 5
// Stage 6 → SmallStage 1
```

### 区域显示名称

**位置:** `Assets/Script/Command/MemoryModelCommand.cs:42`

```csharp
public string GetAreaDisplayName() {
    switch (GetBigStage()) {
        case 1:
            return "教学楼";   // BigStage 1
        case 2:
            return "天台";     // BigStage 2
        case 3:
            return "储物间";   // BigStage 3
        default:
            return "第" + GetBigStage() + "区";  // BigStage 4+
    }
}
```

### 关卡显示格式

**位置:** `Assets/Script/Command/MemoryModelCommand.cs:34`

```csharp
public string GetStageDisplayName() {
    return GetBigStage() + "-" + GetSmallStage();
    // 示例: "1-5", "2-3", "3-1"
}
```

### 关卡与地牢生成的关系

**位置:** `Assets/Script/AboutRoom/ScriptableObject/GungeonCustomInput.cs:20`

```csharp
protected override LevelDescriptionGrid2D GetLevelDescription() {
    if (MemoryModelCommand.Instance.GetSmallStage() == 5) {
        // SmallStage == 5 → Boss 关
        selectLevelGraph = roomConfig.LevelGraphBoss;
    } else {
        // SmallStage 1-4 → 普通关卡
        if (!roomConfig.UseRandomLevelGraph) {
            selectLevelGraph = roomConfig.LevelGraph;
        } else {
            // 随机选择 LevelGraph (如果配置了多个)
            selectLevelGraph = roomConfig.levelGraphs[Random.Next(...)];
        }
    }

    // 根据 selectLevelGraph 生成房间布局
    foreach (var room in selectLevelGraph.Rooms) {
        levelDescription.AddRoom(room, roomConfig.RoomTemplates.GetRoomTemplates(room));
    }

    // 添加走廊
    foreach (var connection in selectLevelGraph.Connections) {
        levelDescription.AddCorridorConnection(connection, corridorRoom, ...);
    }

    // 随机添加额外房间
    AddExtraRoom(levelDescription, RoomType.EnemyRoom, ...);
    AddExtraRoom(levelDescription, RoomType.TreasureRoom, ...);

    return levelDescription;
}
```

**关键规则:**
- **SmallStage 1-4:** 使用 `LevelGraph.asset` (普通关卡布局)
- **SmallStage 5:** 使用 `LevelGraphBoss.asset` (Boss 关布局)
- Boss 关通常只有一个 BossRoom,没有额外房间

---

## 房间/地点系统现状

### 当前项目中已实现的房间类型

基于 `RoomType` 枚举和代码引用分析:

| RoomType | 名称 | 是否实现 | 用途 | 触发逻辑位置 |
|----------|------|---------|------|------------|
| **BirthRoom** | 出生房间 | ✅ 实现 | 玩家出生点 | RoomController.cs:51 |
| **EnemyRoom** | 普通敌人房间 | ✅ 实现 | 清理敌人,掉落白色宝箱 | RoomController.cs:55 |
| **EliteEnemyRoom** | 精英敌人房间 | ✅ 实现 | 多波精英敌人,掉落白色宝箱 | RoomController.cs:59 |
| **BossRoom** | Boss 房间 | ✅ 实现 | Boss 战,播放过场动画 | RoomController.cs:68 |
| **TreasureRoom** | 宝箱房间 | ✅ 实现 | 固定生成蓝色/棕色宝箱 | RoomController.cs:63 |
| **Corridor** | 走廊 | ✅ 实现 | 连接房间的通道 | GungeonCustomInput.cs:49 |
| **TeleportRoom** | 传送房间 | ⚠️ 部分实现 | 传送到其他房间/关卡(逻辑未确认) | GungeonCustomInput.cs:86 |
| **SecretRoom** | 秘密房间 | ❌ 未实现 | 配置存在,但生成逻辑未启用 | RoomTamplatesConfig.cs:57 |
| **ShopRoom** | 商店房间 | ❌ 未实现 | 配置存在,但生成逻辑未启用 | RoomTamplatesConfig.cs:46 |

**房间模板配置:**
- 位置: `Assets/Script/AboutRoom/RoomTamplatesConfig.cs`
- 每个 `RoomType` 对应一个 `GameObject[] Templates` 数组
- 实际模板 Prefab 存储在 `Assets/Resources/Prefabs/Room/` (推测路径)

---

### 当前项目中出现的"地点"概念

**区域名称 (AreaDisplayName):**
| BigStage | 区域名称 | 英文推测 | 显示时机 |
|----------|---------|---------|---------|
| 1 | 教学楼 | Classroom / School Building | 进入 BigStage 1 时显示 2 秒 |
| 2 | 天台 | Rooftop | 进入 BigStage 2 时显示 2 秒 |
| 3 | 储物间 | Storage Room | 进入 BigStage 3 时显示 2 秒 |
| 4+ | 第X区 | Area X | 进入 BigStage 4+ 时显示 2 秒 |

**显示逻辑:**
- 位置: `Assets/Script/Panel/BattleScene/PanelBattle.cs:111`
- 当 `BigStage` 变化时,调用 `ShowAreaName()`
- `TextArea.text = MemoryModelCommand.GetAreaDisplayName()`
- 显示 2 秒后自动隐藏

**未确认的地点信息:**
- ❌ 未找到"走廊 / 教室 / 储物间 / 操场"等作为**房间名称**的引用
- ❌ 这些中文词汇仅存在于区域名称,非房间类型
- ⚠️ 可能在美术资源命名或 Prefab 命名中存在,但代码中未体现

---

## 房间连接与地图结构

### 地图生成流程 (Edgar 插件)

**核心工具:** Edgar for Unity (第三方地牢生成插件)

**LevelGraph 结构:**
- 位置: ScriptableObject `LevelGraph.asset` / `LevelGraphBoss.asset`
- 定义: 房间节点 + 连接边
- 格式:
```
LevelGraph:
  Rooms:
    - CustomRoom { RoomType: BirthRoom }
    - CustomRoom { RoomType: EnemyRoom }
    - CustomRoom { RoomType: EnemyRoom }
    - CustomRoom { RoomType: EliteEnemyRoom }
    - CustomRoom { RoomType: TreasureRoom }
  Connections:
    - { From: BirthRoom, To: EnemyRoom1 }
    - { From: EnemyRoom1, To: EnemyRoom2 }
    - { From: EnemyRoom2, To: EliteEnemyRoom }
    - { From: EliteEnemyRoom, To: TreasureRoom }
```

**生成步骤:**
```
GungeonCustomInput.GetLevelDescription()  [GungeonCustomInput.cs:16]
  ↓
1. 选择 LevelGraph (普通 / Boss)
  ↓
2. AddRoom() 添加所有 Rooms 到 levelDescription  [行:40]
  ↓
3. AddCorridorConnection() 为每条 Connection 添加走廊  [行:49]
  ↓
4. AddExtraRoom() 随机添加额外房间 (EnemyRoom/TreasureRoom)  [行:59-60]
  ├── GetPossibleRoomAttachToEnemyRoom() 获取可连接的房间  [行:125]
  │   └── 条件: 非 BirthRoom/TeleportRoom, 且符合死胡同概率
  └── AddCorridorConnection() 连接到选中的房间
  ↓
5. DungeonGeneratorGrid2D.GenerateCoroutine() 生成物理房间
  ├── 随机选择 RoomTemplate Prefab
  ├── 摆放房间到网格中 (避免重叠)
  └── 生成走廊连接房间
  ↓
6. RoomPostProcessing.GetRoomInstances() 获取所有生成的房间实例
```

**房间连接表示:**
- 每个 `RoomInstanceGrid2D` 有 `Doors` 属性
- `DoorInstanceGrid2D` 记录:
  - `ConnectedRoomInstance` - 连接到的房间
  - 门的位置和方向 (Top/Bottom/Left/Right)

**门控制逻辑:**
- 位置: `Assets/Script/Controller/RoomController.cs:137`
```csharp
CloseDoor(RoomInstanceGrid2D roomInstance) {
    foreach (DoorInstanceGrid2D door in roomInstance.Doors) {
        GameObject roomObj = door.ConnectedRoomInstance.RoomTemplateInstance;
        SetDoorAnimator(roomObj, isUp=true);  // 门升起 (关闭)
    }
    player.m_Attr.isBattle = true;
}

OpenDoor(RoomInstanceGrid2D roomInstance) {
    foreach (DoorInstanceGrid2D door in roomInstance.Doors) {
        GameObject roomObj = door.ConnectedRoomInstance.RoomTemplateInstance;
        SetDoorAnimator(roomObj, isUp=false);  // 门降下 (打开)
    }
    player.m_Attr.isBattle = false;
}
```

**门类型 (动画命名):**
- `LeftVerDownDoor` - 左侧垂直门
- `RightVerDownDoor` - 右侧垂直门
- `TopHorDownDoor` - 顶部水平门
- `BottomHorDownDoor` - 底部水平门

---

### 房间连接关系现状

**实现状态:** ✅ 已实现 (通过 Edgar 插件)

**连接方式:**
- 房间之间通过 **走廊 (Corridor)** 连接
- 走廊是独立的 RoomType,有自己的模板 Prefab
- 门动画控制玩家是否能通过连接

**地图拓扑:**
- Edgar 插件根据 LevelGraph 自动生成拓扑结构
- 支持多种布局 (线性/分支/环形)
- 具体拓扑由 `LevelGraph.asset` 配置决定 (需查看 Inspector)

**未确认的连接信息:**
- ❓ LevelGraph 的具体节点和边配置 (需在 Unity Editor 中查看 `.asset` 文件)
- ❓ 是否有多个 LevelGraph 变体 (如 `LevelGraph 1.asset`)
- ❓ 随机 LevelGraph 的启用条件 (`roomConfig.UseRandomLevelGraph` 的值)

---

## 特殊房间系统

### TeleportRoom (传送房间)

**代码位置:** `Assets/Script/AboutRoom/ScriptableObject/GungeonCustomInput.cs:86`

**生成逻辑:**
```csharp
if (secretRoom.RoomType == RoomType.TeleportRoom) {
    // 传送房间连接到两个房间 (形成环)
    var connection1 = new CustomConnection { From: roomToAttachTo.room, To: secretRoom };
    levelDescription.AddCorridorConnection(connection1, corridorRoom, ...);

    CustomRoom secondRoom = GetRoomWhichNeighborsLessThanFour().room;
    var connection2 = new CustomConnection { From: secondRoom, To: secretRoom };
    levelDescription.AddCorridorConnection(connection2, corridorRoom, ...);
}
```

**用途推测:**
- 连接两个原本不相邻的房间
- 形成快捷通道或循环路径

**未确认信息:**
- ❓ TeleportRoom 的实际生成概率 (当前代码中未启用)
- ❓ 传送功能是否实现 (需查看 `Assets/Script/NeedMono/Item/Portal.cs`)

---

### SecretRoom (秘密房间)

**配置位置:** `Assets/Script/AboutRoom/RoomTamplatesConfig.cs:57`

**生成逻辑:** ❌ 当前未调用 `AddExtraRoom(RoomType.SecretRoom, ...)`

**预期用途:**
- 隐藏房间,需要特殊条件触发 (如炸墙)
- 奖励丰厚 (稀有武器/大量金币)

**实现建议:**
- 需在 `GungeonCustomInput.GetLevelDescription()` 中添加:
```csharp
AddExtraRoom(levelDescription, RoomType.SecretRoom, ..., roomConfig.SecretRoomChance);
```

---

### ShopRoom (商店房间)

**配置位置:** `Assets/Script/AboutRoom/RoomTamplatesConfig.cs:46`

**生成逻辑:** ❌ 当前未调用 `AddExtraRoom(RoomType.ShopRoom, ...)`

**预期用途:**
- NPC 商人,出售武器/道具
- 不生成敌人,安全区

**实现建议:**
- 需在 `GungeonCustomInput.GetLevelDescription()` 中添加:
```csharp
AddExtraRoom(levelDescription, RoomType.ShopRoom, ..., roomConfig.ShopRoomChance);
```

---

## 房间数据流

### 房间配置 → 生成 → 控制 链条

```
1. 配置阶段 (Editor / ScriptableObject)
   ↓
IRoomConfig (ScriptableObject)
  ├── LevelGraph (ScriptableObject)
  │   ├── Rooms: List<CustomRoom>  { RoomType }
  │   └── Connections: List<CustomConnection>  { From, To }
  ├── LevelGraphBoss (ScriptableObject)
  └── RoomTemplates (RoomTamplatesConfig)
      ├── BirthRoomTemplates: GameObject[]
      ├── EnemyRoomTemplates: GameObject[]
      └── ... (每个 RoomType 的模板)
      ↓
2. 运行时生成阶段
   ↓
BattleScene 加载
  ↓
GameLoopBattle.Start()
  ├── DungeonGeneratorGrid2D.CustomInputTask = GungeonCustomInput
  └── CoroutinePool.StartCoroutine(Generate())
      ↓
GungeonCustomInput.GetLevelDescription()  [GungeonCustomInput.cs:16]
  ├── 根据 SmallStage 选择 LevelGraph
  ├── AddRoom() / AddCorridorConnection()
  └── return LevelDescriptionGrid2D
      ↓
DungeonGeneratorGrid2D.GenerateCoroutine()  [Edgar 插件]
  ├── 根据 LevelDescription 生成房间
  ├── 随机选择 RoomTemplate Prefab
  ├── 摆放房间到网格 (Tilemap)
  └── return List<RoomInstanceGrid2D>
      ↓
RoomPostProcessing.GetRoomInstances()  [RoomPostProcessing.cs]
  └── return List<RoomInstanceGrid2D>
      ↓
3. 运行时控制阶段
   ↓
RoomController.OnAfterRunUpdate()  [RoomController.cs:33]
  ├── RoomInstances = GetRoomInstances()
  └── foreach (RoomInstanceGrid2D roomInstance in RoomInstances)
      ├── Room room = new Room() { roomInstanceGrid2D = roomInstance }
      └── switch ((roomInstance.Room as CustomRoom).RoomType)
          ├── BirthRoom → SetPlayerPosition()
          ├── EnemyRoom → SpawnEnemies(room, isElite=false)
          ├── EliteEnemyRoom → SpawnEnemies(room, isElite=true)
          ├── TreasureRoom → CreateOtherTreasureBox(room)
          ├── BossRoom → AddBoss(room)
          └── 注册触发器 (OnTriggerEnter/Exit)
              ↓
玩家进入房间
  ↓
TriggerCenter.NotisfyObserver(OnTriggerEnter, player, FloorCollider)
  ↓
RoomController.AlwaysUpdate()  [RoomController.cs:92]
  ├── isEnterEnemyFloor = true
  ├── IsPlayerInFloor() → true
  ├── CloseDoor(roomInstance)  // 关门
  └── if (CurrentEnemyNum == 0 && WaveNum == 0):
      ├── CreateWhiteTreasureBox()
      ├── ShowBattleFinishAnim()
      ├── OpenDoor(roomInstance)  // 开门
      └── RemoveObserver()
```

---

## 关卡推进逻辑 (推测)

### 未确认的关卡推进代码

❌ **未找到 `MemoryModel.Stage++` 的调用位置**

**推测触发点:**
1. Boss 死亡后
2. 完成所有房间清理
3. 进入传送门 (如果存在)

**推测代码位置:**
- `Assets/Script/Character/Boss/*.cs` 的 `OnDie()` 方法
- `Assets/Script/NeedMono/Item/Portal.cs` 的传送逻辑

**推测流程:**
```
Boss 死亡
  ↓
IBoss.OnDie()  (推测)
  ├── EventCenter.NotisfyObserver(EventType.OnBossDie, boss)
  └→ 某个监听器:
      ├── MemoryModelCommand.AddStage(1)  (推测方法,需添加)
      │   └── MemoryModel.Stage++
      ├── 显示 "关卡完成" UI
      ├── 掉落宝箱/奖励
      └── 生成传送门 → 点击后 ReloadActiveScene() 或 LoadScene(MiddleScene)
```

**确认方法:**
需查看 `Assets/Script/Character/Boss/` 目录下的 Boss 实现类。

---

## 改造建议 (基于需求: 学校地图房间连接)

### 需求理解
- **目标:** 实现"学校地图房间连接 + 天台出生 + 教学楼 1-5 入口映射"
- **解读:**
  - 当前系统使用 Edgar 自动生成随机地牢
  - 需求是固定布局的学校地图 (天台 → 教学楼 → 各个教室)
  - 这需要从 **程序生成 (Procedural Generation)** 转向 **手工关卡设计 (Handcrafted Levels)**

### 冲突点分析

| 当前系统特性 | 需求特性 | 冲突程度 |
|------------|---------|---------|
| Edgar 随机生成地牢 | 固定学校地图布局 | ⚠️ 中等 |
| RoomType 类型标识 | 具体房间名称 (如"3-1教室") | ⚠️ 中等 |
| 走廊自动连接 | 固定走廊/楼梯连接 | ⚠️ 中等 |
| BigStage 区域名称 | 具体楼层/区域映射 | ✅ 兼容 |
| 关卡推进系统 | 需补充实现 | ❌ 高 |

### 改造路线建议

**方案 1: 预设 LevelGraph (推荐,低风险)**
- 在 Unity Editor 中手工设计 LevelGraph
- 为每个 BigStage 创建对应的 LevelGraph (如 `LevelGraphSchool.asset`)
- 房间名称通过 `CustomRoom.name` 字段标识
- 保留 Edgar 生成,但使用固定的 LevelGraph

**实现步骤:**
1. 创建 `LevelGraphSchool1.asset` (教学楼)
2. 添加房间节点: BirthRoom (天台), EnemyRoom (教室 1-5), Corridor (走廊/楼梯)
3. 手工连接节点 (天台 → 走廊 → 教室 1 → ... → 教室 5)
4. 在 `GungeonCustomInput` 中根据 `BigStage == 1` 加载 `LevelGraphSchool1`

**优点:**
- 无需修改核心生成逻辑
- 保留 Edgar 的碰撞体/寻路生成
- 灵活性高,可随时调整布局

**缺点:**
- 房间位置仍有随机性 (Edgar 的摆放算法)
- 无法精确控制房间坐标

---

**方案 2: 禁用 Edgar,使用预制场景 (高风险,彻底改造)**
- 为每个 BigStage 创建独立的 Unity Scene
- 手工摆放房间 Prefab 到固定位置
- 修改 `RoomController` 直接加载场景而非生成

**实现步骤:**
1. 创建 `SchoolMap.unity` 场景
2. 手工摆放房间 Prefab (天台/教室/走廊)
3. 修改 `SceneModelCommand.LoadScene()` 根据 BigStage 加载对应场景
4. 移除 Edgar 依赖

**优点:**
- 完全控制布局,精确到坐标
- 可使用 Unity 场景编辑器的全部功能

**缺点:**
- 需要重写大量代码 (RoomController/生成逻辑)
- 失去程序生成的灵活性
- 工作量大,风险高

---

**推荐方案:** 方案 1 (预设 LevelGraph)

**理由:**
- 最小化代码修改
- 保留现有系统优势
- 风险可控,可逐步迁移

---

## 总结

### 1. RoomList/RoomPack/RoomCode 系统
- **角色:** 联机大厅系统
- **数据源:** Protobuf 网络协议
- **单机关系:** 完全无关

### 2. 地牢房间系统 (RoomType / CustomRoom / Room)
- **角色:** 游戏内地图生成
- **数据源:** Edgar 插件 + LevelGraph ScriptableObject
- **实现状态:** ✅ 核心功能完整 (BirthRoom / EnemyRoom / EliteEnemyRoom / BossRoom / TreasureRoom / Corridor)

### 3. 关卡系统 (Stage / BigStage / SmallStage)
- **角色:** 进度管理
- **数据源:** MemoryModel.Stage
- **实现状态:** ⚠️ 部分实现 (显示逻辑完整,推进逻辑未确认)

### 4. 地点/区域名称
- **角色:** UI 显示
- **数据源:** 硬编码 (教学楼/天台/储物间)
- **实现状态:** ✅ 完整
- **限制:** 仅作为区域名称,非房间类型

### 5. 房间连接关系
- **角色:** 地图拓扑
- **数据源:** Edgar LevelGraph + 自动生成
- **实现状态:** ✅ 完整 (通过走廊连接,门动画控制)

### 6. 未实现的房间类型
- ❌ SecretRoom (配置存在,未启用)
- ❌ ShopRoom (配置存在,未启用)
- ⚠️ TeleportRoom (代码存在,未确认功能)

### 7. 改造学校地图的最佳路线
1. 创建预设 LevelGraph (手工设计固定布局)
2. 为每个 BigStage 配置对应的 LevelGraph
3. 补充关卡推进逻辑 (Boss 死亡 → Stage++)
4. 可选: 扩展 RoomType 添加具体房间名称 (如 ClassroomType 枚举)
