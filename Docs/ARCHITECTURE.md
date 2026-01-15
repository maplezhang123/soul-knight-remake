# Soul-Knight UI 面板系统架构

## UI 架构总览

### Panel 层级结构与生命周期

**基类:** `IPanel` (位置: `Assets/Script/Panel/IPanel.cs`)

**生命周期方法:**
```csharp
OnInit()    → 初始化 (只调用一次,绑定按钮事件)
OnEnter()   → 进入面板 (每次显示时调用)
OnUpdate()  → 更新面板 (每帧调用,需手动调用)
OnExit()    → 退出面板 (每次隐藏时调用)
OnPause()   → 暂停面板 (子面板打开时调用)
OnResume()  → 恢复面板 (子面板关闭时调用)
```

**父子面板机制:**
- 每个 Panel 维护 `children` 列表和 `parent` 引用
- 调用 `EnterPanel(typeof(ChildPanel))` 进入子面板:
  - 父面板调用 `OnPause()` → `m_GameObject.SetActive(false)`
  - 子面板调用 `OnEnter()` → `m_GameObject.SetActive(true)`
- 子面板 `OnExit()` 时:
  - 如果 `isShowPanelAfterExit == true`:
    - 父面板调用 `OnResume()` → `m_GameObject.SetActive(true)`

**UIController 职责:**
- 创建根 Panel (`new PanelRoot()`)
- 每帧调用 `PanelRoot.OnUpdate()`

---

## MainMenuScene 面板系统

### UI 层级树

```
Canvas (MainMenuScene.unity)
└── PanelRoot                          [Assets/Script/Panel/MainMenuScene/PanelRoot.cs]
    ├── 自身 UI 元素:
    │   ├── ButtonPanel          (主按钮,切换 DivStart/DivLeft 显示)
    │   ├── DivStart             (包含 ButtonSinglePlayer, ButtonMultiPlayer)
    │   ├── DivLeft              (包含 ButtonLogin, ButtonSetting)
    │   ├── TextStart            ("开始游戏" 文本)
    │   └── 背景图/Logo
    │
    ├── PanelLogin(this)               [Assets/Script/Panel/MainMenuScene/PanelLogin.cs]
    │   └── 职责: 用户登录 (联机模式)
    │       ├── InputFieldUserName   (用户名输入框)
    │       ├── InputFieldPassword   (密码输入框)
    │       └── ButtonLogin          (发送 RequestLogin)
    │
    ├── PanelOnlineAlert(this)         [Assets/Script/Panel/MainMenuScene/PanelOnlineAlert.cs]
    │   └── 职责: 联机模式提示 ("请先登录")
    │       ├── ButtonLogin          (打开 PanelLogin)
    │       └── ButtonBack           (返回 PanelRoot)
    │
    └── PanelSetting(this)             [Assets/Script/Panel/MainMenuScene/PanelSetting.cs]
        └── 职责: 游戏设置
            ├── ButtonSound          (打开 PanelSound)
            └── ButtonBack           (返回 PanelRoot)
            │
            └── PanelSound(this)           [Assets/Script/Panel/MainMenuScene/PanelSound.cs]
                └── 职责: 音量/音效设置
                    ├── SliderBGM        (背景音乐音量)
                    ├── SliderSFX        (音效音量)
                    └── ButtonBack       (返回 PanelSetting)

(独立弹出,不在树中)
└── PanelRoomList                      [Assets/Script/Panel/MainMenuScene/PanelRoomList.cs]
    └── 职责: 联机房间列表 (联机模式)
        ├── ButtonSearch         (搜索房间 → RequestFindRoom)
        ├── ButtonBack           (返回 PanelRoot)
        ├── RoomItem (模板)      (房间列表项)
        │   ├── TextRoomName     (房间名称)
        │   ├── TextPlayerNum    (当前人数/最大人数)
        │   └── TextState        (房间状态: 等待加入/游戏中/已满)
        └── PanelUserList(this)        [Assets/Script/Panel/MainMenuScene/PanelUserList.cs]
            └── 职责: 房间内玩家列表

(独立弹出,不在树中)
└── PanelCreateRoom                    [Assets/Script/Panel/MainMenuScene/PanelCreateRoom.cs]
    └── 职责: 创建联机房间
        ├── InputFieldRoomName   (房间名称)
        ├── InputFieldMaxNum     (最大人数)
        └── ButtonCreate         (创建房间 → RequestCreateRoom)
```

