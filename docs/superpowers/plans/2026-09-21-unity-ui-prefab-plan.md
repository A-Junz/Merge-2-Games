# Unity Merge UI Prefab 化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将当前运行时拼装的 Unity 合成游戏 UI 迁移为可在 Inspector 中直接编辑的 `GameUi`、`OrderCard` 和 `BoardCell` Prefab，同时保持现有玩法、交互和存档行为。

**Architecture:** `GameUi.prefab` 保存所有固定 UI 层级和布局；`OrderCard.prefab`、`BoardCell.prefab` 保存可重复视图。`GameUiView` 暴露序列化引用，`GamePresenter` 只负责实例化 Prefab、注册事件和刷新动态数据。

**Tech Stack:** Unity 2022.3.62f3c1, uGUI, C#, Unity Test Framework, UnityEditor PrefabUtility。

**Spec:** `docs/superpowers/specs/2026-09-21-unity-ui-prefab-design.md`

## Global Constraints

- 参考分辨率必须保持 1080×1920 竖屏。
- 玩法状态只能由 `GameState` 持有，Prefab 不保存订单、棋盘或存档数据。
- 不引入第三方 UI 框架或运行时依赖。
- 运行时不得再用 `UiFactory` 创建可见 UI 层级。
- 订单人物、棋盘合成物和动态文本必须继续由运行时绑定，不得烘焙进静态 UI 图。
- 缺失 Prefab 或关键引用必须明确报错并停止 UI 初始化，不得静默恢复旧的代码拼装布局。

## Review Focus

- Prefab 引用丢失：启动应报出具体引用名称，而不是出现半空白界面；由 Task 5 的引用测试覆盖。
- 重复实例数量：订单始终创建 `OrderModel.SlotCount` 个，棋盘始终创建 `BoardModel.CellCount` 个；由 Task 4 的 EditMode 测试覆盖。
- Prefab 修改传播：调整 Prefab 尺寸/切图后重新运行应直接体现，不应被 C# 布局覆盖；由 Task 6 的 PlayMode/截图测试覆盖。
- 动态刷新覆盖静态节点：刷新金币、等级、订单和物品时只能写数据字段，不得重建节点；由 Task 5 的测试覆盖。
- 交互回归：生成、提交、图鉴、删除、拖拽和存档路径必须保持；由 Task 6 的现有 PlayMode 套件覆盖。

---

### Task 1: Add failing Prefab contract tests

**Files:**
- Create: `UnityMergePrototype/Assets/Tests/EditMode/UiPrefabReferenceTests.cs`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/UiTextureIntegrationTests.cs`

**Interfaces:**
- Consumes: planned `GameUiView`, `OrderCardView`, `BoardCellView` serialized references and Prefab resources.
- Produces: executable assertions that define the Prefab contract before production implementation.

- [ ] **Step 1: Write the failing tests**

Add tests with these behaviors:

```csharp
[Test]
public void LoadsEditableUiPrefabsWithRequiredReferences()
{
    var root = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
    Assert.IsNotNull(root);
    Assert.IsNotNull(root.OrderCardPrefab);
    Assert.IsNotNull(root.BoardCellPrefab);
    Assert.IsNotNull(root.LevelLabel);
    Assert.IsNotNull(root.LevelFill);
    Assert.IsNotNull(root.OrderContent);
    Assert.IsNotNull(root.BoardGrid);
}

