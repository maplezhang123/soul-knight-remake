# Soul-Knight 项目地图

## 场景列表与用途

项目包含4个 Unity 场景:

| 场景文件 | 索引 | 场景名称枚举 | 用途 | 入口脚本 |
|---------|------|------------|------|---------|
| `MainMenuScene.unity` | 0 | MainMenuScene | 主菜单：角色登录、单机/联机选择、设置 | `GameLoopZero.cs` |
| `MiddleScene.unity` | 1 | MiddleScene | 角色选择与准备：选角色、选宠物、背包、商店 | `GameLoop.cs` |
| `Room1.unity` (实为 BattleScene) | 2 | BattleScene | 战斗场景：地牢生成、战斗、房间探索 | `GameLoopBattle.cs` |
| `OnlineStartRoom.unity` | 3 | OnlineStartScene | 在线多人等待室(联机模式) | `GameLoopOnlineStart.cs` |

**场景切换机制:**
- 统一通过 `SceneModelCommand.Instance.LoadScene(SceneName)` 切换
- 位置: `Assets/Script/Command/SceneModelCommand.cs`
- 内部使用 `SceneManager.LoadSceneAsync` 并触发 `OnSceneChangeComplete` 事件

---

## 核心脚本目录结构

```
Assets/Script/
├── AboutRoom/                  # 房间生成与配置
│   ├── ScriptableObject/       # 房间配置 SO
│   │   ├── GungeonCustomInput.cs      # 地牢生成自定义输入(核心)
│   │   ├── IRoomConfig.cs             # 房间配置接口
│   │   └── ForestRoomConfig.cs        # 森林主题房间配置
│   ├── CustomRoom.cs           # 自定义房间类(RoomType)
│   ├── RoomTamplatesConfig.cs  # 房间模板映射配置
│   └── RoomPostProcessing.cs   # 房间后处理

├── Attribute/                  # 属性系统
│   ├── ShareAttribute/         # 共享属性(PlayerAttr/EnemyAttr)
│   └── Strategy/               # 属性策略模式

├── Buff/                       # Buff 系统
│   ├── BuffBurn.cs             # 灼烧
│   ├── BuffFreeze.cs           # 冻结
│   ├── BuffDizzy.cs            # 眩晕
│   └── BuffPoisoning.cs        # 中毒

├── Character/                  # 角色实体
│   ├── Player/                 # 玩家角色类(Knight/Rogue/Wizard...)
│   ├── Enemy/                  # 敌人类
│   ├── Boss/                   # Boss类
│   ├── Pet/                    # 宠物类
│   ├── IPlayer.cs              # 玩家接口
│   ├── IEnemy.cs               # 敌人接口
│   └── IBoss.cs                # Boss接口

├── Command/                    # 命令模式(Command Pattern)
│   ├── SceneModelCommand.cs    # 场景切换命令
│   ├── MemoryModelCommand.cs   # 内存数据命令(关卡/金钱)
│   ├── PlayerCommand.cs        # 玩家数据查询
│   ├── EnemyCommand.cs         # 敌人数据查询
│   ├── WeaponCommand.cs        # 武器数据查询
│   ├── BossCommand.cs          # Boss数据查询
│   ├── ArchiveCommand.cs       # 存档命令
│   └── LanguageCommand.cs      # 语言命令

├── Controller/                 # 控制器(MVC-Controller)
│   ├── AbstractController.cs   # 控制器基类
│   ├── PlayerController.cs     # 玩家控制器
│   ├── EnemyController.cs      # 敌人控制器
│   ├── RoomController.cs       # 房间控制器(生成敌人/管理门)
│   ├── UIController.cs         # UI 控制器
│   ├── ItemController.cs       # 物品控制器
│   ├── BuffController.cs       # Buff 控制器
│   ├── SceneController.cs      # 场景控制器
│   └── InputController.cs      # 输入控制器

├── Facade/                     # 外观模式(Facade Pattern)
│   ├── IGameFacade.cs          # Facade 基类
│   ├── MainMenu/GameFacade.cs  # MainMenuScene Facade
│   ├── MiddleScene/GameFacade.cs   # MiddleScene Facade
│   └── Battle/GameFacade.cs    # BattleScene Facade

├── Factory/                    # 工厂模式
│   ├── AttributeFactory.cs     # 属性工厂
│   ├── PlayerFactory.cs        # 玩家工厂
│   ├── EnemyFactory.cs         # 敌人工厂
│   ├── WeaponFactory.cs        # 武器工厂
│   ├── SkillFactory.cs         # 技能工厂
│   └── ResourceFactory/        # 资源工厂(Proxy模式)

├── GameFacade/                 # 游戏外观(未确认用途)
├── GameLoop/                   # 场景入口
│   ├── GameLoop.cs             # MiddleScene 入口
│   ├── GameLoopZero.cs         # MainMenuScene 入口
│   ├── GameLoopBattle.cs       # BattleScene 入口
│   └── GameLoopOnlineStart.cs  # OnlineStartScene 入口

├── Items/                      # 物品系统
│   ├── Bullet/                 # 子弹(玩家/敌人)
│   ├── Item/                   # 掉落物品(金币等)
│   ├── Laser/                  # 激光
│   ├── SwordLight/             # 刀光
│   ├── Effect/                 # 特效
│   └── BoomEffect/             # 爆炸特效

├── Mediator/                   # 中介者模式
│   └── GameMediator.cs         # 全局中介者(注册/获取Controller/System)

├── Model/                      # 数据模型(MVC-Model)
│   ├── AbstractModel.cs        # 模型基类
│   ├── SceneModel.cs           # 场景数据
│   ├── MemoryModel.cs          # 运行时数据(选中角色/关卡/金钱)
│   ├── PlayerModel.cs          # 玩家属性列表
│   ├── EnemyModel.cs           # 敌人属性列表
│   ├── BossModel.cs            # Boss 属性列表
│   ├── ArchiveModel.cs         # 存档数据
│   ├── PlayerInputModel.cs     # 玩家输入数据
│   ├── PlantModel.cs           # 植物数据
│   └── WeaponModel.cs          # 武器数据
├── ModelContainer.cs           # Model 容器(单例,初始化所有Model)

├── MultiPlayer/                # 联机系统(Protobuf)
│   ├── SoulKnightProtocol.cs   # Protobuf 协议定义
│   ├── ClientFacade.cs         # 客户端 Facade
│   ├── Manager/                # 请求管理器
│   └── Request/                # 各类请求(Login/CreateRoom/JoinRoom...)

├── NeedMono/                   # 需要 MonoBehaviour 的杂项
│   ├── TreasureBox/            # 宝箱
│   ├── Item/                   # 交互物品(保险箱/铁匠台/幸运猫/传送门/冰箱)
│   ├── Garden/                 # 花园
│   ├── ToBattleRoom.cs         # 进入战斗房间触发器
│   └── TriggerDetection.cs     # 触发器检测

├── Panel/                      # UI 面板
│   ├── IPanel.cs               # 面板基类
│   ├── MainMenuScene/          # 主菜单场景面板
│   ├── MiddleScene/            # 角色选择场景面板
│   └── BattleScene/            # 战斗场景面板

├── Pool/                       # 对象池
├── StateMathine/               # 状态机(Enemy状态)
├── System/                     # 系统层
│   ├── AbstractSystem.cs       # 系统基类
│   ├── AudioSystem.cs          # 音频系统
│   ├── TalentSystem.cs         # 天赋系统
│   └── BackpackSystem.cs       # 背包系统

├── Utility/                    # 工具类
│   ├── EventCenter.cs          # 事件中心
│   ├── TriggerCenter.cs        # 触发器中心
│   └── UnityTool.cs            # Unity 工具方法

├── GameEnum.cs                 # 全局枚举定义
└── CoroutinePool.cs            # 协程池
```