### 面板切换关系图

```
用户操作流程:

启动游戏
  ↓
PanelRoot (主菜单)
  ├─→ [点击 ButtonSinglePlayer] → LoadScene(MiddleScene)
  │
  ├─→ [点击 ButtonMultiPlayer] → EnterPanel(PanelOnlineAlert)
  │       ↓
  │   PanelOnlineAlert
  │       ├─→ [点击 ButtonLogin] → EnterPanel(PanelLogin)
  │       │       ↓
  │       │   PanelLogin
  │       │       ├─→ [登录成功] → OnExit() → (推测) 打开 PanelRoomList
  │       │       └─→ [点击 ButtonBack] → OnExit() → 返回 PanelOnlineAlert
  │       │
  │       └─→ [点击 ButtonBack] → OnExit() → 返回 PanelRoot
  │
  ├─→ [点击 ButtonSetting] → EnterPanel(PanelSetting)
  │       ↓
  │   PanelSetting
  │       ├─→ [点击 ButtonSound] → EnterPanel(PanelSound)
  │       │       ↓
  │       │   PanelSound
  │       │       └─→ [点击 ButtonBack] → OnExit() → 返回 PanelSetting
  │       │
  │       └─→ [点击 ButtonBack] → OnExit() → 返回 PanelRoot
  │
  └─→ [点击 ButtonLogin] → EnterPanel(PanelLogin)

(联机模式额外流程)
PanelLogin 登录成功
  ↓
PanelRoomList (房间列表)
  ├─→ [点击 ButtonSearch] → 显示房间列表
  │       └─→ [点击 RoomItem] → RequestJoinRoom
  │           └─→ [加入成功] → MemoryModelCommand.EnterOnlineMode()
  │               └─→ LoadScene(OnlineStartRoom)
  │
  └─→ [点击 ButtonCreate] → (推测) 打开 PanelCreateRoom
      └─→ PanelCreateRoom
          └─→ [创建成功] → LoadScene(OnlineStartRoom)
```

### 按钮事件绑定详情 (MainMenuScene)

| 面板 | 按钮名称 | 触发方法 | 位置 (行号) |
|------|---------|---------|------------|
| **PanelRoot** | ButtonPanel | 切换 DivStart/DivLeft 显示 | PanelRoot.cs:43 |
|  | ButtonSinglePlayer | `SceneModelCommand.LoadScene(MiddleScene)` | PanelRoot.cs:80 |
|  | ButtonMultiPlayer | `EnterPanel(typeof(PanelOnlineAlert))` | PanelRoot.cs:84 |
|  | ButtonLogin | `EnterPanel(typeof(PanelLogin))` | PanelRoot.cs:88 |
|  | ButtonSetting | `EnterPanel(typeof(PanelSetting))` | PanelRoot.cs:76 |
| **PanelRoomList** | ButtonSearch | `RequestFindRoom.SendRequest()` | PanelRoomList.cs:42 |
|  | ButtonBack | `OnExit()` | PanelRoomList.cs:38 |
|  | RoomItem | `RequestJoinRoom.SendRequest(RoomName)` | PanelRoomList.cs:87/105 |
| **PanelLogin** | ButtonLogin | `RequestLogin.SendRequest()` (推测) | (需查看文件) |
|  | ButtonBack | `OnExit()` (推测) | (需查看文件) |
| **PanelSetting** | ButtonSound | `EnterPanel(typeof(PanelSound))` (推测) | (需查看文件) |
|  | ButtonBack | `OnExit()` (推测) | (需查看文件) |

---

## MiddleScene 面板系统

### UI 层级树

