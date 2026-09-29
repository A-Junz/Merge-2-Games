# Unity 二合核心玩法原型 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在同级创建 `UnityMergePrototype` Unity 2022.3 工程，实现可在 macOS Editor Play Mode 中运行的 7×9 核心二合原型。

**Architecture:** 使用纯 C# 核心模型保存棋盘、物品、能量、订单和存档状态，使用 MonoBehaviour 和 uGUI 只做输入、渲染和生命周期适配。主场景由编辑器建置脚本产生，运行时使用色块、文字和按钮构成功能界面。

**Tech Stack:** Unity 2022.3.62f3c1, C#, uGUI, Unity Test Framework, `JsonUtility`, `Application.persistentDataPath`.

**Spec:** `docs/superpowers/specs/2026-09-18-unity-merge-core-prototype-design.md`

## Global Constraints

- 原工程 `2.x_源码_装扮梦想家园_2.4.13ts` 不修改。
- Unity 工程目录必须为 `UnityMergePrototype` 并使用 Unity 2022.3.62f3c1。
- 验收方式是 macOS Unity Editor Play Mode，1080×1920 竖屏 Canvas。
- 棋盘固定为 7×9，共 63 个全部开放的格子。
- 原型含两条各 5 级物品链、3 个循环订单和 1 个独立生成器。
- 初始能量 30，上限 30，生成一个物品消耗 1，每 30 秒恢复 1 点。
- 原型不迁移原美术、音效、广告、渠道 SDK、活动、商城、家园装修、仓库、格子解锁和外部打包。
- 按用户要求跳过本次文档提交；实施过程不包含 Git commit 步骤。

## Review Focus

- 拖到原格、无效格索引或棋盘外：操作被拒绝且原状态不变；由 Task 2 的 `BoardModelTests` 固定。
- 能量不足或棋盘无空格时点击生成器：不扣能量且不生成物品；由 Task 3 的 `GeneratorModelTests` 固定。
- 对最高级或不同链物品执行合成：不产生非法物品；由 Task 2 的 `BoardModelTests` 固定。
- 存档文件损坏、版本不支持或 JSON 结构不完整：回退到默认状态而不阻止场景；由 Task 4 的 `SaveServiceTests` 固定。
- 离线时间超过上限或系统时间倒跳：能量不超过 30，时间倒跳不增加能量；由 Task 3 的 `EnergyModelTests` 固定。

---

### Task 1: 创建 Unity 工程骨架与测试包