---

## 程序入口与初始化流程

### 1. MainMenuScene 入口

**启动顺序:**
```
MainMenuScene.unity 加载
  ↓
GameLoopZero.Start()              [Assets/Script/GameLoop/GameLoopZero.cs:8]
  ↓
new MainMenuScene.GameFacade()    [Assets/Script/Facade/MainMenu/GameFacade.cs:7]
  ├── new UIController()
  ├── RegisterController(UIController)
  └── RegisterSystem(AudioSystem)
  ↓
GameLoopZero.Update()
  ↓
GameFacade.GameUpdate()           [每帧调用]
  └── UIController.GameUpdate()
      ├── SceneController.GameUpdate()  (场景切换)
      └── InputController.GameUpdate()  (输入处理)
```

**关键 UI 入口:**
- `PanelRoot` 在 `UIController.OnInit()` 时创建
- 位置: `Assets/Script/Panel/MainMenuScene/PanelRoot.cs:21`
- 按钮绑定:
  - `ButtonSinglePlayer` → 加载 MiddleScene (行:80)
  - `ButtonMultiPlayer` → 打开 PanelOnlineAlert (行:84)

---

### 2. MiddleScene 入口

**启动顺序:**
```
MiddleScene.unity 加载
  ↓
MiddleScene.GameLoop.Start()      [Assets/Script/GameLoop/GameLoop.cs:9]
  ↓
new MiddleScene.GameFacade()      [Assets/Script/Facade/MiddleScene/GameFacade.cs:11]
  ├── new PlayerController()
  ├── new EnemyController()
  ├── new UIController()
  ├── new ItemController()
  ├── new BuffController()
  ├── RegisterSystem(AudioSystem)
  ├── RegisterSystem(TalentSystem)
  └── RegisterSystem(BackpackSystem)
  ↓
等待用户点击角色
  ↓
EventCenter.NotisfyObserver(EventType.OnFinishSelectPlayer)
  ↓
TurnOnController() → 激活 PlayerController/EnemyController
```

