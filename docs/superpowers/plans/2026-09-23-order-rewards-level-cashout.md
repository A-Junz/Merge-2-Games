# 订单奖励、等级进度与模拟提现实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Cocos Creator 2.4.13 核心合成游戏中移除普通生成器等待冷却、将气泡冷却加速改为激励视频，并加入订单驱动的等级与本地模拟提现顶栏。

**Architecture:** 扩展现有 `LevelMgr` 作为唯一等级、订单 EXP 与现金状态接口；顾客交付完成时以订单 ID 幂等结算；游戏 HUD 监听进度事件。普通生成器不再使用计时器等待，气泡冷却继续存在但仅在激励视频成功时跳过。

**Tech Stack:** Cocos Creator 2.4.13、TypeScript、现有 `LocalValue`/`StorageHelper`、`CMgr.helper.showRewardAd`、Cocos 运行时节点/Graphics/Label。

**Spec:** `docs/superpowers/specs/2026-09-23-order-rewards-level-cashout-design.md`

## Global Constraints

- 工程使用 Cocos Creator 2.4.13。
- 只实现本地模拟提现，不调用真实支付、银行、钱包或服务端接口。
- 复用当前 `LevelMgr` 的等级作为唯一玩家等级。
- 订单按顾客交付完成结算：每个订单只结算一次，增加 Rp20.00 与 1 EXP。
- 每次升级增加 Rp30.00；Lv1→3、Lv2→4、Lv3–6→5、Lv7–9→6、Lv10–12→7、Lv13–16→8、Lv17–20→9、Lv21+→10 EXP。
- 进入新规则时保留本机已有等级，不补发历史订单奖励。
- Lv10 起可提现；提现提取全部本地余额并归零。
- 普通生成器 `OUT_ITEM_WITH_OPEN_CD` / `OUT_RANDOM_AND_CD` 不再等待时间冷却；气泡冻结冷却仍可等待，激励视频成功后可提前完成。
- 核心玩法可达的金币扣费入口改为激励视频；金币收入和正向奖励保持原样。
- 不修改随包静态配置表作为玩家进度存档，不增加外部依赖。
- 本会话不新增或运行测试；Cocos 启动/构建受已知 macOS LaunchServices 故障阻断，不得报告未验证的运行成功。

## Review Focus

1. 同一顾客订单的重复交付回调、重启前后重复 ID 必须只奖励一次（Task 2：核查订单 ID、防重存档与唯一发奖调用点）。
2. 升级门槛边界、EXP 溢出、Lv21+ 和旧等级迁移必须一致（Task 1：逐项对照阈值表和本地记录序列化）。
3. 生成器旧存档处于 `CAN_GET`/未来计时状态时必须直接恢复可产出，不把气泡冻结一起移除（Task 3：沿状态初始化、视图遮罩、点击与产出流程逐支核对）。
4. 激励视频失败、取消、无广告或重复回调不能解冻气泡，也不能扣除金币（Task 3：检查 SDK 成功回调的唯一变更路径）。
5. Lv10 门槛、零余额、重置与异常/未知版本存档不能导致误提现或静默覆盖（Task 1/4：核对所有提现分支和恢复备份行为）。

---

### Task 1: 在 LevelMgr 建立持久化订单等级与现金状态