```
Canvas (MiddleScene.unity)
└── PanelRoot                          [Assets/Script/Panel/MiddleScene/PanelRoot.cs]
    ├── 自身 UI 元素:
    │   └── TextGem              (宝石数量显示)
    │
    └── PanelRoom(this)                [Assets/Script/Panel/MiddleScene/PanelRoom.cs]
        ├── 自身 UI 元素:
        │   ├── DivBottom        (底部 UI 容器)
        │   │   ├── ButtonHome       (返回主菜单)
        │   │   └── ButtonStore      (打开商店)
        │   └── Title            (标题)
        │
        ├── PanelGemStore(this)        [Assets/Script/Panel/MiddleScene/PanelGemStore/PanelGemStore.cs]
        │   └── 职责: 宝石商店
        │       ├── 武器商店
        │       ├── 材料商店
        │       └── PanelGemStore1(this)  (子商店界面)
        │
        ├── PanelSelectPlayer(this)    [Assets/Script/Panel/MiddleScene/PanelSelectPlayer.cs]
        │   └── 职责: 角色选择界面
        │       ├── 显示角色属性 (HP/MP/Armor/Critical...)
        │       ├── 选择皮肤
        │       └── [开始游戏 按钮] → LoadScene(BattleScene)
        │       │
        │       └── PanelBattle(this)      [Assets/Script/Panel/MiddleScene/PanelBattle.cs]
        │           └── 职责: 角色预览战斗界面 (内嵌)
        │
        └── PanelSelectPet(this)       [Assets/Script/Panel/MiddleScene/PanelSelectPet.cs]
            └── 职责: 宠物选择界面
                ├── 显示宠物属性
                └── 选择宠物

(额外独立面板,不在主树中)
├── PanelBackpack                      [Assets/Script/Panel/MiddleScene/PanelBackpack.cs]
│   └── 职责: 背包系统
│       ├── 已装备武器列表
│       └── 未装备武器列表
│
├── PanelPause                         [Assets/Script/Panel/MiddleScene/PanelPause.cs]
│   └── 职责: 暂停菜单
│
├── PanelSeed                          [Assets/Script/Panel/MiddleScene/PanelSeed.cs]
│   └── 职责: 种子选择界面 (花园系统)
│
├── PanelSafeBox                       [Assets/Script/Panel/MiddleScene/PanelSafeBox.cs]
│   └── 职责: 保险箱界面
│
├── PanelLuckyCat                      [Assets/Script/Panel/MiddleScene/PanelLuckyCat.cs]
│   └── 职责: 幸运猫界面
│
├── PanelSmithingTable                 [Assets/Script/Panel/MiddleScene/PanelSmithingTable.cs]
│   └── 职责: 铁匠台界面 (武器合成)
│
├── PanelSmithingAlert                 [Assets/Script/Panel/MiddleScene/PanelSmithingAlert.cs]
│   └── 职责: 铁匠合成确认弹窗
│
└── PanelUnlockGarden                  [Assets/Script/Panel/MiddleScene/PanelUnlockGarden.cs]
    └── 职责: 解锁花园弹窗
```

### 面板切换关系图

```
MiddleScene 加载
  ↓
PanelRoot (自动创建)
  ├── OnInit() → 注册 OnFinishSelectPlayer 事件 (行:19)
  └── OnEnter()
      └── EnterPanel(typeof(PanelRoom))   [PanelRoot.cs:27]
          ↓
PanelRoom
  ├── OnInit() → 注册 OnPlayerClick / OnPetClick 事件 (行:31/36)
  ├── ButtonHome.onClick → LoadScene(MainMenuScene)  [行:25]
  └── ButtonStore.onClick → EnterPanel(typeof(PanelGemStore))  [行:29]
      ↓
用户点击场景中的角色模型
  ↓
PanelRoot.OnUpdate() → 检测鼠标点击 LayerMask "Player"  [PanelRoot.cs:39]
  ↓
EventCenter.NotisfyObserver(EventType.OnPlayerClick, gameObject)  [行:42]
  ↓
PanelRoom 监听到事件 → EnterPanel(typeof(PanelSelectPlayer))  [PanelRoom.cs:33]
  ├── DisappearAnim() → 隐藏 DivBottom/Title  [行:34]
  └── PanelSelectPlayer 打开
      ├── 显示角色属性/技能
      └── [点击 "开始游戏" 按钮] → LoadScene(BattleScene) (推测)
          ↓
EventCenter.NotisfyObserver(EventType.OnFinishSelectPlayer)
  ↓
MiddleScene.GameFacade 监听器触发  [Facade/MiddleScene/GameFacade.cs:31]
  ├── TurnOnController(PlayerController)
  ├── TurnOnController(EnemyController)
  └── PanelRoot.SetActive(false)  [PanelRoot.cs:21]

(宠物选择流程类似)
用户点击场景中的宠物模型
  ↓
EventCenter.NotisfyObserver(EventType.OnPetClick)  [PanelRoot.cs:51]
  ↓
PanelRoom 监听到事件 → EnterPanel(typeof(PanelSelectPet))  [PanelRoom.cs:38]
```

