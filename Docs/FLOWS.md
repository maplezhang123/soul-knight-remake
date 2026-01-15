# Soul-Knight 主流程与玩家路径

## 玩家视角流程图

### 完整游戏流程 (文本流程图)

```
[启动游戏]
    ↓
[MainMenuScene - 主菜单]
    ├─→ [点击 ButtonSinglePlayer] → 单机模式
    │       ↓
    │   [MiddleScene - 角色选择场景]
    │       ↓
    │   [点击场景中的角色模型]
    │       ↓
    │   [PanelSelectPlayer 弹出] → 选择皮肤、查看属性
    │       ↓
    │   [点击 "开始游戏" (推测按钮名)]
    │       ↓
    │   [加载 BattleScene]
    │       ↓
    │   [地牢生成中... (协程)]
    │       ↓
    │   [TimeLine 播放入场动画]
    │       ↓
    │   [玩家在 BirthRoom 出生]
    │       ↓
    │   [自由移动探索房间]
    │       ├─→ [进入 EnemyRoom] → 门关闭 → 清理敌人 → 门打开 → 掉落白色宝箱
    │       ├─→ [进入 EliteEnemyRoom] → 多波敌人 → 门打开 → 掉落白色宝箱
    │       ├─→ [进入 TreasureRoom] → 拾取蓝色/棕色宝箱
    │       ├─→ [进入 BossRoom] → 播放 Boss 过场动画 → Boss 战
    │       └─→ [击败 Boss] → 关卡完成 → Stage +1 (推测)
    │       ↓
    │   [角色死亡] → PanelResurrection 弹出 → 选择复活/放弃
    │       ↓
    │   [返回 MainMenuScene 或 MiddleScene]
    │
    └─→ [点击 ButtonMultiPlayer] → 联机模式
            ↓
        [PanelOnlineAlert 弹出] → 登录提示 → PanelLogin
            ↓
        [输入用户名/密码] → 发送 Login 请求
            ↓
        [PanelRoomList] → 搜索房间 / 创建房间
            ↓
        [进入 OnlineStartRoom.unity] → 等待其他玩家
            ↓
        [房主开始游戏] → 加载 BattleScene (多人)
            ↓
        (后续同单机,但同步所有玩家状态)
```

---

## 代码视角调用链

### 1. 启动 → 主菜单

```
Unity 启动
  ↓
SceneManager 加载 MainMenuScene.unity
  ↓
GameLoopZero.Start()                     [GameLoop/GameLoopZero.cs:8]
  ├── Time.timeScale = 1
  └── new MainMenuScene.GameFacade()     [Facade/MainMenu/GameFacade.cs:7]
      ├── new UIController()
      │   └── UIController.OnInit()
      │       └── new PanelRoot()        [Panel/MainMenuScene/PanelRoot.cs:21]
      │           ├── new PanelLogin(this)
      │           ├── new PanelOnlineAlert(this)
      │           └── new PanelSetting(this)
      └── RegisterSystem(AudioSystem)
  ↓
GameLoopZero.Update() [每帧]
  └── GameFacade.GameUpdate()
      └── UIController.GameUpdate()
          ├── SceneController.GameUpdate()
          └── InputController.GameUpdate()
```

**UI 交互:**
```
PanelRoot.OnInit()                       [Panel/MainMenuScene/PanelRoot.cs:30]
  ├── ButtonSinglePlayer.onClick → SceneModelCommand.LoadScene(SceneName.MiddleScene)  [行:80]
  ├── ButtonMultiPlayer.onClick → EnterPanel(typeof(PanelOnlineAlert))                [行:84]
  ├── ButtonLogin.onClick → EnterPanel(typeof(PanelLogin))                             [行:88]
  └── ButtonSetting.onClick → EnterPanel(typeof(PanelSetting))                         [行:76]
```

---

### 2. 主菜单 → 角色选择