**Files:**
- Create: `UnityMergePrototype/ProjectSettings/ProjectVersion.txt`
- Create: `UnityMergePrototype/Packages/manifest.json`
- Create: `UnityMergePrototype/Assets/Scripts/Core/.gitkeep`
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/.gitkeep`
- Create: `UnityMergePrototype/Assets/Scripts/Persistence/.gitkeep`
- Create: `UnityMergePrototype/Assets/Tests/EditMode/.gitkeep`
- Create: `UnityMergePrototype/Assets/Tests/PlayMode/.gitkeep`
- Create: `UnityMergePrototype/Assets/Editor/.gitkeep`

**Interfaces:**
- Produces a Unity 2022.3.62f3c1 project root that Unity can import without third-party packages.

- [ ] **Step 1: Create the project metadata**

  Write `ProjectVersion.txt` with `m_EditorVersion: 2022.3.62f3c1` and `m_EditorVersionWithRevision: 2022.3.62f3c1` (do not invent a revision hash). Generate the standard 2022.3 package manifest from Unity Hub, then pin `com.unity.test-framework` to `1.1.33`; keep only Unity built-in modules and the test framework dependency.

- [ ] **Step 2: Create the folder skeleton**

  Create the listed `Assets` directories and empty marker files so the responsibilities are visible before any runtime code is added.

- [ ] **Step 3: Verify project import**

  Run:

  ```bash
  /Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -quit \
    -projectPath "/Users/edy/Documents/ChatGPT/二合/UnityMergePrototype" \
    -logFile "/private/tmp/unity-merge-import.log"
  ```

  Expected: exit code `0`, a generated `Library` directory, and no `error CS` or `Failed to import` lines in the log.

### Task 2: 实现物品目录与 7×9 棋盘核心

**Files:**
- Create: `UnityMergePrototype/Assets/Scripts/Core/ItemDefinition.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/ItemCatalog.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/BoardItem.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/BoardActionResult.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/BoardModel.cs`
- Create: `UnityMergePrototype/Assets/Tests/EditMode/BoardModelTests.cs`

**Interfaces:**
- `ItemCatalog.CreatePrototype()` returns two chains (`dessert`, `drink`) with item IDs `1001..1005` and `2001..2005`.
- `BoardModel.Columns == 7`, `BoardModel.Rows == 9`, `BoardModel.Size == 63`.
- `BoardModel.TryPlace(int index, int itemId)`, `BoardModel.TryMove(int from, int to, out BoardActionResult result)`, `BoardModel.TryRemove(int itemId, int count, out int removed)`, `BoardModel.Count(int itemId)`, `BoardModel.CountAll()`, `BoardModel.FindFirstEmpty()` and `BoardModel.GetItemId(int index)` are the public operations.
- `BoardActionKind` is the enum `{ Rejected, Moved, Swapped, Merged }`; `BoardActionResult.Kind` uses it and the result also carries `From`, `To`, `ResultItemId` and `Message`.

- [ ] **Step 1: Write the failing board tests**

  Add tests with these exact behaviors:

  ```csharp
  private static BoardModel NewBoard() => new BoardModel(ItemCatalog.CreatePrototype());

  [Test] public void Board_has_63_empty_slots()
  {
      var board = NewBoard();
      Assert.AreEqual(63, board.Size);
      for (var i = 0; i < board.Size; i++) Assert.AreEqual(0, board.GetItemId(i));
  }

  [Test] public void Move_to_empty_slot_clears_source()
  {
      var board = NewBoard();
      Assert.IsTrue(board.TryPlace(0, 1001));
      Assert.IsTrue(board.TryMove(0, 1, out var result));
      Assert.AreEqual(BoardActionKind.Moved, result.Kind);
      Assert.AreEqual(0, board.GetItemId(0));
      Assert.AreEqual(1001, board.GetItemId(1));
  }

  [Test] public void Different_items_swap()
  {
      var board = NewBoard(); board.TryPlace(0, 1001); board.TryPlace(1, 2001);
      Assert.IsTrue(board.TryMove(0, 1, out var result));
      Assert.AreEqual(BoardActionKind.Swapped, result.Kind);
      Assert.AreEqual(2001, board.GetItemId(0)); Assert.AreEqual(1001, board.GetItemId(1));
  }

  [Test] public void Same_chain_same_level_merges()
  {
      var board = NewBoard(); board.TryPlace(0, 1001); board.TryPlace(1, 1001);
      Assert.IsTrue(board.TryMove(0, 1, out var result));
      Assert.AreEqual(BoardActionKind.Merged, result.Kind);
      Assert.AreEqual(0, board.GetItemId(0)); Assert.AreEqual(1002, board.GetItemId(1));
  }

  [Test] public void Different_chain_does_not_merge()
  {
      var board = NewBoard(); board.TryPlace(0, 1001); board.TryPlace(1, 2001);
      board.TryMove(0, 1, out var result);
      Assert.AreEqual(BoardActionKind.Swapped, result.Kind);
      Assert.AreEqual(2001, board.GetItemId(0)); Assert.AreEqual(1001, board.GetItemId(1));
  }

  [Test] public void Highest_level_cannot_merge()
  {
      var board = NewBoard(); board.TryPlace(0, 1005); board.TryPlace(1, 1005);
      board.TryMove(0, 1, out var result);
      Assert.AreEqual(BoardActionKind.Rejected, result.Kind);
      Assert.AreEqual(1005, board.GetItemId(0)); Assert.AreEqual(1005, board.GetItemId(1));
  }

  [Test] public void Invalid_index_is_rejected_without_mutation()
  {
      var board = NewBoard(); board.TryPlace(0, 1001);
      Assert.IsFalse(board.TryMove(-1, board.Size, out var result));
      Assert.AreEqual(BoardActionKind.Rejected, result.Kind); Assert.AreEqual(1001, board.GetItemId(0));
  }

  [Test] public void Remove_requires_exact_count()
  {
      var board = NewBoard(); board.TryPlace(0, 1001);
      Assert.IsFalse(board.TryRemove(1001, 2, out var removed));
      Assert.AreEqual(0, removed); Assert.AreEqual(1, board.Count(1001));
  }
  ```

- [ ] **Step 2: Run the tests and verify the expected failure**

  Run the EditMode test filter after the project imports:

  ```bash
  /Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -quit \
    -projectPath "/Users/edy/Documents/ChatGPT/二合/UnityMergePrototype" \
    -runTests -testPlatform editmode \
    -testFilter BoardModelTests \
    -testResults "/private/tmp/unity-merge-board-red.xml" \
    -logFile "/private/tmp/unity-merge-board-red.log"
  ```

  Expected: tests compile and fail because the core types do not yet exist; a test setup/compiler error is not an acceptable RED result.

- [ ] **Step 3: Implement the minimal catalog and model**

  Implement `ItemDefinition` with `Id`, `ChainId`, `Level`, `DisplayName`, and `Color`; build the ten definitions in `ItemCatalog.CreatePrototype()`. Implement `BoardModel` with a private `int[63]`, guarded indices, empty-slot movement, swap behavior, and merge behavior that looks up the next level in `ItemCatalog`. Do not put UnityEngine types in these files.

- [ ] **Step 4: Run the board tests and verify GREEN**

  Re-run the same command with `unity-merge-board-green.xml`. Expected: every `BoardModelTests` test passes and the XML reports zero failures/errors.

### Task 3: 实现能量、生成器、订单和顶层状态

**Files:**
- Create: `UnityMergePrototype/Assets/Scripts/Core/EnergyModel.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/GeneratorModel.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/OrderDefinition.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/OrderModel.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Core/GameState.cs`
- Create: `UnityMergePrototype/Assets/Tests/EditMode/EnergyModelTests.cs`
- Create: `UnityMergePrototype/Assets/Tests/EditMode/GeneratorModelTests.cs`
- Create: `UnityMergePrototype/Assets/Tests/EditMode/OrderModelTests.cs`

**Interfaces:**
- `EnergyModel(int current = 30, int max = 30, DateTimeOffset? lastUpdatedUtc = null)`; `TryConsume(int amount, out string error)`; `ApplyOfflineRecovery(DateTimeOffset now)`.
- `GeneratorModel.TryGenerate(BoardModel board, EnergyModel energy, ItemCatalog catalog, System.Random random, out int slot, out int itemId, out string error)`.
- `OrderDefinition` contains `ItemId`, `RequiredCount`, `RewardCoins`; `OrderModel.CreateInitial(ItemCatalog, System.Random)` creates exactly three orders; `TrySubmit(int orderIndex, BoardModel board, out int reward, out string error)` removes required items and replaces the submitted order.
- `GameState` owns `BoardModel Board`, `EnergyModel Energy`, `int Coins`, `List<OrderDefinition> Orders`, and `int RandomSeed`.

- [ ] **Step 1: Write failing energy, generator, and order tests**

  Add tests for initial 30/30 energy, one-point consumption, rejection at zero, cap at 30 after offline recovery, no recovery when the clock moves backwards, successful generation into the first empty slot, rejection on a full board, rejection without energy, exact order removal/reward, and unchanged state when an order is short. The critical boundary tests must be concrete, for example:

  ```csharp
  [Test] public void Full_board_generation_does_not_consume_energy()
  {
      var catalog = ItemCatalog.CreatePrototype(); var board = new BoardModel(catalog);
      for (var i = 0; i < board.Size; i++) board.TryPlace(i, 1001);
      var energy = new EnergyModel(1, 30, DateTimeOffset.UtcNow);
      var ok = new GeneratorModel().TryGenerate(board, energy, catalog, new System.Random(7), out _, out _, out var error);
      Assert.IsFalse(ok); Assert.AreEqual(1, energy.Current); Assert.AreEqual("Board is full", error);
  }

  [Test] public void Offline_recovery_never_exceeds_cap_or_uses_backwards_time()
  {
      var last = DateTimeOffset.UtcNow; var energy = new EnergyModel(0, 30, last);
      energy.ApplyOfflineRecovery(last.AddSeconds(300)); Assert.AreEqual(10, energy.Current);
      energy.ApplyOfflineRecovery(last.AddSeconds(-1)); Assert.AreEqual(10, energy.Current);
      energy.ApplyOfflineRecovery(last.AddSeconds(3600)); Assert.AreEqual(30, energy.Current);
  }
  ```

- [ ] **Step 2: Run the focused tests and verify RED**

  Run `-testFilter "EnergyModelTests|GeneratorModelTests|OrderModelTests"` and confirm failures are caused by missing behavior/types rather than test syntax.

- [ ] **Step 3: Implement the minimum services**

  Implement elapsed-time recovery using UTC timestamps and integer floor division by 30 seconds. Use the supplied `System.Random` only, so tests can reproduce both item-chain selection and replacement orders. Keep generator and order code dependent on the `BoardModel` interface, not on UI objects.

- [ ] **Step 4: Run focused tests and verify GREEN**

  Re-run the same filter and confirm zero failures/errors. Then run all EditMode tests to ensure Task 2 behavior remains green.

### Task 4: 实现 JSON 存档和状态恢复

**Files:**
- Create: `UnityMergePrototype/Assets/Scripts/Persistence/SaveData.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Persistence/SaveService.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Persistence/FileSaveStorage.cs`
- Create: `UnityMergePrototype/Assets/Tests/EditMode/SaveServiceTests.cs`

**Interfaces:**
- `ISaveStorage.Read()` returns `string`; `ISaveStorage.Write(string json)` persists text; `ISaveStorage.Delete()` removes it.
- `SaveService.Save(GameState state)` serializes schema version, slots, energy, coins, orders, random seed, and last energy UTC ticks.
- `SaveService.LoadOrCreate()` returns a valid `GameState`; malformed, unsupported, or incomplete JSON returns a fresh state and records a warning through an injectable `Action<string>`.
- `FileSaveStorage` maps to `Application.persistentDataPath/merge-prototype-save.json`; tests use an in-memory storage implementation.

- [ ] **Step 1: Write failing round-trip and recovery tests**

  Cover a full state round-trip, malformed JSON fallback, unsupported schema fallback, missing slot-array fallback, delete/reset, and an injected storage write failure that leaves the in-memory state usable. The malformed-input contract is pinned by this test shape:

  ```csharp
  [Test] public void Malformed_json_returns_fresh_state_and_warning()
  {
      var warnings = new List<string>();
      var service = new SaveService(new MemorySaveStorage("{not-json"), warnings.Add, ItemCatalog.CreatePrototype());
      var state = service.LoadOrCreate();
      Assert.AreEqual(30, state.Energy.Current); Assert.AreEqual(0, state.Coins);
      StringAssert.Contains("save", warnings[0].ToLowerInvariant());
  }
  ```

- [ ] **Step 2: Run `SaveServiceTests` and verify RED**

  Run the EditMode filter `SaveServiceTests`; expected failure is absent persistence behavior, not an exception from the test harness.

- [ ] **Step 3: Implement DTOs and the storage boundary**

  Use serializable DTO classes with arrays/lists supported by `JsonUtility`; validate schema version and exactly 63 slots before constructing `GameState`. Keep file I/O behind `ISaveStorage` so no test writes to the user profile.

- [ ] **Step 4: Run all EditMode tests and verify GREEN**

  Run the full EditMode suite and confirm zero failures/errors, including the Review Focus corruption and clock-boundary tests.

### Task 5: 搭建运行时 uGUI 与场景生成器

**Files:**
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/GameBootstrap.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/GamePresenter.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/StatusView.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/BoardGridView.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/BoardItemView.cs`
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/OrderView.cs`
- Create: `UnityMergePrototype/Assets/Editor/PrototypeSceneBuilder.cs`
- Create: `UnityMergePrototype/Assets/Scenes/Main.unity`

**Interfaces:**
- `GameBootstrap.Awake()` creates/loads `GameState`, creates the Canvas hierarchy, and injects it into `GamePresenter`; it exposes read-only `State`, `BoardGrid`, `GeneratorButton`, and `IReadOnlyList<OrderView> Orders` for smoke tests.
- `GamePresenter.Refresh()` renders the complete state; `Generate()`, `Move(int from, int to)`, and `SubmitOrder(int orderIndex)` call core services, refresh, persist, and display a status message.
- `BoardGridView.Build(int size)` creates exactly 63 `BoardItemView`s and sets `CellCount`; `BoardGridView.Render(BoardModel board, ItemCatalog catalog)` updates all cells.
- `BoardItemView` exposes `Index`, `SetItem(int itemId, ItemDefinition definition)`, and a drag-end callback `(from, to)`.
- `StatusView.Show(string message)` and `OrderView.Render(OrderDefinition order, int currentCount, bool canSubmit)` are presentation-only methods.

- [ ] **Step 1: Create the failing PlayMode smoke test**

  Add `Assets/Tests/PlayMode/MainSceneSmokeTests.cs` that loads `Assets/Scenes/Main.unity`, finds `GameBootstrap`, asserts one Canvas, 63 board cells, one generator button, three order views, and invokes the generator button to assert energy decreases and one board cell becomes non-empty. The public surface consumed by the test is explicit:

  ```csharp
  [UnityTest] public IEnumerator Main_scene_boots_and_generates()
  {
      yield return SceneManager.LoadSceneAsync("Main");
      var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
      Assert.IsNotNull(bootstrap); Assert.AreEqual(63, bootstrap.BoardGrid.CellCount);
      Assert.AreEqual(3, bootstrap.Orders.Count);
      var before = bootstrap.State.Energy.Current;
      bootstrap.GeneratorButton.onClick.Invoke();
      Assert.AreEqual(before - 1, bootstrap.State.Energy.Current);
      Assert.Greater(bootstrap.State.Board.CountAll(), 0);
  }
  ```

- [ ] **Step 2: Run the PlayMode test and verify RED**

  Run Unity with `-testPlatform playmode -testFilter MainSceneSmokeTests`; expected failure is missing scene/components, not a project import error.

- [ ] **Step 3: Implement the runtime UI**

  Build a 1080×1920 Canvas with `CanvasScaler` set to `Scale With Screen Size`, a top status row, a central `GridLayoutGroup` with 7 columns and 9 rows, a distinct generator button above the grid, and three order panels below it. Use built-in `Image`, `Text`, and `Button` components with deterministic colors and labels. `GamePresenter` must be the only class that mutates `GameState` from UI callbacks.

- [ ] **Step 4: Implement the editor scene builder**

  `PrototypeSceneBuilder.CreateMainScene()` must create a scene with one root `GameBootstrap`, save it at `Assets/Scenes/Main.unity`, set it as the first Build Settings scene, and avoid requiring manual Inspector assignments. The runtime bootstrap creates the child UI deterministically.

- [ ] **Step 5: Re-run the PlayMode smoke test and verify GREEN**

  Run the same PlayMode filter; expected result is zero failures/errors and the scene must leave a save file in a test-controlled storage path.

### Task 6: 实现拖拽、交换、合成和订单交互

**Files:**
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/BoardDragHandler.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/BoardItemView.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/GamePresenter.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/BoardGridView.cs`
- Modify: `UnityMergePrototype/Assets/Tests/PlayMode/MainSceneSmokeTests.cs`
- Create: `UnityMergePrototype/Assets/Tests/EditMode/PresentationContractTests.cs`

