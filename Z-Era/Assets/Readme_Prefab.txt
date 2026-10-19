base下预制体导入场景后需要赋值的字段
1. Inventory Canvas -> Canvas组件 -> 渲染摄像机（MainCamera）
2. Inventory Canvas -> ItemDescriptionText -> Inventory Item Description View脚本 -> Radial View（InventorySystem -> InventoryContainer）
3. InventorySystem -> InventoryInput -> Inventory Input脚本 -> "背包背景"下的三个字段 (第一个为Inventory Canvas，第二个为Inventory Canvas -> FullscreenBackgroundImage,第三个为HUD Canvas) -> "暂停游戏"下的Player Controller (Player)
4. InventorySystem -> InventoryContainer -> Inventory Radial View脚本 -> Main Camera (MainCamera)
5. Player -> Noise Emitter脚本 (NoiseManager)
6. Player -> Pickup Controller脚本 -> Prompt Text (HUD Canvas -> PickupPromptText) -> Radial View (InventorySystem -> InventoryContainer)
7. Player下各武器 -> Weapon Effects脚本 -> Bullet Pool (BulletPool)
8. 创建可对话NPC -> 