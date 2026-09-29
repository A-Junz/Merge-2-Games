# 核心合成模式重构实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将 Cocos Creator 2.4.13 工程从“大厅驱动的家园经营游戏”改造成“直接进入合成界面的核心合成模式”。

**Architecture:** 保留现有 `GameView` 作为唯一主玩法界面，以一个精简的核心启动流程加载配置、公共资源、合成分包、音频和 SDK，然后直接创建 `GameView`。不删除旧模块文件，改为移除启动初始化和 UI 入口；底层事件总线继续服务于棋盘、顾客和保留资源同步，但产品红点及被裁剪系统事件不再注册。

**Tech Stack:** Cocos Creator 2.4.13、TypeScript、Cocos Prefab/Scene、现有分包和 SDK 架构。

**Spec:** `docs/superpowers/specs/2026-09-22-core-merge-mode-design.md`

## Global Constraints

- 必须使用 Cocos Creator 2.4.13 工程结构和现有 TypeScript/Prefab 资源格式。
- 启动后不得显示 `LobbyView`，必须直接进入 `GameView`。
- 保留合成棋盘、顾客订单、等级经验、金币/代币/经验、在线/离线奖励、新手引导、声音设置、广告/分享/渠道 SDK 和配置资源加载。
- 完全移除能量资源：不初始化、不显示、不消耗、不恢复、不购买、不通过活动或广告发放。
- 删除产品层红点及已删除系统入口；底层事件总线仅保留棋盘、顾客和保留资源同步所需事件。
- 不直接删除旧模块和资源文件，除非确认它们会被核心启动流程或保留 UI 引用。
- 不改动用户已有的其他未提交文件。

## Review Focus

- 启动顺序：隐私/登录/SDK 初始化完成后，不能回到大厅或依赖大厅 UI。
- 顾客订单：裁剪任务、市场和场景后，`BuyView` 仍能加载、交付订单和发放金币/经验。
- 能量引用：任何运行时路径和 Prefab 入口都不能再读取、显示、消耗或发放能量。
- UI 残留：`MoneyView`、`GameBottomView` 和 `GameView` Prefab 中不能留下商店、仓库、任务、活动、CD 或能量购买交互。
- 活动兼容性：在线/离线奖励仍可显示，但不能依赖每日任务、红点、活动礼包或能量奖励。

---

### Task 1: 建立核心模式启动流程

**Files:**
- Create: `tools/verify_core_merge_mode.py`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/Main.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/lobby/LobbyC.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/login/LoginView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/start/StartC.ts`

**Interfaces:**
- Consumes: existing `ModuleManager`, `PrivacyC`, `SDKManager`, `CMgr`, `GameC`, `ActivityMgr`, `ActivityC`.
- Produces: one boot path that loads core resources and invokes `GameC.instance().intoLayer()` without creating `LobbyView`.

- [ ] **Step 1: Add a failing static source contract test before editing**

  Create `tools/verify_core_merge_mode.py` with checks for the future core boot path, forbidden energy references, removed-system entry points and retained level/settings controls. Run it before production edits; it must fail against the current lobby-driven project. Use `rg` to record every `LobbyView` creation and every `LobbyC.firstShow()`/`LobbyC.showView()` call so the failure is attributable to the existing boot path.

- [ ] **Step 2: Implement the core boot branch**

  Refactor `LobbyC` so its resource-loading callback initializes language/config, runs privacy/start flow, initializes only retained services, and calls `GameC.instance().intoLayer()`. Do not call `showView()`, `SceneC`, `TaskC`, `LevelC.intoLayer()`, `GiftBoxC`, `WeeklyC`, or other removed controllers.

- [ ] **Step 3: Preserve login and start-screen contracts**

  Update `LoginView` and `StartC` callbacks to target the core boot method instead of a lobby screen. Keep `PrivacyC`, channel login, `CMgr.helper.startGame()`, and SDK initialization intact.

- [ ] **Step 4: Run static startup verification**

  Run:

  ```bash
  rg -n "LobbyView|firstShow\(|showView\(\)|GameC\.instance\(\)\.intoLayer" 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/Main.ts 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/lobby/LobbyC.ts 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/login/LoginView.ts 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/start/StartC.ts
  ```

  Expected: no core boot path constructs `LobbyView`; exactly one core path enters `GameC`.

- [ ] **Step 5: Commit**

  ```bash
  git add tools/verify_core_merge_mode.py 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/Main.ts 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/lobby/LobbyC.ts 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/login/LoginView.ts 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/start/StartC.ts
  git commit -m "refactor: boot directly into core merge game"
  ```

### Task 2: Remove energy and energy/CD behavior

**Files:**
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/item/model/ItemMgr.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/item/model/ItemModel.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/item/ItemC.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/GameC.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/model/FoodTypeModel.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/model/FoodItemModel.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/model/GridFoodItemModel.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/FoodItemView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameBottomView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/item/view/MoneyView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/item/view/EnergyNumView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/item/view/BuyEnergyView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/activity/ActivityC.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/giftbox/GiftBoxC.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/extention/gevent/EventCheckPoint.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/extention/gevent/GameEventAdapter.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/config/Config.ts`

