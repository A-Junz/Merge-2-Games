# 初始棋盘解锁与订单规则 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将固定初始棋盘、三态解锁交互、默认订单序列、新订单上限/合成链开放规则、重置按钮和存档兼容完整接入 Unity 原型。

**Architecture:** Core 层负责初始棋盘数据、每格解锁状态、拖拽权限和解锁传播；OrderModel 负责等级规则与前三个固定订单；Persistence 保存解锁状态和默认订单进度；Presentation 只负责遮罩、按钮和状态刷新。初始布局集中在 `InitialBoardLayout`，避免 Core 与 UI 各自维护一份棋盘表。

**Tech Stack:** Unity 2022.3.62f3c1、C#、Unity UI Prefab、NUnit EditMode/PlayMode tests、现有 JSON `SaveService`。

**Spec:** `docs/superpowers/specs/2026-09-22-initial-board-unlock-orders-design.md`

## Global Constraints

- 棋盘固定为 7 列 × 9 行，共 63 格；行列坐标从 1 开始。
- 完全解锁格支持移动、交换、合成；半解锁格不可作为源，只能被完全解锁格合成；未解锁格不可参与交互。
- 新游戏中心格为第 5 行第 4 列的面粉 101；四邻接初始半解锁，其余初始未解锁。
- 新游戏激活订单数为 1；默认订单序列为 102、103、104，完成第三单后恢复随机订单。
- 订单上限为：1 级 1、2 级 2、3～5 级 3、6～10 级 4、11 级以上 5。
- 合成链开放数量为 `min(10, 1 + level / 2)`，使用整数除法。
- 半解锁遮罩覆盖右半区域，未解锁遮罩覆盖整个图标区域。
- 旧存档必须继续读取；缺失解锁字段时按中心完全解锁、四邻接半解锁、其余未解锁迁移。

## Review Focus

- 初始棋盘表的 63 个坐标是否全部映射到合法 `ItemCatalog` ID；由 Task 1 的逐格测试覆盖。
- 半解锁目标是否只允许完全解锁源进行同链同级合成，避免误放置/交换；由 Task 2 的权限矩阵测试覆盖。
- 解锁传播是否不会把锁定格错误地变成完全解锁；由 Task 2 的状态传播测试覆盖。
- 完成固定订单 1/2 后是否严格进入固定订单 2/3，而不是被随机订单替换；由 Task 3 的序列测试覆盖。
- 重置或旧存档迁移是否丢失解锁状态、订单进度或金币；由 Task 4 的存档/重置测试覆盖。

---

### Task 1: 更新需求文档与初始棋盘数据

**Files:**
- Modify: `docs/首个版本系统规则.md`
- Create: `UnityMergePrototype/Assets/Scripts/Core/InitialBoardLayout.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/CellUnlockState.cs`
- Test: `UnityMergePrototype/Assets/Tests/EditMode/InitialBoardLayoutTests.cs`

**Interfaces:**
- Produces `InitialBoardLayout.ItemIds` (`IReadOnlyList<int>`, length 63).
- Produces `InitialBoardLayout.UnlockStates` (`IReadOnlyList<CellUnlockState>`, length 63).
- Produces `InitialBoardLayout.CenterCellIndex` and `InitialBoardLayout.Build(BoardModel board)` for `GameState.CreateNew`.

- [ ] **Step 1: Write the failing layout test**

  在 `InitialBoardLayoutTests` 中断言 63 个坐标的 ID，重点断言中心 `row=5,col=4` 为 101，四邻接为 102/105/101/102，且布局表中所有 ID 都由 `ItemCatalog.IsValidItem` 接受。

  ```csharp
  [Test]
  public void InitialLayout_MatchesNineBySevenSpec()
  {
      var catalog = new ItemCatalog();
      Assert.AreEqual(63, InitialBoardLayout.ItemIds.Count);
      Assert.AreEqual(101, InitialBoardLayout.ItemIds[(5 - 1) * 7 + (4 - 1)]);
      foreach (int itemId in InitialBoardLayout.ItemIds)
          Assert.IsTrue(catalog.IsValidItem(itemId), $"非法初始物品: {itemId}");
  }
  ```

- [ ] **Step 2: Run the layout test and verify it fails**

  Run the Unity EditMode test filter `InitialBoardLayoutTests` or the repository's static NUnit runner. Expected: compile/test failure because `InitialBoardLayout` and `CellUnlockState` do not exist.