### 交互物品触发的面板 (MiddleScene)

这些面板通过场景中的交互物品触发,不在主 UI 树中:

| 面板 | 触发物品 | 位置 |
|------|---------|------|
| **PanelBackpack** | 按 B 键 或 点击背包图标 | (需确认) |
| **PanelSafeBox** | 点击场景中的 SafeBox | Assets/Script/NeedMono/Item/SafeBox.cs |
| **PanelSmithingTable** | 点击场景中的 SmithingTable | Assets/Script/NeedMono/Item/SmithingTable.cs |
| **PanelLuckyCat** | 点击场景中的 LuckyCat | Assets/Script/NeedMono/Item/LuckyCat.cs |
| **PanelSeed** | 点击场景中的 Garden | Assets/Script/NeedMono/Garden/Garden.cs |

---

## BattleScene 面板系统

### UI 层级树

```
Canvas (BattleScene / Room1.unity)
└── PanelRoot                          [Assets/Script/Panel/BattleScene/PanelRoot.cs]
    └── 职责: 显示提示信息 (Toast)
        │
        └── PanelBattle(this)          [Assets/Script/Panel/BattleScene/PanelBattle.cs]
            ├── 自身 UI 元素:
            │   ├── ButtonPause      (暂停按钮)
            │   ├── SliderHp         (生命值条)
            │   ├── SliderMp         (魔法值条)
            │   ├── SliderArmor      (护甲值条)
            │   ├── TextHp           (生命值文本)
            │   ├── TextMp           (魔法值文本)
            │   ├── TextArmor        (护甲值文本)
            │   ├── TextMoney        (金钱显示)
            │   ├── TextMiddle       (关卡显示: "1-5")
            │   └── TextArea         (区域名称: "教学楼")
            │
            ├── PanelPause(this)           [Assets/Script/Panel/BattleScene/PanelPause.cs]
            │   └── 职责: 暂停菜单
            │       ├── ButtonContinue   (继续游戏)
            │       ├── ButtonRestart    (重新开始)
            │       └── ButtonMainMenu   (返回主菜单)
            │
            ├── PanelResurrection(this)    [Assets/Script/Panel/BattleScene/PanelResurrection.cs]
            │   └── 职责: 复活界面
            │       ├── ButtonResurrect  (复活 - 消耗货币/道具)
            │       └── ButtonGiveUp     (放弃 - 返回主菜单)
            │
            └── PanelBossCutScene(this)    [Assets/Script/Panel/BattleScene/PanelBossCutScene.cs]
                └── 职责: Boss 过场动画界面
                    └── [动画播放完成] → EventCenter.NotisfyObserver(OnBossCutSceneFinish)
                        └── 进入 PanelBoss

(独立弹出,不在树中)
└── PanelBoss                          [Assets/Script/Panel/BattleScene/PanelBoss.cs]
    └── 职责: Boss 战界面
        ├── BossHpBar            (Boss 血条)
        ├── BossName             (Boss 名称)
        └── BossPhaseIndicator   (Boss 阶段指示器,推测)
```

### 面板切换关系图