```
用户点击 ButtonSinglePlayer
  ↓
SceneModelCommand.LoadScene(SceneName.MiddleScene)  [Command/SceneModelCommand.cs:20]
  └── SceneManager.LoadSceneAsync((int)SceneName.MiddleScene)
      ↓ (异步加载)
OnSceneChange(AsyncOperation)            [Command/SceneModelCommand.cs:33]
  ├── sceneModel.SetValue()
  ├── EventCenter.NotisfyObserver(EventType.OnSceneChangeComplete)
  └── EventCenter.ClearObserver()
      ↓
MiddleScene.unity 加载完成
  ↓
MiddleScene.GameLoop.Start()             [GameLoop/GameLoop.cs:9]
  ├── Time.timeScale = 1
  └── new MiddleScene.GameFacade()       [Facade/MiddleScene/GameFacade.cs:11]
      ├── new PlayerController()
      ├── new EnemyController()
      ├── new UIController()
      │   └── new PanelRoot()            [Panel/MiddleScene/PanelRoot.cs:9]
      │       └── new PanelRoom(this)    [行:12]
      │           ├── new PanelGemStore(this)
      │           ├── new PanelSelectPlayer(this)
      │           └── new PanelSelectPet(this)
      ├── new ItemController()
      ├── new BuffController()
      ├── RegisterSystem(AudioSystem)
      ├── RegisterSystem(TalentSystem)
      └── RegisterSystem(BackpackSystem)
  ↓
PanelRoot.OnInit()                       [Panel/MiddleScene/PanelRoot.cs:14]
  ├── OnResume() → m_GameObject.SetActive(true)
  ├── 注册 OnFinishSelectPlayer 事件 → m_GameObject.SetActive(false)  [行:19]
  └── OnEnter()
      └── EnterPanel(typeof(PanelRoom))  [行:27]
```

**角色选择交互:**
```
PanelRoot.OnUpdate() [每帧]             [Panel/MiddleScene/PanelRoot.cs:30]
  ├── 检测鼠标点击
  ├── Physics2D.CircleCast() 检测 LayerMask "Player"
  └── 如果命中角色:
      └── EventCenter.NotisfyObserver(EventType.OnPlayerClick, hit.collider.gameObject)  [行:42]
          ↓
PanelRoom 监听到 OnPlayerClick           [Panel/MiddleScene/PanelRoom.cs:31]
  ├── EnterPanel(typeof(PanelSelectPlayer))
  └── DisappearAnim() → DivBottom/Title 动画隐藏
      ↓
PanelSelectPlayer 打开 (具体逻辑需查看该文件)
  ├── 显示角色属性、技能
  ├── 选择皮肤
  └── [点击 "开始游戏" 按钮 - 未确认位置]
      ↓
EventCenter.NotisfyObserver(EventType.OnFinishSelectPlayer)
  ↓
MiddleScene.GameFacade 监听器触发      [Facade/MiddleScene/GameFacade.cs:31]
  ├── m_BuffController.TurnOffController()
  ├── m_PlayerController.TurnOnController()
  └── m_EnemyController.TurnOnController()
      ↓
[推测] 用户点击 "进入地牢" 按钮
  ↓
SceneModelCommand.LoadScene(SceneName.BattleScene)
```

---

### 3. 角色选择 → 战斗场景