**Interfaces:**
- `BoardDragHandler` implements `IBeginDragHandler`, `IDragHandler`, and `IEndDragHandler`; it reports the source cell and resolves the target cell through `BoardGridView` without changing the model during drag.
- `GamePresenter.Move(int from, int to)` delegates all outcome decisions to `BoardModel.TryMove` and then calls `Refresh()`.
- `BoardGridView.TryGetIndexAtScreenPosition(Vector2 screenPosition, out int index)` returns false for positions outside the grid.

- [ ] **Step 1: Write failing interaction contract tests**

  Add tests that a drag outside the grid leaves the source item in place, a valid drag to an empty cell moves it, a different item swaps it, and a matching item upgrades it. Add a PlayMode test that clicks a satisfied order and asserts coins increase, the required board item count decreases, and a new order is visible. The input-boundary assertion is:

  ```csharp
  [Test] public void Screen_position_outside_grid_is_not_resolved()
  {
      var view = new GameObject("BoardGridView").AddComponent<BoardGridView>();
      view.Build(63);
      Assert.IsFalse(view.TryGetIndexAtScreenPosition(new Vector2(-100, -100), out _));
      Object.DestroyImmediate(view.gameObject);
  }
  ```

- [ ] **Step 2: Run the new tests and verify RED**

  Run `PresentationContractTests` and the PlayMode order test; expected failures must be missing interaction behavior.