[Test]
public void ReusablePrefabsExposeSerializedViewReferences()
{
    var order = Resources.Load<OrderCardView>("UI/Prefabs/OrderCard");
    var cell = Resources.Load<BoardCellView>("UI/Prefabs/BoardCell");
    Assert.IsNotNull(order);
    Assert.IsNotNull(cell);
    Assert.IsNotNull(order.SubmitButton);
    Assert.AreEqual(3, order.ItemIcons.Count);
    Assert.IsNotNull(cell.Background);
    Assert.IsNotNull(cell.IconImage);
}
```

Extend `UiTextureIntegrationTests` so the instantiated main UI asserts sprites on `LevelPanel`, `OrdersHeader`, `BoardFrame`, `GeneratorButton`, `EncyclopediaButton`, and `BoardCell_0`.

- [ ] **Step 2: Run the focused tests and verify the expected RED state**

Run:

```bash
/Users/edy/.unity/bin/unity test /Users/edy/Documents/ChatGPT/二合/UnityMergePrototype --mode EditMode --filter UiPrefabReferenceTests --output /private/tmp/ui-prefab-red.xml --timeout 180 --non-interactive -- -nographics
```

Expected: FAIL because `GameUiView`, the three Prefab assets, and serialized references do not exist yet. If the project is locked by an open editor, record that blocker and use the compiler/static contract checks until the editor is available.

- [ ] **Step 3: Commit the test-only change**

```bash
git add UnityMergePrototype/Assets/Tests/EditMode/UiPrefabReferenceTests.cs UnityMergePrototype/Assets/Tests/EditMode/UiTextureIntegrationTests.cs
git commit -m "test: define editable UI prefab contracts"
```

---

### Task 2: Add the serialized UI reference contracts

**Files:**
- Create: `UnityMergePrototype/Assets/Scripts/Presentation/GameUiView.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/OrderCardView.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/BoardCellView.cs`
- Test: `UnityMergePrototype/Assets/Tests/EditMode/UiPrefabReferenceTests.cs`

**Interfaces:**
- Consumes: Prefab contract tests from Task 1.
- Produces: public read-only accessors on `GameUiView`, serialized view references on repeated Prefab components, and explicit `Bind` methods.

- [ ] **Step 1: Add `GameUiView` with serialized references**

Define a `MonoBehaviour` with serialized fields for:

```csharp
[SerializeField] private OrderCardView orderCardPrefab;
[SerializeField] private BoardCellView boardCellPrefab;
[SerializeField] private RectTransform orderContent;
[SerializeField] private RectTransform boardGrid;
[SerializeField] private Text moneyText, lvText, levelLabel, levelProgressText, statusText;
[SerializeField] private RectTransform levelFill;
[SerializeField] private Button cashOutButton, restartButton, encyclopediaButton, generatorButton;
[SerializeField] private GameObject deleteModal, encyclopediaModal, dragGhost;
```

Expose read-only properties with the same semantic names used by `GamePresenter`. Add `ValidateReferences()` that throws `InvalidOperationException` naming the first missing field.

- [ ] **Step 2: Refactor `OrderCardView` to serialized references**

Replace runtime child creation in `Initialize` with serialized fields for background, avatar, avatar label, three item rows, three item icons, three checkmarks, progress text, reward text, submit button/image/label, and complete banner. Add:

```csharp
public Button SubmitButton => _submitButton;
public IReadOnlyList<Image> ItemIcons => _itemIcons;
public void Bind(GamePresenter presenter, int slot)
```

`Bind` sets presenter and slot, validates the references, and only registers the submit listener. Keep `Render(...)` as the sole data update path.

- [ ] **Step 3: Refactor `BoardCellView` to serialized references**

Replace runtime child creation in `Initialize` with serialized fields for background, icon, emoji label, name label, lucky marker and lucky canvas group. Add read-only `Background` and `IconImage` properties and:

```csharp
public void Bind(GamePresenter presenter, int cellIndex)
```

`Bind` sets presenter/index and validates references. Keep pointer/drag handlers and `Render(...)` behavior unchanged.

- [ ] **Step 4: Run the focused contract tests**

Run the Task 1 command. Expected at this point: the tests compile and still fail only because the Prefab assets have not been generated. No missing-type or missing-property compiler errors should remain.

---

### Task 3: Build deterministic editable Prefab assets

**Files:**
- Create: `UnityMergePrototype/Assets/Editor/UiPrefabBuilder.cs`
- Create: `UnityMergePrototype/Assets/Resources/UI/Prefabs/GameUi.prefab`
- Create: `UnityMergePrototype/Assets/Resources/UI/Prefabs/GameUi.prefab.meta`
- Create: `UnityMergePrototype/Assets/Resources/UI/Prefabs/OrderCard.prefab`
- Create: `UnityMergePrototype/Assets/Resources/UI/Prefabs/OrderCard.prefab.meta`
- Create: `UnityMergePrototype/Assets/Resources/UI/Prefabs/BoardCell.prefab`
- Create: `UnityMergePrototype/Assets/Resources/UI/Prefabs/BoardCell.prefab.meta`
- Test: `UnityMergePrototype/Assets/Tests/EditMode/UiPrefabReferenceTests.cs`

**Interfaces:**
- Consumes: `GameUiView`, serialized repeated-view contracts, UI textures under `Assets/Resources/UI`.
- Produces: ordinary Unity Prefab assets that can be opened and edited in the Project window.

- [ ] **Step 1: Add the Editor builder entry point**

Implement `UiPrefabBuilder.BuildAll()` with menu item `Merge Prototype/Build UI Prefabs`. It must:

1. Create `Assets/Resources/UI/Prefabs`.
2. Create `BoardCell` with the empty-cell sprite, icon, fallback labels, lucky marker and `BoardCellView` serialized fields.
3. Create `OrderCard` with order frame, avatar, three item rows/icons/checkmarks, progress/reward labels, submit button and complete badge, then assign `OrderCardView` fields.
4. Create `GameUi` with Canvas/CanvasScaler/GraphicRaycaster, the current background bands and all top/level/orders/board/bottom/modal/drag-ghost nodes, then assign `GameUiView` fields and child Prefab references.
5. Save using `PrefabUtility.SaveAsPrefabAsset`, reload each asset, call `ValidateReferences`, and log paths.

Set current reference sizes from `UiLayoutSpec`, current colors/fonts from `UiFactory`, and current UI sprites from `Resources/UI`. Keep all editable values on the generated Prefab; do not add a runtime builder component.

- [ ] **Step 2: Generate and inspect Prefab assets**

Run Unity editor method/menu:

```bash
/Users/edy/.unity/bin/unity -batchmode -quit -projectPath /Users/edy/Documents/ChatGPT/二合/UnityMergePrototype -executeMethod MergePrototype.EditorTools.UiPrefabBuilder.BuildAll -logFile /private/tmp/ui-prefab-builder.log
```

Expected: three Prefab assets exist under `Assets/Resources/UI/Prefabs`, with no missing-script or serialization errors. If the already-open editor lock prevents this command, run the menu item in the open editor and preserve the same generated paths.

- [ ] **Step 3: Run the reference tests and verify GREEN**

Run the Task 1 command. Expected: `UiPrefabReferenceTests` and the updated texture integration test pass.

---

### Task 4: Switch repeated views to Prefab instantiation

**Files:**
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/GamePresenter.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/OrderCardView.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/BoardCellView.cs`
- Test: `UnityMergePrototype/Assets/Tests/EditMode/UiPrefabReferenceTests.cs`