```
SceneManager.LoadSceneAsync(BattleScene)
  ↓
Room1.unity (BattleScene) 加载完成
  ↓
GameLoopBattle.Start()                   [GameLoop/GameLoopBattle.cs:18]
  ├── Time.timeScale = 1
  ├── m_Generator = GameObject.Find("Generator").GetComponent<DungeonGeneratorGrid2D>()  [行:21]
  ├── finder = GameObject.Find("AStarPath").GetComponent<AstarPath>()                   [行:22]
  ├── m_Generator.CustomInputTask = GungeonCustomInput                                  [行:24]
  ├── CoroutinePool.StartCoroutine(Generate())  [行:25]  ← **地牢生成协程**
  └── new BattleScene.GameFacade()       [行:26, Facade/Battle/GameFacade.cs:11]
      ├── new ItemController()
      ├── new UIController()
      │   └── new PanelRoot()            [Panel/BattleScene/PanelRoot.cs]
      │       └── new PanelBattle(this)
      │           ├── new PanelPause(this)
      │           ├── new PanelResurrection(this)
      │           └── new PanelBossCutScene(this)
      ├── new RoomController()           ← **房间控制核心**
      ├── new PlayerController()
      ├── new EnemyController()
      ├── new BuffController()
      ├── RegisterSystem(AudioSystem)
      └── RegisterSystem(TalentSystem)
      ↓
Generate() 协程                          [GameLoop/GameLoopBattle.cs:32]
  ├── yield return m_Generator.GenerateCoroutine()  [行:34] ← **Edgar 地牢生成**
  ├── m_isFinishGenerate = true
  ├── SetTilemaps() → 设置墙体/地板碰撞体  [行:36]
  ├── finder.Scan() → A* 寻路扫描        [行:37]
  ├── EventCenter.NotisfyObserver(EventType.OnFinishRoomGenerate)  [行:38]
  ├── yield return new WaitForSeconds(1)
  └── EventCenter.NotisfyObserver(EventType.OnCameraArriveAtPlayer)  [行:40]
      ↓
BattleScene.GameFacade 监听 OnFinishRoomGenerate  [Facade/Battle/GameFacade.cs:31]
  ├── m_ItemController.TurnOnController()
  ├── m_UIController.TurnOnController()
  ├── m_RoomController.TurnOnController()  ← **激活房间控制器**
  ├── m_BuffController.TurnOnController()
  ├── m_EnemyController.TurnOnController()
  └── m_PlayerController.TurnOnController()
      ↓
RoomController.OnAfterRunUpdate()        [Controller/RoomController.cs:33]
  ├── player = PlayerController.Player   [行:43]
  ├── RoomInstances = RoomPostProcessing.GetRoomInstances()  [行:44]
  └── foreach (RoomInstanceGrid2D roomInstance in RoomInstances)  [行:45]
      ├── Room room = new Room() { roomInstanceGrid2D = roomInstance }
      └── switch ((roomInstance.Room as CustomRoom).RoomType)
          ├── case BirthRoom:
          │   └── player.transform.position = Floor.bounds.center  [行:51]  ← **玩家出生位置**
          ├── case EnemyRoom:
          │   └── SpawnEnemies(room, isElite=false)  [行:55]
          ├── case EliteEnemyRoom:
          │   └── SpawnEnemies(room, isElite=true)   [行:59]
          ├── case TreasureRoom:
          │   └── CreateOtherTreasureBox(room)        [行:63]
          └── case BossRoom:
              ├── room.WaveNum = 0
              └── enemyController.AddBoss(room, FloorCenter)  [行:68]
      ↓
      └── TriggerCenter.RegisterObserver(OnTriggerEnter/Exit, player, FloorCollider)  [行:70-88]
          ├── OnTriggerEnter → isEnterEnemyFloor = true
          └── OnTriggerExit → isEnterEnemyFloor = false
```

---

### 4. 战斗场景游戏循环

```
GameLoopBattle.Update() [每帧]
  └── GameFacade.GameUpdate()
      ├── BuffController.GameUpdate()
      ├── ItemController.GameUpdate()
      ├── UIController.GameUpdate()
      ├── RoomController.GameUpdate()      ← **房间逻辑核心**
      ├── EnemyController.GameUpdate()
      └── PlayerController.GameUpdate()
          ↓
RoomController.AlwaysUpdate()            [Controller/RoomController.cs:92]
  └── if (isEnterEnemyFloor)             [行:95]
      ├── IsPlayerInFloor() 检测玩家是否完全进入房间  [行:98]
      └── if (玩家完全进入 && !isEnterEnemyFloorStart)
          ├── isEnterEnemyFloorStart = true
          ├── if (RoomType == BossRoom):
          │   └── EventCenter.NotisfyObserver(EventType.OnPlayerEnterBossRoom, enterRoom)  [行:110]
          │       └→ PanelBattle 监听 → EnterPanel(PanelBossCutScene) → 播放 Boss 动画
          ├── else:
          │   └── EventCenter.NotisfyObserver(EventType.OnPlayerEnterBattleRoom, enterRoom)  [行:106]
          └── CloseDoor(roomInstance)      [行:112] ← **关门**
              ├── foreach door: SetDoorAnimator(isUp=true)
              └── player.m_Attr.isBattle = true
      ↓
      └── if (enterRoom.CurrentEnemyNum == 0)  [行:115]
          ├── if (WaveNum > 0):
          │   └── SpawnEnemies() → 生成下一波  [行:119/123]
          └── else if (!isClearEnemyStart):
              ├── isClearEnemyStart = true
              ├── CreateWhiteTreasureBox(enterRoom)  [行:129]
              ├── ShowBattleFinishAnim()             [行:130] ← **"战斗结束" 动画**
              ├── OpenDoor(roomInstance)             [行:131] ← **开门**
              └── TriggerCenter.RemoveObserver()     [行:132] ← **移除触发器**
```