**Interfaces:**
- Consumes: retained `ItemMgr`, `BagManager`, food output state and activity reward callbacks.
- Produces: merge actions no longer check or consume energy; no energy timer, UI, purchase, CD-clear or reward path remains.

- [ ] **Step 1: Extend the static energy-reference test**

  Use the existing `tools/verify_core_merge_mode.py` from Task 1 and add/enable its energy assertions. Run a repository search for `ItemID.ENERGY`, `showBuyEnergyView`, `ENERGY_TIME`, `energyLabel`, `clearCDTimeBtnClick`, `isCostEnergy`, and `VEDIO_ENERGY` in retained runtime paths and record the baseline matches.

- [ ] **Step 2: Remove energy from the item model and timer**

  Remove energy timer registration and automatic recovery from `ItemMgr`. Remove energy item initialization and any energy-specific buying counters. Keep generic gold/token/experience resource updates intact. Leave legacy energy columns in generated data files untouched unless the compiler requires a schema change; runtime code must not construct or read an energy item.

- [ ] **Step 3: Remove energy consumption from the merge flow**

  Remove `isCostEnergy` from the runtime food model/view chain and change `GameView` so all food output follows the normal output path. Remove energy deduction, energy-insufficient toast and the `BuyEnergyView` fallback. Preserve food output, CD state where it is intrinsic to the food, and customer order behavior.

- [ ] **Step 4: Remove energy UI and reward paths**

  Disable energy nodes in `MoneyView`, remove the energy purchase handlers, remove CD-clear/energy actions from `GameBottomView`, and remove energy from offline/online/gift reward paths. Convert retained activity rewards to existing gold/token/experience/food rewards rather than dropping the claim callback. Keep ad callbacks for the retained rewards.

- [ ] **Step 5: Run energy absence verification**

  Run:

  ```bash
  rg -n "ItemID\.ENERGY|showBuyEnergyView|ENERGY_TIME|VEDIO_ENERGY|energyLabel|clearCDTimeBtnClick|isCostEnergy" 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/config
  ```

  Expected: no matches in retained runtime code; any remaining matches must be commented historical code or unused removed-module source and must be recorded in the ledger. Run `python3 tools/verify_core_merge_mode.py`; expected result is still failing until later tasks complete.

- [ ] **Step 6: Commit**

  ```bash
  git add 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script
  git commit -m "refactor: remove energy resource and cooldown purchase flow"
  ```

### Task 3: Prune the core UI and add level/settings controls