- [ ] **Step 3: Add the layout constants and update the rule document**

  Create `CellUnlockState.cs` with `Locked`, `SemiUnlocked`, `Unlocked`. Create `InitialBoardLayout.cs` with the 63 IDs from the spec, center index `31`, and unlock states where index 31 is `Unlocked`, indices 24/30/32/38 are `SemiUnlocked`, and all other indices are `Locked`.

  Add a “初始棋盘布局与解锁” section to `docs/首个版本系统规则.md`, including the 9×7 table, the three states, the semi-unlocked merge rule, and the four-neighbor propagation rule. Update order quantity, chain opening, initial order sequence, and reset sections in the same document.

- [ ] **Step 4: Run the layout tests and verify they pass**

  Run the same `InitialBoardLayoutTests` filter. Expected: all layout and state assertions pass with no warnings.

- [ ] **Step 5: Commit the documentation/data unit**

  ```bash
  git add docs/首个版本系统规则.md UnityMergePrototype/Assets/Scripts/Core/InitialBoardLayout.cs UnityMergePrototype/Assets/Scripts/Core/CellUnlockState.cs UnityMergePrototype/Assets/Tests/EditMode/InitialBoardLayoutTests.cs
  git commit -m "feat: define initial board layout and unlock states"
  ```

### Task 2: Add board unlock state and interaction rules

**Files:**
- Modify: `UnityMergePrototype/Assets/Scripts/Core/BoardModel.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Core/GameState.cs`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/BoardModelTests.cs`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/GameStateFeatureTests.cs`

**Interfaces:**
- Adds `BoardModel.GetUnlockState(int index)`, `SetUnlockState(int index, CellUnlockState state)`, and `UnlockStateSnapshot()`.
- `BoardModel.HandleDrop` remains the public drop API and returns `DropResult.Rejected` for disallowed source/target combinations.
- Adds `GameState.ApplyInitialBoard()` and exposes `Board.UnlockStateSnapshot()` for persistence/presentation.

- [ ] **Step 1: Write failing permission and propagation tests**

  Add tests for: locked source rejection; semi source rejection; unlocked source to semi target with different item rejection and no mutation; unlocked source to semi target with same item merges and upgrades the target; newly eligible locked neighbors become semi; unlocked-to-unlocked move/swap/merge remains valid.

  ```csharp
  [Test]
  public void SemiUnlockedTarget_AllowsOnlyMergeFromUnlockedSource()
  {
      var catalog = new ItemCatalog();
      var board = new BoardModel(catalog);
      board.SetItemAt(0, 101);
      board.SetItemAt(1, 101);
      board.SetUnlockState(0, CellUnlockState.Unlocked);
      board.SetUnlockState(1, CellUnlockState.SemiUnlocked);

      var result = board.HandleDrop(0, 1);

      Assert.AreEqual(DropResultType.Merged, result.Type);
      Assert.AreEqual(CellUnlockState.Unlocked, board.GetUnlockState(1));
      Assert.AreEqual(102, board.GetItemAt(1));
  }
  ```

- [ ] **Step 2: Run the new board tests and verify they fail**

  Expected: the tests fail because `BoardModel` currently has no unlock state and accepts all cells for normal drops.

- [ ] **Step 3: Implement the unlock state and drop matrix**

  Keep direct `BoardModel` construction backward-compatible by initializing its unlock array to `Unlocked`; `GameState.CreateNew` immediately applies `InitialBoardLayout`. In `HandleDrop`, reject a non-unlocked source, reject a locked target, allow a semi target only for same-ID merge, and after a successful semi merge call `PromoteNeighborsToSemiUnlocked` until the rule reaches a stable state. Do not allow normal move/swap into a semi target.

  Make `GameState.CreateNew` call `InitialBoardLayout.Build`, and ensure `MarkSeen` is not called merely for locked initial contents.

- [ ] **Step 4: Run board/core tests and verify they pass**

  Run `BoardModelTests`, `GameStateFeatureTests`, and the existing core regression runner. Expected: all old unlocked-board tests remain green and all new permission/propagation tests pass.

- [ ] **Step 5: Commit the core interaction unit**

  ```bash
  git add UnityMergePrototype/Assets/Scripts/Core/BoardModel.cs UnityMergePrototype/Assets/Scripts/Core/GameState.cs UnityMergePrototype/Assets/Tests/EditMode/BoardModelTests.cs UnityMergePrototype/Assets/Tests/EditMode/GameStateFeatureTests.cs
  git commit -m "feat: enforce board unlock interaction rules"
  ```