**Interfaces:**
- Consumes: `GameUiView.OrderCardPrefab`, `GameUiView.BoardCellPrefab`, `OrderCardView.Bind`, and `BoardCellView.Bind`.
- Produces: deterministic runtime counts and data-bound repeated UI without runtime child construction.

- [ ] **Step 1: Add the failing instance-count assertions**

Extend the EditMode test to instantiate `GameUi.prefab`, initialize a Presenter with a new `GameState`, and assert:

```csharp
Assert.AreEqual(OrderModel.SlotCount, root.OrderContent.GetComponentsInChildren<OrderCardView>(true).Length);
Assert.AreEqual(BoardModel.CellCount, root.BoardGrid.GetComponentsInChildren<BoardCellView>(true).Length);
```

The test should fail before the Presenter migration because no repeated instances are created by the new Prefab path.

- [ ] **Step 2: Implement Prefab instantiation in `GamePresenter`**

Replace `BuildOrderArea` and `BuildBoard` with methods that clear only their dynamic content roots, instantiate the serialized child Prefabs, set names `OrderCard_0...` and `BoardCell_0...`, and call `Bind`. Preserve layout groups, ScrollRect configuration and all existing refresh order.

- [ ] **Step 3: Run the instance-count test**

Run the focused EditMode command. Expected: PASS with exactly the model slot/cell counts.

---

### Task 5: Switch the root UI and startup path to `GameUi.prefab`

**Files:**
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/GameBootstrap.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/GamePresenter.cs`
- Modify: `UnityMergePrototype/Assets/Editor/MainSceneBuilder.cs`
- Modify: `UnityMergePrototype/Assets/Tests/EditMode/UiTextureIntegrationTests.cs`
- Modify: `UnityMergePrototype/Assets/Tests/PlayMode/MainScenePlayModeTests.cs`

**Interfaces:**
- Consumes: `Resources.Load<GameUiView>("UI/Prefabs/GameUi")` and repeated-view Prefab instances.
- Produces: a running game whose visible root is a Prefab instance and whose Presenter only binds data/interaction.

- [ ] **Step 1: Add a failing root-instance assertion**

Add a test that starts the bootstrap and asserts:

```csharp
var ui = Object.FindObjectOfType<GameUiView>();
Assert.IsNotNull(ui);
Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(ui.gameObject));
Assert.IsNotNull(GameBootstrap.Presenter);
```

Keep the assertion Editor-only if `PrefabUtility` is not available in the PlayMode test assembly; the runtime test must still assert the root and known children by component/name.

- [ ] **Step 2: Replace `GamePresenter.BuildUi` with Prefab binding**

Add an overload:

```csharp
public void Initialize(GameState state, SaveService saveService,
    string loadFallbackReason, GameUiView uiView)