- [ ] **Step 3: Implement pointer interaction**

  Keep the dragged view visual-only until pointer release. Resolve the release position to an index; if resolution fails, restore the source view. For valid release, call `GamePresenter.Move` and let the model return `Moved`, `Swapped`, `Merged`, or `Rejected`.

- [ ] **Step 4: Implement generator and order button bindings**

  Bind the generator button to `GamePresenter.Generate`, order buttons to `SubmitOrder(index)`, and refresh status text after every result. Disable no button solely based on predicted state; failed actions must produce an explanatory status message without mutating state.

- [ ] **Step 5: Run all tests and verify GREEN**

  Run both EditMode and PlayMode suites. Expected: zero failures/errors, no compiler errors, and no unhandled exceptions in the Unity log.

### Task 7: 整合场景、批处理验证与手动 Play 验收

**Files:**
- Modify: `UnityMergePrototype/Assets/Editor/PrototypeSceneBuilder.cs`
- Modify: `UnityMergePrototype/Assets/Scenes/Main.unity`
- Create: `UnityMergePrototype/README.md`

**Interfaces:**
- `PrototypeSceneBuilder.CreateMainScene()` is idempotent and can regenerate the scene without duplicating Build Settings entries.
- `README.md` documents the exact Unity version, scene path, Play steps, reset-save behavior, test commands, and excluded original-game systems.