**Files:**
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/level/model/LevelMgr.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/config/Config.ts`

**Interfaces:**
- Produces `getLevel(): number`, `getOrderExp(): number`, `getExpRequired(): number`, `getCashBalance(): number`, `getCompletedOrderCount(): number`, `getCashoutMinLevel(): number` (constant `10`), `getRecoveryMessage(): string`, `recordCompletedOrder(orderID: string | number): OrderProgressResult`, and `simulateCashout(): CashoutResult`.
- `OrderProgressResult` is `{ awarded: boolean; reason: 'GRANTED' | 'DUPLICATE' | 'INVALID_ID'; level: number; exp: number; expRequired: number; cash: number; levelsGained: number; }`.
- `CashoutResult` is `{ ok: boolean; reason: 'SUCCESS' | 'LEVEL_LOCKED' | 'NO_BALANCE'; amount: number; balance: number; }`.
- `EventName.PLAYER_PROGRESS_CHANGED` is emitted with no required arguments after successful order settlement or simulated cashout; HUD reads the persisted getters.

- [ ] **Step 1: Inspect current persistence and level callers**

Run `rg -n "LevelMgr\\.instance\\(\\)\\.(getLevel|getTotalExp|getPersonNum|checkLevel|updateExp)|getCurLevel\\(" 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic` from the workspace root. Record which features require the existing `level` key, which callers still award `ItemID.EXP`, and whether any UI depends on `LevelItemModel` reward state.

- [ ] **Step 2: Define the one-record state and legacy-level migration**

Add a local interface in `LevelMgr.ts`:

```ts
interface OrderProgressState {
    version: 1;
    level: number;
    exp: number;
    completedOrders: number;
    cash: number;
    completedOrderIDs: string[];
}
```

Use a single `LocalValue('playerOrderProgress.v1', null)` so all new progress is saved as one JSON value. If absent, initialize level from the existing `LocalValue('level', 1)`, with EXP/orders/cash zero. Before replacing a corrupt or unknown version, copy its raw `BaseStorage.LOCAL_NAME + 'playerOrderProgress.v1'` bytes to a unique recovery key and retain the warning in `getRecoveryMessage()`; do not silently overwrite raw data if backup fails. `CoreActionView` displays this warning once via the existing toast on startup.

- [ ] **Step 3: Implement the exact open-ended EXP threshold function**

Implement `getExpRequired()` from the confirmed table:

```ts
private requiredExp(level: number): number {
    if (level <= 1) return 3;
    if (level === 2) return 4;
    if (level <= 6) return 5;
    if (level <= 9) return 6;
    if (level <= 12) return 7;
    if (level <= 16) return 8;
    if (level <= 20) return 9;
    return 10;
}
```

`getExpRequired()` returns `requiredExp(this.state.level)`; clamp malformed negative levels to level 1 during migration.

- [ ] **Step 4: Add idempotent order settlement and per-level cash awards**

In `recordCompletedOrder(orderID)`, normalize `String(orderID)`, return `INVALID_ID` for an empty ID, and return `DUPLICATE` unchanged for an ID already in `completedOrderIDs`. Otherwise clone state, add the ID, increment `completedOrders`, add 20 cash and 1 EXP, then loop while `exp >= requiredExp(level)`, subtracting the current requirement, incrementing level and adding 30 cash for each level. Save the complete state once with `LocalValue.setValue(nextState)`, then mirror the level to the legacy `LocalValue('level', 1)` for consumers that still directly use that old key, emit one progress event and return `GRANTED` plus the resulting snapshot values.

- [ ] **Step 5: Implement Lv10 mock cashout and legacy API behavior**

`simulateCashout()` must return `LEVEL_LOCKED` below level 10, `NO_BALANCE` at zero balance, or `SUCCESS` with the full amount and updated zero-balance state at level 10+. Keep `getLevel()` sourced from the new single progress state. Keep `getPersonNum()` using the matching `LevelItemModel` count where available, and return the last configured level capacity after level data ends. Remove or deprecate `checkLevel/updateExp` only after Task 2 removes every active caller.

- [ ] **Step 6: Run static state-contract review**

Use `rg` to verify the state key is only written through the single progress-record method; manually trace level 1→2, exact-threshold level-up, EXP carryover, Lv20→21→22, duplicate ID, corrupt backup failure, zero balance and Lv9/Lv10 payout branches. Do not add or run tests.

### Task 2: Award once at order delivery and remove legacy XP leveling

**Files:**
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/task/model/TaskMgr.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/level/model/LevelMgr.ts`

**Interfaces:**
- Consumes `LevelMgr.recordCompletedOrder(orderID)` from Task 1.
- Produces a single order-completion integration at `GameView.foodMoveFinish(buyItem, pos)` using `buyItem.getID()`.

- [ ] **Step 1: Verify order identity and delivery lifecycle**

Inspect `BuyItemView.getID()`, `GameView.buyFood()`, `foodMoveFinish()`, `BuyPerson` removal/recovery, and animation callbacks. Confirm the ID remains stable through the full handoff and that the completion method cannot run for an incomplete item delivery.

- [ ] **Step 2: Call progression only after the customer handoff is complete**

In `foodMoveFinish`, capture `buyItem.getID()` before `buyItem.removeSelf()` and call `LevelMgr.instance().recordCompletedOrder(orderID)` once when the delivery is accepted. Keep the existing item reward, person-out animation, recovery and next-customer flow intact. On a duplicate result, do not replay the money/EXP animation or toast.

