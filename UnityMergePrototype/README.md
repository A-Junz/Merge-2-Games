# Unity 二合核心玩法原型

## 打开与运行

- Unity 版本：`2022.3.62f3c1`
- 工程路径：`/Users/edy/Documents/ChatGPT/二合/UnityMergePrototype`
- 主场景：`Assets/Scenes/Main.unity`
- 操作：用 Unity Hub 添加上述工程，打开 `Assets/Scenes/Main.unity`，点击 Play。

## 玩法

1. 点击底部的 `SERVE ITEMS`，按从上到下、从左到右的顺序把所有空位发满普通物品；物品等级按规则权重抽取，已占用格不会被覆盖。
2. 拖动物品到空格会移动，拖到不同物品会交换。
3. 两个同链、同等级物品拖放在一起会合成下一级。
4. 当棋盘上的物品满足某张订单时，点击订单卡的提交按钮获得金币。
5. 特殊金额元素可以双击直接兑现；发牌不受能量限制。
6. 长按合成物可打开删除确认弹窗；确认后移除该元素。
7. 发牌按钮左侧的图鉴图标可打开 10 条普通食物合成链图鉴；发牌生成 3/4 级元素时会短暂显示幸运标识。
8. 订单区固定显示约 3.5 张卡，1～3 单不可滑动，4～5 单可左右滑动；订单缺口每 5 秒刷新。
9. 状态会自动写入 `Application.persistentDataPath` 下的 `merge-prototype-save.json`。
10. 点击进度条右侧的“重开”会清除存档并创建新游戏。

## 批处理测试

```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/edy/Documents/ChatGPT/二合/UnityMergePrototype"

# EditMode：核心棋盘、能量、生成器、订单、存档
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform editmode \
  -testResults /private/tmp/merge-editmode.xml \
  -logFile /private/tmp/merge-editmode.log

# PlayMode：主场景启动、生成、拖拽、合成、提交订单
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform playmode \
  -testResults /private/tmp/merge-playmode.xml \
  -logFile /private/tmp/merge-playmode.log
```

## 简化测试版本 A

`GameBootstrap` 的 `Variant` 字段可切换为 `TestA`。该版本使用独立的 `merge-prototype-save-test-a.json` 存档，包含指定的 7×9 初始棋盘、前 5 条普通合成链和普通合成链最高 7 级；订单进度等级没有上限，切回 `Current` 即恢复当前版本规则，两个版本的存档互不覆盖。

## 范围说明

这是从原 Cocos Creator 工程重建的功能原型，不直接载入 Cocos 场景或 TypeScript。本版包含 7×9 棋盘、10 条各 8 级厨房食物合成链、5 级特殊金额链、顺序发牌、最多 5 个动态订单、金币、经验和 JSON 存档。完整系统规则见 `docs/首个版本系统规则.md`。原工程中的美术、音效、活动、商城、家园装修、广告/渠道 SDK、仓库、格子解锁和小游戏打包不在本阶段。