- [ ] **Step 1: Rebuild the scene in batch mode**

  Run:

  ```bash
  /Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -quit \
    -projectPath "/Users/edy/Documents/ChatGPT/二合/UnityMergePrototype" \
    -executeMethod PrototypeSceneBuilder.CreateMainScene \
    -logFile "/private/tmp/unity-merge-scene-build.log"
  ```

  Expected: exit code `0`, `Assets/Scenes/Main.unity` exists, and the log contains no compiler errors.

- [ ] **Step 2: Run the complete automated verification**

  Run EditMode and PlayMode tests separately with XML results under `/private/tmp/unity-merge-tests/`. Check both exit codes and parse the XML files for zero failures and errors. Also run a compile-only import command and scan the log for `error CS`, `NullReferenceException`, `MissingReferenceException`, and `Failed to load`.

- [ ] **Step 3: Perform manual Unity Editor verification**

  Open `UnityMergePrototype` in Unity 2022.3.62f3c1, open `Assets/Scenes/Main.unity`, click Play, then verify: generator creates an item and reduces energy; dragging to empty moves; dragging a different item swaps; dragging equal items merges; submitting a satisfied order removes items and adds coins; closing/re-entering Play restores the saved state; “重新开始” clears the state. Confirm Console has no errors or unhandled exceptions.