**敌人生成逻辑:**
```
RoomController.SpawnEnemies(Room room, bool isElite)  [Controller/RoomController.cs:237]
  ├── totalEnemiesCount = room.SpawnEnemyNum (3-6 + WaveNum)  [行:240]
  └── while (room.CurrentEnemyNum < totalEnemiesCount)
      ├── RandomPointInBounds(FloorCollider.bounds, 2f)  [行:244]
      ├── 检查点是否在 Floor 内 (避免洞)              [行:247]
      ├── 检查点周围 1f 半径无碰撞体                   [行:253]
      └── enemyController.SpawnEnemy(room, position, isEnterEnemyFloorStart, isElite)  [行:260]
          └→ room.CurrentEnemyNum++
  └── room.WaveNum--                               [行:262]
```

**门控制逻辑:**
```
CloseDoor(RoomInstanceGrid2D)            [Controller/RoomController.cs:137]
  └── foreach door in roomInstance.Doors
      ├── GetConnectedRoomInstance
      └── SetDoorAnimator(roomObj, isUp=true)  [行:179]
          ├── LeftVerDownDoor.SetBool("isUp", true)
          ├── RightVerDownDoor.SetBool("isUp", true)
          ├── TopHorDownDoor.SetBool("isUp", true)
          └── BottomHorDownDoor.SetBool("isUp", true)

OpenDoor(RoomInstanceGrid2D)             [Controller/RoomController.cs:155]
  └── SetDoorAnimator(roomObj, isUp=false) → 门降下
```

---

### 5. 玩家死亡/复活流程

```
PanelBattle.OnUpdate()                   [Panel/BattleScene/PanelBattle.cs:83]
  └── if (GetPlayer().IsDie)             [行:101]
      └── EnterPanel(typeof(PanelResurrection))  [行:103]
          ↓
PanelResurrection 打开 (具体逻辑需查看该文件)
  ├── [选择复活] → 扣除货币/道具 → player.Resurrect()
  └── [选择放弃] → 返回 MainMenuScene 或 MiddleScene
```

---

### 6. 关卡推进逻辑 (推测)

**未确认:** 代码中未找到明确的 `MemoryModel.Stage++` 调用位置。

**推测流程:**
```
Boss 死亡
  ↓
BossController.OnDie() (推测)
  ├── EventCenter.NotisfyObserver(EventType.OnBossDie)
  └→ 某个监听器触发:
      ├── MemoryModelCommand.AddStage(1)  (推测方法)
      │   └── MemoryModel.Stage++
      ├── 显示 "关卡完成" UI
      ├── 掉落宝箱/奖励
      └── 传送玩家到下一区域 或 返回 MiddleScene
```

**确认方法:**
需查看 `IBoss.cs` 或 `BossController.cs` 中的 `OnDie()` 方法实现。

---

## 关键分支流程

### 分支 1: 联机模式