- [ ] **Step 3: Stop old item EXP from incrementing the unified player level**

Remove the `LevelMgr.instance().checkLevel(...)` call in `TaskMgr.addItems()`; preserve the task's own item/reward logic. Search every `checkLevel`, `updateExp`, `ItemID.EXP` and `getTotalExp` caller; delete only level-advancement paths that conflict with order-only leveling, and leave unrelated inventory reads intact.

- [ ] **Step 4: Review order reward ordering and persistence**

Trace the sequence from customer accepts all requested board items through `foodMoveFinish`, progression save, legacy level mirror, event emission and new-customer creation. Verify saved state survives restart without paying historical orders and duplicate order IDs do not award twice. Do not add or run tests.

### Task 3: Remove generator waits and change bubble cooldown acceleration to rewarded video

**Files:**
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/FoodItemView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameBottomView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/model/GridFoodItemModel.ts`

**Interfaces:**
- Consumes existing generator `FoodOutType` state, `FoodItemView` mask state, and `CMgr.helper.showRewardAd(0, callback)`.
- Produces a bubble-only rewarded-ad cooldown action; no path deducts `ItemID.GOLD` for cooldown.

- [ ] **Step 1: Map every cooldown state transition**

Trace both `OUT_RANDOM_AND_CD` and `OUT_ITEM_WITH_OPEN_CD` through `GameView.addFoodByGrid`, `FoodItemView.setTime/updateState/showMask/timeUpdate`, and `GridFoodItemModel.init`. Trace bubble `splitChance` separately. Note persisted states that currently encode waiting time.

- [ ] **Step 2: Make fixed-output generator items immediately producible**

Change the `OUT_ITEM_WITH_OPEN_CD` click branch so it does not enter `CAN_GET`, write a future timestamp, or render a countdown. It should immediately follow the existing fixed output selection, decrement configured count and preserve current exhausted-source behavior. Normalize old saved generator `CAN_GET`/future-time states to immediately usable state on load without modifying `splitChance` bubble freezes.

- [ ] **Step 3: Make random-and-cooldown generators recover count immediately**

At the point where an `OUT_RANDOM_AND_CD` generator exhausts its count, replace the `CAN_GET` + `setTime()` transition with immediate count reset, `GOT` state and visual refresh. Keep random output selection, board-capacity checks, and the configured number of outputs unchanged.

- [ ] **Step 4: Present a rewarded-video affordance only for frozen bubble elements**

In `GameBottomView.setFoodInfo`, remove cooldown buttons from normal generator waiting states. For a bubble-frozen item identified by the existing `splitChance`/freeze state, show a video affordance rather than a coin amount. In `clearCDTimeBtnClick`, first validate the selected item is frozen due to bubble behavior, then call `CMgr.helper.showRewardAd(0, callback)`; on success clear the frozen state, restore its eligible count, refresh board and selected-item UI, and notify existing tutorial hooks. On false/error/no callback leave model, time and inventory unchanged.

- [ ] **Step 5: Confirm UI timers cannot reintroduce generator cooldown**

Update `FoodItemView.updateState/showMask/timeUpdate` and `GridFoodItemModel.init` only as needed so only bubble statuses attach to time observers. Check pre-existing saved generator timers, bubble timers, unlimited random sources, fixed source depletion, and merge outputs. Use `rg -n "isEnough\\(ItemID\\.GOLD|updateItemNum\\(ItemID\\.GOLD, -|getOpenPrice"` under `assets/Script/logic` to audit active gold spend paths; change each remaining core-reachable debit to a rewarded-ad path or document why it is not a debit. Do not add or run tests.

### Task 4: Add top HUD and local mock cashout interaction

**Files:**
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/CoreActionView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/config/Config.ts`
- Read/modify only if needed: `2.x_源码_装扮梦想家园_2.4.13ts/assets/game/prefab/GameView.prefab`

**Interfaces:**
- Consumes `LevelMgr.getCashBalance()`, `getLevel()`, `getOrderExp()`, `getExpRequired()`, `simulateCashout()`, and `PLAYER_PROGRESS_CHANGED`.
- Produces a visible `PlayerProgressHud` at the top of the core game screen with amount, cashout action, level and EXP progress.

- [ ] **Step 1: Add the progress-change event and HUD subscription**