```

Validate the view, cache its serialized references, instantiate repeated items, register existing callbacks, and call `RefreshAll`. Remove calls to all `Build*` methods that create visible UI. Keep only data refresh and modal/drag state updates.

- [ ] **Step 3: Update `GameBootstrap`**

Load and instantiate `GameUi.prefab`, obtain its `GameUiView`, add/get `GamePresenter` on the instantiated root, and call the new `Initialize` overload. Preserve `SavePathOverride` and EventSystem fallback.

- [ ] **Step 4: Update the scene builder and tests**

Ensure `MainSceneBuilder` creates only camera, EventSystem and `GameBootstrap`; do not bake a second UI root into the scene. Update integration tests to use the Prefab root and verify the current dynamic data labels are populated after initialization.

- [ ] **Step 5: Run focused EditMode and PlayMode tests**

Run:

```bash
/Users/edy/.unity/bin/unity test /Users/edy/Documents/ChatGPT/二合/UnityMergePrototype --mode EditMode --output /private/tmp/ui-prefab-editmode.xml --timeout 300 --non-interactive -- -nographics
/Users/edy/.unity/bin/unity test /Users/edy/Documents/ChatGPT/二合/UnityMergePrototype --mode PlayMode --output /private/tmp/ui-prefab-playmode.xml --timeout 300 --non-interactive -- -nographics
```

Expected: existing Core/UI/interaction tests remain green, with no duplicate Canvas/EventSystem or missing-reference errors.

---

### Task 6: Remove runtime visual construction and document the editing workflow

**Files:**
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/UiFactory.cs`
- Modify: `UnityMergePrototype/Assets/Scripts/Presentation/GamePresenter.cs`
- Modify: `UnityMergePrototype/Assets/Resources/UI/README.md`
- Modify: `docs/unity-ui-style.md`
- Test: full EditMode and PlayMode suites

**Interfaces:**
- Consumes: Prefab-only UI path from Tasks 3–5.
- Produces: no hidden duplicate UI construction path and clear instructions for designers.

- [ ] **Step 1: Search for forbidden runtime UI creation**

Run:

```bash
rg -n 'CreatePanel|CreateText|CreateButton|CreateImage|BuildTopBar|BuildLevelBar|BuildOrderArea|BuildBoard|BuildBottomBar|BuildDeleteModal|BuildEncyclopediaModal' UnityMergePrototype/Assets/Scripts/Presentation
```

Expected: no calls from runtime Presenter/View classes. Keep only resource-loading helpers if they are still used by dynamic image binding; delete unused construction methods after all references are gone.

- [ ] **Step 2: Add Prefab editing documentation**

Document the three Prefab paths, the serialized reference ownership, how to use `Merge Prototype/Build UI Prefabs`, which fields are safe to edit, and which dynamic values must remain empty/serialized for runtime binding.

- [ ] **Step 3: Run the complete test suites and final static checks**

Run both Unity test commands from Task 5 and:

```bash
git diff --check
rg -n 'CreatePanel|CreateText|CreateButton|CreateImage' UnityMergePrototype/Assets/Scripts/Presentation
find UnityMergePrototype/Assets/Resources/UI/Prefabs -maxdepth 1 -name '*.prefab' -print
```

Expected: both test reports have zero failures; the runtime search returns no visual-construction calls; all three Prefab assets are present. If Unity is locked by another editor, report the exact lock message instead of claiming tests passed.

- [ ] **Step 4: Commit the implementation**

```bash
git add UnityMergePrototype/Assets/Editor/UiPrefabBuilder.cs UnityMergePrototype/Assets/Scripts/Presentation UnityMergePrototype/Assets/Resources/UI/Prefabs UnityMergePrototype/Assets/Tests docs/unity-ui-style.md UnityMergePrototype/Assets/Resources/UI/README.md
git commit -m "refactor: move Unity UI layout into prefabs"
```