**关键 UI 入口:**
- `PanelRoot` → `PanelRoom` (行:27)
- `PanelRoom` 监听 `OnPlayerClick` 事件 (行:31)
- 点击角色后打开 `PanelSelectPlayer`

**进入战斗的按钮位置:**
- 未确认: 需查看 `PanelSelectPlayer` 或场景中的 "开始游戏" 按钮

---

### 3. BattleScene 入口

**启动顺序:**
```
BattleScene.unity (Room1.unity) 加载
  ↓
BattleScene.GameLoopBattle.Start()    [Assets/Script/GameLoop/GameLoopBattle.cs:18]
  ├── 获取 DungeonGeneratorGrid2D 组件
  ├── 设置 GungeonCustomInput
  ├── StartCoroutine(Generate())      地牢生成协程
  └── new BattleScene.GameFacade()    [Assets/Script/Facade/Battle/GameFacade.cs:11]
      ├── new ItemController()
      ├── new UIController()
      ├── new RoomController()         **核心房间控制器**
      ├── new PlayerController()
      ├── new EnemyController()
      ├── new BuffController()
      ├── RegisterSystem(AudioSystem)
      └── RegisterSystem(TalentSystem)
  ↓
Generate() 完成
  ↓
SetTilemaps() → 设置碰撞体和图层
  ↓
AStarPath.Scan() → 寻路网格扫描
  ↓
EventCenter.NotisfyObserver(EventType.OnFinishRoomGenerate)
  ↓
TurnOnController() → 激活所有 Controller
  ↓
RoomController.OnAfterRunUpdate()     [Assets/Script/Controller/RoomController.cs:33]
  ├── 获取所有 RoomInstances
  ├── 遍历每个房间:
  │   ├── BirthRoom → 设置玩家出生位置 (行:51)
  │   ├── EnemyRoom → SpawnEnemies() (行:55)
  │   ├── EliteEnemyRoom → SpawnEnemies(isElite=true) (行:59)
  │   ├── TreasureRoom → CreateOtherTreasureBox() (行:63)
  │   ├── BossRoom → AddBoss() (行:68)
  │   └── 注册 OnTriggerEnter/Exit 监听器 (行:70-88)
  └── 开始游戏循环
```

**地牢生成核心:**
- `GungeonCustomInput.GetLevelDescription()` 生成房间布局
- 位置: `Assets/Script/AboutRoom/ScriptableObject/GungeonCustomInput.cs:16`
- 根据 `MemoryModel.Stage` 决定使用哪个 LevelGraph:
  - SmallStage == 5 → LevelGraphBoss (Boss关)
  - 否则 → LevelGraph 或随机 LevelGraph

---

## 关键数据流

### ModelContainer 初始化 (全局唯一)
位置: `Assets/Script/ModelContainer.cs`

```
ModelContainer.Instance (构造函数)
  ├── AddModel(new SceneModel())
  ├── AddModel(new MemoryModel())         **运行时关卡/金钱数据**
  ├── AddModel(new PlayerInputModel())
  ├── AddModel(new ArchiveModel())
  ├── AddModel(new PlantModel())
  ├── AddModel(new WeaponModel())         从 CSV 加载武器
  ├── AddModel(new PlayerModel())         从 CSV 加载角色
  ├── AddModel(new EnemyModel())          从 CSV 加载敌人
  └── AddModel(new BossModel())           从 CSV 加载 Boss
```