Add `EventName.PLAYER_PROGRESS_CHANGED = 'PLAYER_PROGRESS_CHANGED'` and have `CoreActionView` subscribe/unsubscribe in `onLoad/onDestroy`, following its existing catalog-change listener. Read the state getters on initialization and after every event. If `LevelMgr.getRecoveryMessage()` is nonempty, show it once through `TipC.instance().showToast(...)` without removing the recovery backup.

- [ ] **Step 2: Create a compact screenshot-matched top bar**

Build code-driven Cocos nodes inside `CoreActionView` at the safe top of existing `TopInfo`: left amount text formatted `Rp` plus two decimals and a `提现` button; right `LV N`, progress fill, and `current/required EXP`. Use non-blocking graphics/text except the button, which must stop propagation and not intercept board gestures. Update labels/fill without recreating the board.

- [ ] **Step 3: Implement guarded local cashout**

On the button click, show the requirement when level <10, show an empty-balance message at zero, and otherwise confirm the local simulation. After confirmation call `simulateCashout()`, update the HUD via the event, and show the returned amount as simulated success. Do not reference network, bank, wallet or SDK payout interfaces.

- [ ] **Step 4: Verify reset and older saved data behavior**

Confirm existing `CoreActionView.resetData()` clears the new LocalValue key through `StorageHelper.clear()` and still restores the four audio keys. Inspect old and new save initialization so a missing new record retains the prior level and starts new progress at 0 EXP/Rp0.00.

- [ ] **Step 5: Review layout and event lifecycle**

Use prefab dimensions and current visible-size calculations to verify the HUD does not overlap the order/customer controls or top safe area. Inspect that listeners are removed and repeated initialization does not duplicate nodes. If Cocos is launchable, visually verify 9:16 and current editor preview resolutions; otherwise document the unavailable visual/runtime check.

### Task 5: Cross-system source audit and available verification

**Files:**
- Review all files changed in Tasks 1–4.
- Update: `docs/superpowers/specs/2026-09-23-order-rewards-level-cashout-design.md` only if implementation findings require an approved spec clarification.

**Interfaces:**
- Consumes every interface and behavior defined above.
- Produces a short verification record with exact commands/results and known limits.

- [ ] **Step 1: Audit level APIs and progression consumers**

Run `rg -n "getLevel\\(|getPersonNum\\(|getTotalExp\\(|checkLevel\\(|updateExp\\(" 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic` and inspect each remaining consumer for a single level source and correct high-level fallback.

- [ ] **Step 2: Audit spending/ad paths and cooldown state**

Run `rg -n "ItemID\\.GOLD|showRewardAd|OUT_ITEM_WITH_OPEN_CD|OUT_RANDOM_AND_CD|setTime\\(" 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/buy 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/level`. Confirm positive gold grants are untouched, no core debit remains, and bubble and generator timer paths are distinct.

- [ ] **Step 3: Perform static source checks**

Check imports/exports, unique class members, event subscribe/unsubscribe pairs, storage-key names, `LocalValue` object serialization, Cocos 2.4 APIs, and all changed source for whitespace errors. No tests or test files are added/run under the current session constraint.

- [ ] **Step 4: Attempt runtime validation only if the known launch blocker has changed**

The prior Cocos 2.4.13 CLI launch aborted in Electron/`NSApplication`, while `/usr/bin/open` returned LaunchServices `-10822`, with no build output. Do not repeat the same failed launch unless that environment has changed. If it has, open this project in Cocos Creator 2.4.13, build Web Mobile once, then manually verify a unique order award, each threshold band including Lv21+, generator immediate production, bubble ad success/failure, the Lv9/Lv10 payout boundary, persistence after restart, and data reset. Report exact unavailable checks rather than inferring success.

## Plan Self-Review

- Spec coverage: order/cash/EXP award, threshold progression, existing-level migration, duplicate prevention, ordinary generator wait removal, bubble rewarded ads, spending audit, header display, local cashout, reset behavior, malformed state recovery and Cocos verification boundaries are assigned to Tasks 1–5.
- Placeholder scan: no placeholder markers or unspecified implementation actions are used; methods and local state fields are named above.
- Type consistency: `recordCompletedOrder`, `simulateCashout`, all HUD getters and event name are defined once and consumed consistently.
- Session constraint: test file/command steps are omitted; source-path reviews and a conditional Creator manual run are the available verification strategy.
- Repository state: project files and current additions are largely untracked in an unborn repository; this plan does not instruct staging or committing unrelated project files.