- [ ] **Step 4: Document operation and current scope**

  Write `README.md` with the Unity executable path, project path, scene path, Play instructions, reset-save instructions, batch test commands, and a concise list of intentionally unported Cocos features.

- [ ] **Step 5: Verify the final requirement checklist**

  Re-read the spec and check every completion criterion: Unity opens the project, Main is the first Build Settings scene, Play enters the prototype, the core loop works without original Cocos runtime dependencies, restart restores defaults, persistence restores state, automated tests pass, and the original Cocos directory has no modifications.

## Plan Self-Review

- Spec coverage: Tasks 1–7 cover project creation, all core components, UI/data flow, persistence, error boundaries, automated tests, and Editor Play verification.
- Placeholder scan: no `TBD`, `TODO`, "implement later", or unspecified edge-case steps remain; each task names concrete files, interfaces, commands, and expected outcomes.
- Type consistency: Task 2 defines `BoardActionResult` and all `BoardModel` methods consumed by Tasks 3 and 6; Task 3 defines `GameState`, `EnergyModel`, `GeneratorModel`, and `OrderModel` consumed by Tasks 4 and 5; Task 5 defines presenter/view callbacks consumed by Task 6.
- Review Focus coverage: all five focus cases have named tests in Tasks 2–4, and the PlayMode interaction task covers the corresponding user-facing behavior.
- Scope check: the plan creates one independently testable Unity prototype and leaves the Cocos project untouched; full feature parity remains outside this plan.