```
用户点击 ButtonMultiPlayer
  ↓
PanelOnlineAlert.OnEnter()               [Panel/MainMenuScene/PanelOnlineAlert.cs]
  └── 提示 "请先登录"
      ↓
用户点击 "登录" 按钮
  ↓
PanelLogin.OnEnter()                     [Panel/MainMenuScene/PanelLogin.cs]
  ├── 输入 UserName / Password
  └── ButtonLogin.onClick
      └── RequestLogin.SendRequest(userName, password, callback)  [MultiPlayer/Request/RequestLogin.cs]
          └→ ClientFacade.SendRequest(MainPack)
              └→ TCP 发送 Protobuf 数据到服务器
                  ↓
服务器返回 ReturnCode.Success
  ↓
PanelRoomList.OnEnter()                  [Panel/MainMenuScene/PanelRoomList.cs]
  ├── ButtonSearch.onClick → RequestFindRoom.SendRequest()  [行:42]
  │   └→ 返回 List<RoomPack>
  │       └─→ 显示房间列表 (RoomName, CurrentNum/MaxNum, RoomCode状态)
  └── 用户点击 RoomItem
      └── RequestJoinRoom.SendRequest(RoomName)  [行:87]
          └→ 如果成功:
              └── MemoryModelCommand.EnterOnlineMode()  [行:122]
                  └── MemoryModel.isOnlineMode.Value = true
                      ↓
加载 OnlineStartRoom.unity
  ↓
OnlineStartRoom 等待房主开始
  ↓
房主点击 "开始游戏"
  ↓
RequestEnterOnlineStartRoom.SendRequest()
  ↓
服务器广播 → 所有玩家加载 BattleScene
  ↓
(同单机流程,但同步所有玩家位置/输入)
```

### 分支 2: 暂停菜单

```
PanelBattle 中用户按 ESC 或点击 ButtonPause
  ↓
PanelBattle.ButtonPause.onClick          [Panel/BattleScene/PanelBattle.cs:59]
  ├── EnterPanel(typeof(PanelPause))
  └── PanelBattle.SetActive(false)
      ↓
PanelPause.OnEnter() (具体逻辑需查看)
  ├── Time.timeScale = 0 (推测)
  ├── [继续游戏] → OnExit() → Time.timeScale = 1
  └── [返回主菜单] → LoadScene(MainMenuScene)
```

---

## 时序图 (玩家进入战斗房间)

```
玩家移动
  ↓
Collider2D 触发 Floor 的 OnTriggerEnter
  ↓
TriggerCenter.NotisfyObserver(OnTriggerEnter, player, FloorCollider)
  ↓
RoomController.AlwaysUpdate() 检测到 isEnterEnemyFloor = true
  ↓
IsPlayerInFloor() 返回 true (玩家完全进入)
  ↓
isEnterEnemyFloorStart = true
  ↓
EventCenter.NotisfyObserver(OnPlayerEnterBattleRoom, enterRoom)
  ↓
CloseDoor(enterRoom.roomInstanceGrid2D)
  ├─→ 所有门的 Animator.SetBool("isUp", true)
  └─→ player.m_Attr.isBattle = true
  ↓
敌人开始移动/攻击 (EnemyController.GameUpdate())
  ↓
玩家击杀所有敌人
  ↓
enterRoom.CurrentEnemyNum == 0
  ↓
if (WaveNum > 0): SpawnEnemies() → 生成下一波
  ↓
WaveNum == 0 && CurrentEnemyNum == 0
  ↓
CreateWhiteTreasureBox(enterRoom)
  ↓
ShowBattleFinishAnim() → "Finish" 动画播放
  ↓
OpenDoor(enterRoom.roomInstanceGrid2D)
  ├─→ Animator.SetBool("isUp", false)
  └─→ player.m_Attr.isBattle = false
  ↓
TriggerCenter.RemoveObserver() → 防止重复触发
```

---

## 数据流追踪

### 玩家属性数据流