### Task 3: Implement order counts, chain opening, and fixed default orders

**Files:**
- Modify: `UnityMergePrototype/Assets/Scripts/Core/OrderModel.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Core/GameState.cs`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/RulesSystemTests.cs`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/OrderAndSaveTests.cs`

**Interfaces:**
- `OrderModel.InitialOrderCount` becomes `1`.
- `OrderModel.MaxOrdersForLevel(int level)` returns `1/2/3/4/5` for the specified level ranges.
- `GameState.AvailableChainCountForLevel(int level)` returns `min(10, 1 + level / 2)`.
- `OrderModel.DefaultOrderProgress` is an integer persisted alongside orders; `CreateDefaultOrder(int index, DeterministicRandom rng)` returns 102, 103, or 104 with the existing reward/character generation.

- [ ] **Step 1: Write failing order-rule tests**

  Add assertions for initial active count 1, level max-order ranges, chain counts at levels 1/2/3/4/6/20, fixed order IDs 102→103→104, and random generation after the third fixed order.

- [ ] **Step 2: Run the order tests and verify they fail**

  Expected: current implementation reports two active orders, old level caps, old chain count, and random first orders.

- [ ] **Step 3: Implement the order rule changes**

  Change `InitialOrderCount`, `MaxOrdersForLevel`, and `AvailableChainCountForLevel`. Initialize the first order as 102 and set `DefaultOrderProgress` to the next fixed order index. In the `ActiveCount == 1` submit path, replace the current order with the next fixed order while progress is below 3; after 104, use the existing random roll path. Keep all demand candidates and reward calculations unchanged except for the already-implemented reward multiplier.

- [ ] **Step 4: Run order tests and full pure-core regression tests**

  Expected: fixed-order sequence, order caps, chain opening, reward multiplier, refresh timing, and existing merge/order tests all pass.

- [ ] **Step 5: Commit the order unit**

  ```bash
  git add UnityMergePrototype/Assets/Scripts/Core/OrderModel.cs UnityMergePrototype/Assets/Scripts/Core/GameState.cs UnityMergePrototype/Assets/Tests/EditMode/RulesSystemTests.cs UnityMergePrototype/Assets/Tests/EditMode/OrderAndSaveTests.cs
  git commit -m "feat: add initial order sequence and revised progression caps"
  ```

### Task 4: Persist unlock state, default-order progress, and reset state

**Files:**
- Modify: `UnityMergePrototype/Assets/Scripts/Core/GameState.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Persistence/SaveService.cs`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/OrderAndSaveTests.cs`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/GameStateFeatureTests.cs`

**Interfaces:**
- Increase `GameState.SaveVersion` to the next version.
- Save `boardUnlockStates` as 63 integer enum values and `defaultOrderProgress` as an integer.
- `GameState.CreateForRestore` accepts optional unlock states and default-order progress while retaining the existing two-argument overload for callers.

- [ ] **Step 1: Write failing persistence tests**

  Add round-trip tests that save/load a partially unlocked board and fixed-order progress, plus a legacy JSON test with no unlock field that expects center/neighbor migration rather than startup failure.

- [ ] **Step 2: Run persistence tests and verify they fail**

  Expected: the current JSON model drops unlock state and default-order progress.

- [ ] **Step 3: Implement versioned persistence and reset construction**

  Add fields to `SaveData`, validate length and enum range, serialize/restore them, and migrate versions 1–2 with the deterministic center/neighbor unlock layout. Ensure `GameState.CreateNew` and reset both use the same initial-state constructor.

- [ ] **Step 4: Run persistence tests and verify they pass**

  Expected: new saves round-trip all fields and old saves load without exceptions or data corruption.

- [ ] **Step 5: Commit the persistence unit**

  ```bash
  git add UnityMergePrototype/Assets/Scripts/Core/GameState.cs UnityMergePrototype/Assets/Scripts/Persistence/SaveService.cs UnityMergePrototype/Assets/Tests/EditMode/OrderAndSaveTests.cs UnityMergePrototype/Assets/Tests/EditMode/GameStateFeatureTests.cs
  git commit -m "feat: persist board unlock and default order progress"
  ```

### Task 5: Add lock overlays, interaction gating, and reset UI

