2026-01-14 | Ŀ: Ϊָ | ļ: Assets/Script/Character/IPlayer.cs | ժҪ: ʹ Camera.main.ScreenToWorldPoint  mouseDir FirePoint/RotOrigin Ϊ淶
2026-01-15 | ??: UI?????????? | ??: Assets/Script/Panel/MainMenuScene/PanelRoomList.cs | ??: ???????????????????
2026-01-15 | 目的: UI显示层实验 | 文件: Assets/Script/Panel/BattleScene/PanelBattle.cs | 摘要: 临时修改关卡显示为"X-6"格式，仅影响UI显示层，不改变Stage/SmallStage真实逻辑，不影响Boss判定(SmallStage==5)，可一键回滚
2026-01-15 | 目的: 修复角色选择BUG | 文件: Assets/Script/Panel/MiddleScene/PanelSelectPlayer.cs | 摘要: 在ButtonStart.onClick中添加MemoryModel.PlayerAttr写入(行118)，确保选择的角色(如Teacher)在进入BattleScene时不被重置为默认Knight，修复"选张老师进战斗变刺客"问题