```
Unity Inspector 配置 PlayerScriptableObject
  ↓
PlayerScriptableObject.PlayerData (CSV 字符串)
  ↓
ModelContainer.Instance 构造函数初始化
  ↓
PlayerModel.Init()                       [Model/PlayerModel.cs]
  ├── 解析 CSV 字符串
  └── PlayerAttrDic[PlayerType] = PlayerAttribute
      ↓
用户在 MiddleScene 点击角色
  ↓
PlayerCommand.GetPlayerAttr(PlayerType)  [Command/PlayerCommand.cs]
  └── return PlayerModel.PlayerAttrDic[PlayerType]
      ↓
AttributeFactory.CreatePlayerAttribute(PlayerAttribute)  [Factory/AttributeFactory.cs]
  └── return new PlayerAttribute() { MaxHp = attr.MaxHp, ... }
      ↓
PlayerFactory.CreatePlayer(PlayerType, position)  [Factory/PlayerFactory.cs]
  ├── GameObject playerObj = ResourcesFactory.GetPlayerPrefab(PlayerType)
  └── IPlayer player = playerObj.GetComponent<IPlayer>()
      ├── player.m_Attr = AttributeFactory.CreatePlayerAttribute(...)
      └── player.Init()
          ↓
MemoryModel.PlayerAttr = player.m_Attr  (保存到运行时数据)
  ↓
BattleScene 加载时
  ↓
PlayerController.TurnOnController()
  ├── player = PlayerFactory.CreatePlayer(MemoryModel.PlayerAttr.Type)
  └── player.m_Attr.CurrentHp = MemoryModel.PlayerAttr.CurrentHp (恢复状态)
```

### 关卡数据流

```
MemoryModel.OnInit()                     [Model/MemoryModel.cs:9]
  ├── Stage = 5  (初始关卡,奇怪的默认值)
  └── Money = 0
      ↓
BattleScene 加载
  ↓
GungeonCustomInput.GetLevelDescription() [AboutRoom/ScriptableObject/GungeonCustomInput.cs:16]
  ├── MemoryModelCommand.GetSmallStage()  [Command/MemoryModelCommand.cs:30]
  │   └── return Stage - (GetBigStage() - 1) * 5
  │       ├── BigStage = (Stage - 1) / 5 + 1
  │       └── SmallStage = Stage - (BigStage - 1) * 5
  └── if (SmallStage == 5):
      │   selectLevelGraph = roomConfig.LevelGraphBoss  [行:22]
      └── else:
          └── selectLevelGraph = roomConfig.LevelGraph (或随机)  [行:28/33]
              ↓
地牢生成基于 selectLevelGraph
  ├── LevelGraphBoss → Boss 关 (单个 BossRoom)
  └── LevelGraph → 正常关卡 (多个房间)
      ↓
PanelBattle.OnEnter()                    [Panel/BattleScene/PanelBattle.cs:65]
  ├── TextMiddle.text = MemoryModelCommand.GetStageDisplayName()  [行:78]
  │   └→ 显示 "1-5" 或 "2-3" 等
  └── TextArea.text = MemoryModelCommand.GetAreaDisplayName()     [行:117]
      └→ 显示 "教学楼" / "天台" / "储物间" / "第X区"
```

---

## 未确认的关键流程 (需补充)

1. **从 MiddleScene 进入 BattleScene 的按钮位置**
   - 推测位置: `PanelSelectPlayer` 或 `PanelRoom` 中的 "开始游戏" 按钮
   - 需查看文件: `Assets/Script/Panel/MiddleScene/PanelSelectPlayer.cs`

2. **关卡推进逻辑**
   - 未找到 `MemoryModel.Stage++` 的调用位置
   - 推测触发点: Boss 死亡后的回调
   - 需查看文件: `Assets/Script/Character/Boss/*.cs` 的 `OnDie()` 方法

3. **关卡完成后的流程**
   - 是否返回 MiddleScene 还是直接进入下一关?
   - 是否有商店/结算界面?
   - 需查看: `PanelBoss` 或 Boss 死亡事件的监听器

4. **金钱获取途径**
   - 确认: 击杀敌人掉落 CoinType 枚举物品
   - 未确认: 宝箱是否掉落金币? 金币如何增加 `MemoryModel.Money`?
   - 需查看文件: `Assets/Script/Items/Item/Coin/*.cs`

5. **存档系统触发时机**
   - `ArchiveModel` 存在,但未找到 `ArchiveCommand.Save()` 调用位置
   - 推测: 关卡完成/返回主菜单时自动保存
   - 需查看文件: `Assets/Script/Command/ArchiveCommand.cs`