```
BattleScene 加载
  ↓
PanelRoot (自动创建)
  └── OnEnter()
      └── PanelBattle.OnEnter()  [PanelBattle.cs:65]
          ├── if (isFirstEnter):
          │   ├── TimeLine.Play() → 播放入场动画  [行:71]
          │   ├── TextMiddle.text = "1-5"        [行:78]
          │   └── ShowAreaName() → TextArea.text = "教学楼"  [行:80/117]
          └── PanelBattle 开始每帧更新

用户按 ESC 或点击 ButtonPause
  ↓
ButtonPause.onClick → EnterPanel(typeof(PanelPause))  [PanelBattle.cs:61]
  ├── PanelBattle.SetActive(false)  [行:62]
  └── PanelPause.OnEnter()
      ├── Time.timeScale = 0 (推测)
      ├── [点击 ButtonContinue] → OnExit() → Time.timeScale = 1
      ├── [点击 ButtonRestart] → LoadScene(BattleScene)
      └── [点击 ButtonMainMenu] → LoadScene(MainMenuScene)

玩家进入 BossRoom
  ↓
EventCenter.NotisfyObserver(EventType.OnPlayerEnterBossRoom, enterRoom)  [RoomController.cs:110]
  ↓
PanelBattle 监听到事件  [PanelBattle.cs:50]
  ├── EventCenter.NotisfyObserver(EventType.OnPause)  [行:52]
  └── DelayInvoke(0.5s) → EnterPanel(typeof(PanelBossCutScene))  [行:55]
      ├── OnResume()  [行:56]
      └── PanelBossCutScene.OnEnter()
          ├── 播放 Boss 入场动画
          └── [动画完成] → EventCenter.NotisfyObserver(OnBossCutSceneFinish)
              └─→ (推测) 打开 PanelBoss

玩家死亡
  ↓
PanelBattle.OnUpdate() → GetPlayer().IsDie == true  [PanelBattle.cs:101]
  ↓
EnterPanel(typeof(PanelResurrection))  [行:103]
  └── PanelResurrection.OnEnter()
      ├── [点击 ButtonResurrect] → player.Resurrect() → 扣除货币 → OnExit()
      └── [点击 ButtonGiveUp] → LoadScene(MainMenuScene) 或 MiddleScene
```

### PanelBattle 实时更新逻辑

```csharp
PanelBattle.OnUpdate() [每帧]         [PanelBattle.cs:83]
  ├── 检测 BigStage 是否变化 → ShowAreaName()  [行:86-90]
  ├── if (GetPlayer() != null):
  │   ├── SliderHp.value = CurrentHp / MaxHp        [行:94]
  │   ├── SliderMp.value = CurrentMp / Magic        [行:96]
  │   ├── SliderArmor.value = CurrentArmor / Armor  [行:95]
  │   ├── TextHp.text = "CurrentHp/MaxHp"           [行:97]
  │   ├── TextMp.text = "CurrentMp/Magic"           [行:98]
  │   ├── TextArmor.text = "CurrentArmor/Armor"     [行:99]
  │   ├── TextMoney.text = MemoryModel.Money        [行:100]
  │   └── if (IsDie): EnterPanel(PanelResurrection) [行:101-103]
  └── ShowAreaName() → 2秒后自动隐藏 TextArea  [行:125]
```

---

## 面板显示/隐藏机制总结

### 显示/隐藏逻辑

**IPanel 基类实现:**
```csharp
OnEnter()                               [Panel/IPanel.cs]
  ├── if (parent != null): parent.OnPause()
  └── m_GameObject.SetActive(true)

OnExit()
  ├── m_GameObject.SetActive(false)
  └── if (isShowPanelAfterExit && parent != null): parent.OnResume()

OnPause()
  └── m_GameObject.SetActive(false)

OnResume()
  └── m_GameObject.SetActive(true)
```

**父子面板切换示例:**
```
PanelRoot (Active)
  ↓ [EnterPanel(PanelSetting)]
PanelRoot.OnPause() → SetActive(false)
PanelSetting.OnEnter() → SetActive(true)
  ↓ [PanelSetting.OnExit()]
PanelSetting.SetActive(false)
PanelRoot.OnResume() → SetActive(true) (如果 isShowPanelAfterExit==true)
```

### 特殊面板控制 (不继承 IPanel)

某些面板可能直接继承 MonoBehaviour,不使用 IPanel 机制:
- 通过 `EventCenter` 事件触发显示/隐藏
- 示例: Toast 通知、漂浮文本 (EffectType.PlayerPopupNum)

---

## 事件驱动的 UI 更新

### 关键事件与 UI 响应