**Files:**
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/BoardCellView.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/GameUiView.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/GamePresenter.cs`
- Modify: `UnityMergePrototype/Assets/Editor/UiPrefabBuilder.cs`
- Modify: `UnityMergePrototype/Assets/Resources/UI/Prefabs/BoardCell.prefab`
- Modify: `UnityMergePrototype/Assets/Resources/UI/Prefabs/GameUi.prefab`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/UiPrefabReferenceTests.cs`
- Modify: `UnityMergePrototype/Assets/Tests/PlayMode/DragInteractionTests.cs`

**Interfaces:**
- `BoardCellView.SetUnlockState(CellUnlockState state)` updates overlay visibility and raycast/drag behavior.
- `GameUiView.RestartButton` exposes the serialized reset button.
- `GamePresenter.NotifyRestart()` binds to the reset button and calls `GameState.CreateNew`, saves, and refreshes all views.

- [ ] **Step 1: Write failing UI/reference tests**

  Assert that BoardCell has `SemiLockOverlay` and `FullLockOverlay`, GameUi has `RestartButton`, RestartButton is placed after/right of GeneratorButton, and presenter refreshes locked cells with the correct visual state. Add a PlayMode drag test proving a semi-unlocked cell cannot start a drag.

- [ ] **Step 2: Run UI tests and verify they fail**

  Expected: current prefabs lack the new overlay references and serialized restart button.

- [ ] **Step 3: Implement the UI bindings and prefab assets**

  Add overlay Images to `BoardCellView` and builder/prefab: full overlay for `Locked`, right-half overlay for `SemiUnlocked`, hidden overlay for `Unlocked`. In `RefreshBoard`, pass each model state into the cell. Gate `NotifyCellBeginDrag` before creating a drag ghost. Add the reset button to `GameUiView`, bind `NotifyRestart`, and place it immediately right of the generator in builder and serialized prefab.

- [ ] **Step 4: Run prefab/UI tests and verify they pass**

  Expected: reference checks, prefab contract checks, and drag interaction tests pass. Static prefab checks must find all 63 cells, both overlays, and the restart entry point.

- [ ] **Step 5: Commit the presentation unit**

  ```bash
  git add UnityMergePrototype/Assets/Scripts/Presentation/BoardCellView.cs UnityMergePrototype/Assets/Scripts/Presentation/GameUiView.cs UnityMergePrototype/Assets/Scripts/Presentation/GamePresenter.cs UnityMergePrototype/Assets/Editor/UiPrefabBuilder.cs UnityMergePrototype/Assets/Resources/UI/Prefabs/BoardCell.prefab UnityMergePrototype/Assets/Resources/UI/Prefabs/GameUi.prefab UnityMergePrototype/Assets/Tests/EditMode/UiPrefabReferenceTests.cs UnityMergePrototype/Assets/Tests/PlayMode/DragInteractionTests.cs
  git commit -m "feat: show board locks and add reset game button"
  ```

### Task 6: Final requirements verification

**Files:**
- Verify: `docs/首个版本系统规则.md` and all changed source, tests, and prefabs

- [ ] **Step 1: Re-read the requirements document against the approved spec**

  Confirm the Task 1 document update contains the exact initial 9×7 table, center/neighbor lock behavior, semi-only merge rule, revised order caps, revised chain formula, default orders, reset button, and save migration behavior.

- [ ] **Step 2: Run static checks**

  ```bash
  git diff --check
  rg -n "InitialBoardLayout|CellUnlockState|RestartButton|DefaultOrderProgress|MaxOrdersForLevel|AvailableChainCountForLevel" UnityMergePrototype/Assets/Scripts UnityMergePrototype/Assets/Tests
  ```

- [ ] **Step 3: Compile and run core regression tests**

  Compile Core, Persistence, Presentation, and all EditMode/PlayMode test sources with the Unity 2022.3 managed assemblies. Run the pure-core regression runner and confirm `core regression tests PASS`.

- [ ] **Step 4: Run Unity EditMode and PlayMode batch tests**

  Use the Unity 2022.3.62f3c1 batch test command. If the Licensing Client IPC failure recurs, record the exact log and report that limitation without claiming the full Unity suite passed.

- [ ] **Step 5: Review final changes and preserve the workspace state**

  Check the final file list, confirm no unrelated files were modified, and report any inability to commit caused by the workspace `.git/index.lock` permission restriction.