**Files:**
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameBottomView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/item/view/MoneyView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/game/prefab/GameView.prefab`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/public/prefabs/GameBottomView.prefab`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/public/prefabs/MoneyView.prefab`

**Interfaces:**
- Consumes: `LevelC`, `SoundC`, `GameView`, existing resource HUD and `BuyView`.
- Produces: a single core screen with customer orders, merge board, resources, a top-left level button, and a top-left settings button.

- [ ] **Step 1: Capture prefab node baseline**

  Parse the three Prefab JSON files and list nodes/handlers containing `Storage`, `Market`, `Task`, `DailyTask`, `Special`, `Lucky`, `Box`, `Gift`, `Egg`, `shop`, `energy`, `clear`, and `setting`. This baseline identifies exactly which nodes must be disabled or removed.

- [ ] **Step 2: Remove unwanted GameBottom interactions**

  Remove storage, CD-clear, energy purchase and other deleted-system handlers from `GameBottomView`. Preserve food information, sell/delete behavior, customer order interaction, and the internal `CLOSE_GAME_VIEW` handling if still required by the retained UI.

- [ ] **Step 3: Add top-left level/settings controls**

  Reuse the existing `MoneyView` level node and settings node, move both to the top-left with portrait-safe margins, and keep their existing `LevelC.instance().intoLayer()` and `SoundC.instance().intoLayer()` handlers. If the existing node is not reachable from the core screen, add a small `GameView` overlay with the same two handlers instead of restoring any lobby UI.

- [ ] **Step 4: Prune prefab nodes**

  Disable or remove obsolete nodes and event handlers from `GameView.prefab`, `GameBottomView.prefab`, and `MoneyView.prefab`: storage, market, tasks, special tasks, activities, spin/box/egg/gift, more games, energy controls, CD clear and scene/build controls. Keep customer order prefabs, level/settings nodes and retained resource labels.

- [ ] **Step 5: Run UI static verification**

  Run the baseline parser again and search for forbidden handler names. Expected: only retained `BuyView`/customer nodes, level, setting, resource HUD and guide nodes remain active; no forbidden node has an active click handler. Run `python3 tools/verify_core_merge_mode.py`; expected result is still failing only on later tasks.

- [ ] **Step 6: Commit**

  ```bash
  git add 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/item/view 2.x_源码_装扮梦想家园_2.4.13ts/assets/game/prefab/GameView.prefab 2.x_源码_装扮梦想家园_2.4.13ts/assets/public/prefabs/GameBottomView.prefab 2.x_源码_装扮梦想家园_2.4.13ts/assets/public/prefabs/MoneyView.prefab
  git commit -m "feat: simplify merge screen controls"
  ```

### Task 4: Keep customer/order and retained activities independent of removed systems

**Files:**
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/activity/model/ActivityMgr.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/activity/ActivityC.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/public/bag/BagManager.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/buy/model/BuyMgr.ts`

**Interfaces:**
- Consumes: `BuyView` and customer models, `ActivityMgr`/`ActivityC`, `BagManager`, `CMgr` reward ads.
- Produces: customer orders and online/offline rewards that do not initialize or update daily tasks, special tasks, red tips, market, storage or energy.

- [ ] **Step 1: Remove daily-task coupling from retained paths**

  Remove `DailyTaskMgr` updates from `BagManager`, `GameView`, `ActivityMgr`, customer delivery and SDK reward callbacks where those updates are only for the removed daily-task system. Keep the actual gold/token/experience updates.

- [ ] **Step 2: Remove red-tip and game-event coupling from retained activities**

  Make online/offline reward state visible through the activity views without registering product red tips or opening removed event-gated UI. Keep the timer and ad/share reward callbacks needed for online/offline claims, and grant only retained gold/token/experience/food resources.

- [ ] **Step 3: Verify customer/order initialization**

  Ensure `BuyView` initializes from `BuyMgr` without `TaskMgr`, `MarketMgr`, `StorageMgr`, `SceneMgr`, or `SpecialtaskMgr`. Preserve customer item requirements, delivery, reward payout and next-customer rotation.