### Excel → ScriptableObject → Model → Command 链条
```
Assets/Resources/Excel/PlayerData.CSV
  ↓ (Unity Inspector 配置)
Resources.Load<TextAsset>("Excel/PlayerData")
  ↓
PlayerScriptableObject (反序列化)
  ↓
PlayerModel.Init()
  ├── PlayerAttrDic.Add(PlayerType, Attr)
  ↓
PlayerCommand.GetPlayerAttr(PlayerType)
  ↓
AttributeFactory.CreatePlayerAttribute(Attr)
  ↓
PlayerFactory.CreatePlayer(PlayerType)
  ↓
IPlayer 实例
```

---

## 事件系统关键节点

位置: `Assets/Script/Utility/EventCenter.cs`

**核心事件列表:**
- `OnSceneChangeComplete` - 场景切换完成
- `OnFinishSelectPlayer` - 完成角色选择
- `OnFinishRoomGenerate` - 房间生成完成
- `OnPlayerEnterBattleRoom` - 玩家进入战斗房间
- `OnPlayerEnterBossRoom` - 玩家进入 Boss 房间
- `OnCameraArriveAtPlayer` - 摄像机到达玩家
- `OnPlayerClick` - 玩家点击事件
- `OnPetClick` - 宠物点击事件
- `OnEnemyHurt` - 敌人受伤
- `OnPlayerHurt` - 玩家受伤
- `OnPause` / `OnResume` - 暂停/恢复
- `OnWantShowNotice` - 显示通知

**事件中心特性:**
- 支持泛型参数 `NotisfyObserver<T>(EventType, T data)`
- 场景切换时 `ClearObserver()` 清理监听器

---

## 快速定位关键功能

| 功能 | 入口位置 |
|------|---------|
| **单机开始游戏** | `PanelRoot.ButtonSinglePlayer.onClick` → LoadScene(MiddleScene) <br> 位置: `Assets/Script/Panel/MainMenuScene/PanelRoot.cs:80` |
| **联机房间列表** | `PanelRoot.ButtonMultiPlayer.onClick` → EnterPanel(PanelOnlineAlert) <br> 位置: `Assets/Script/Panel/MainMenuScene/PanelRoot.cs:84` |
| **角色选择确认** | `PanelSelectPlayer` (需查看具体按钮) → EventCenter.NotisfyObserver(OnFinishSelectPlayer) |
| **进入战斗** | `PanelSelectPlayer` 或 `PanelRoom` (需查看) → LoadScene(BattleScene) |
| **房间生成逻辑** | `GungeonCustomInput.GetLevelDescription()` <br> 位置: `Assets/Script/AboutRoom/ScriptableObject/GungeonCustomInput.cs:16` |
| **敌人生成逻辑** | `RoomController.SpawnEnemies()` <br> 位置: `Assets/Script/Controller/RoomController.cs:237` |
| **玩家出生位置** | `RoomController.OnAfterRunUpdate()` 遍历 BirthRoom <br> 位置: `Assets/Script/Controller/RoomController.cs:51` |
| **关卡推进** | `MemoryModelCommand.AddStage()` (未找到此方法,需补充) |
| **金钱增加** | `MemoryModelCommand.AddMoney(int)` <br> 位置: `Assets/Script/Command/MemoryModelCommand.cs:8` |

---

## 联机相关标识

### 确认为联机遗留的组件:
- `Assets/Script/MultiPlayer/` 整个目录 (Protobuf 协议)
- `PanelRoomList` - 房间列表面板 (RoomPack/RoomCode 语义)
- `PanelCreateRoom` - 创建房间面板
- `PanelUserList` - 用户列表面板
- `PanelOnlineAlert` - 在线提示面板
- `OnlineStartRoom.unity` - 联机等待室场景
- `MemoryModel.isOnlineMode` - 在线模式标志 (单机未使用)

### 单机复用联机命名的情况:
- **RoomCode 枚举** (WaitForJoin/Playing/Full) 在单机中未使用
- **RoomPack 结构** 仅在联机时通过 Protobuf 传输
- **PanelRoomList** 在单机模式下不可见

**结论:** 联机系统完整但独立,单机玩法完全不依赖联机代码。