| 事件名称 | 触发位置 | UI 响应 |
|---------|---------|--------|
| **OnFinishSelectPlayer** | PanelSelectPlayer (推测) | PanelRoot.SetActive(false) |
| **OnPlayerEnterBossRoom** | RoomController.cs:110 | PanelBattle → EnterPanel(PanelBossCutScene) |
| **OnBossCutSceneFinish** | PanelBossCutScene (推测) | 打开 PanelBoss |
| **OnWantShowNotice** | 全局 (多处) | 显示 Toast 通知 |
| **OnPause** | PanelBattle.cs:52 | 暂停游戏 (Time.timeScale = 0, 推测) |
| **OnResume** | PanelPause (推测) | 恢复游戏 (Time.timeScale = 1) |
| **OnPlayerClick** | PanelRoot.cs:42 | PanelRoom → EnterPanel(PanelSelectPlayer) |
| **OnPetClick** | PanelRoot.cs:51 | PanelRoom → EnterPanel(PanelSelectPet) |

---

## UI 数据绑定

### 数据源 → UI 更新链条

**玩家属性 → PanelBattle:**
```
IPlayer.m_Attr (PlayerAttribute)
  ├── CurrentHp, CurrentMp, CurrentArmor  (实时变化)
  └── m_ShareAttr.MaxHp, Magic, Armor     (固定值)
      ↓
PanelBattle.OnUpdate() [每帧]
  ├── GetPlayer().m_Attr.CurrentHp
  └── 更新 SliderHp.value / TextHp.text
```

**金钱 → PanelBattle:**
```
MemoryModel.Money (int)
  ↓ (敌人死亡掉落金币 → 拾取)
MemoryModelCommand.AddMoney(int)  [Command/MemoryModelCommand.cs:8]
  └── MemoryModel.Money += addition
      ↓
PanelBattle.OnUpdate() [每帧]
  └── TextMoney.text = MemoryModel.Money.ToString()  [PanelBattle.cs:100]
```

**关卡/区域 → PanelBattle:**
```
MemoryModel.Stage (int)
  ↓
MemoryModelCommand.GetBigStage()       [Command/MemoryModelCommand.cs:26]
MemoryModelCommand.GetSmallStage()     [行:30]
MemoryModelCommand.GetAreaDisplayName() [行:42]
  ↓
PanelBattle.OnEnter() [初始化]
  ├── TextMiddle.text = "1-5"          [PanelBattle.cs:78]
  └── TextArea.text = "教学楼"         [行:117]
      ↓
PanelBattle.OnUpdate() [每帧检测变化]
  └── if (currentBigStage != lastBigStage): ShowAreaName()  [行:86-90]
```

---

## 未确认的 UI 流程 (需补充)

1. **PanelSelectPlayer 的 "开始游戏" 按钮**
   - 推测位置: PanelSelectPlayer 内部
   - 需查看文件: `Assets/Script/Panel/MiddleScene/PanelSelectPlayer.cs`
   - 预期行为: `ButtonStartGame.onClick → LoadScene(BattleScene)`

2. **PanelBoss 的触发时机**
   - 推测: 监听 `OnBossCutSceneFinish` 事件
   - 需查看文件: `Assets/Script/Panel/BattleScene/PanelBoss.cs`

3. **PanelPause 的完整逻辑**
   - 是否真的设置 `Time.timeScale = 0`?
   - 需查看文件: `Assets/Script/Panel/BattleScene/PanelPause.cs`

4. **PanelResurrection 的完整逻辑**
   - 复活如何扣除货币/道具?
   - 放弃后返回哪个场景?
   - 需查看文件: `Assets/Script/Panel/BattleScene/PanelResurrection.cs`

5. **交互物品面板的绑定方式**
   - SafeBox/SmithingTable/LuckyCat 如何触发对应 Panel?
   - 推测: 通过 `EventCenter.NotisfyObserver(EventType.OnWant...)` 事件
   - 需查看文件: `Assets/Script/NeedMono/Item/*.cs`

---

## 联机命名污染总结

### 单机未使用的联机 Panel:
- `PanelRoomList` - 房间列表 (RoomPack 语义)
- `PanelCreateRoom` - 创建房间
- `PanelUserList` - 用户列表
- `PanelLogin` - 登录 (联机模式)
- `PanelOnlineAlert` - 在线提示

### 结论:
- 联机 UI 系统完全独立,单机玩法不依赖
- 主菜单中 `ButtonMultiPlayer` 是联机入口,单机玩家不会触碰
- 没有命名污染问题,代码分离清晰