- [ ] **Step 4: Run static dependency verification**

  Search retained files for imports and singleton calls to `TaskMgr`, `DailyTaskMgr`, `MarketMgr`, `StorageMgr`, `SceneMgr`, `SpecialtaskMgr`, `LuckyspinMgr`, `BoxpopMgr`, `CrazyClickC`, `GiftBoxC`, and `WeeklyC`. Expected: no retained startup/UI path invokes them.

- [ ] **Step 5: Commit**

  ```bash
  git add 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/activity 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/public/bag 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/buy
  git commit -m "refactor: isolate retained customer and activity flows"
  ```

### Task 5: Disable product red tips and removed-system event entry points

**Files:**
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/extention/redtip/RedTipMgr.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/extention/gevent/GameEventAdapter.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/extention/gevent/GameEventConfig.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameView.ts`
- Modify: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/view/GameBottomView.ts`

**Interfaces:**
- Consumes: internal `GEvent`/`GameEventAdapter` notifications used by the merge board and guide.
- Produces: no visible product red dots and no event-driven entry into removed systems; core board/customer/resource events still propagate.

- [ ] **Step 1: Identify event consumers required by core gameplay**

  Search all `EventName` emissions/listeners used by `GameView`, `BuyView`, `GridItemView`, `ItemMgr`, `LevelMgr`, `ActivityC`, and `GuideMgr`. Mark board/customer/resource/guide events as retained and task/market/scene/activity-special events as removable.

- [ ] **Step 2: Disable product red-tip output**

  Change `RedTipMgr` or its core-mode call sites so add/remove operations for tasks, store, build, daily task, lucky spin, box, special task, egg, recorder and market do not create visible nodes. Do not break guide overlays or ordinary UI event dispatch.

- [ ] **Step 3: Remove removed-system event entry points**

  Remove listeners/callbacks that open task, market, storage, scene, special activity, spin, box, egg, gift or weekly views. Keep listeners that update the board, customer orders, retained resources, level and guide.

- [ ] **Step 4: Run event/red-tip verification**

  Run a static search for removed event handlers and red-tip type usage in the retained boot and GameView paths. Expected: no removed-system view opening or red-tip node creation is reachable from the core screen.

- [ ] **Step 5: Commit**

  ```bash
  git add 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/extention 2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game
  git commit -m "refactor: remove product red tips and obsolete event entries"
  ```

### Task 6: Compile, run and verify the reduced game in Cocos Creator 2.4.13

**Files:**
- Modify only files required by compiler/runtime errors found during verification.
- Create: `docs/superpowers/verification/2026-09-22-core-merge-mode-verification.md`

**Interfaces:**
- Consumes: all completed core-mode changes.
- Produces: Cocos Creator 2.4.13 runtime evidence and a checklist of retained/removable features.

- [ ] **Step 1: Run the Cocos TypeScript compile/preview command**

  Use the existing Cocos Creator 2.4.13 installation and open the project. Start a preview server with the project as the target. Expected: QuickCompiler completes without TypeScript errors.

- [ ] **Step 2: Verify startup UI**

  Open the preview and confirm the first gameplay screen is `GameView`, with no `LobbyView`. Confirm the top-left level and settings controls are visible.

- [ ] **Step 3: Verify core gameplay**

  Exercise food generation, drag/merge, customer order delivery, gold/experience changes, level popup/reward, settings popup, guide overlay and online/offline reward view.

- [ ] **Step 4: Verify removals**

  Confirm no visible or clickable entry exists for home/build, tasks, daily tasks, market, storage, special events, red dots, energy, energy purchase or CD clear.

- [ ] **Step 5: Record evidence**

  Write the commands, compiler result, preview URL, observed retained systems and any platform-only limitations into `docs/superpowers/verification/2026-09-22-core-merge-mode-verification.md`.

- [ ] **Step 6: Final verification run**

  Re-run the Cocos compile/preview command and the static searches from Tasks 1–5. Expected: compile succeeds, core screen loads, retained flows remain usable, and no removed entry is reachable.
