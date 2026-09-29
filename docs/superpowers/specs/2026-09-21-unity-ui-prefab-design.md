# Unity Merge UI Prefab 化设计

## 背景

当前 `UnityMergePrototype` 的界面由 `GamePresenter`、`OrderCardView`、`BoardCellView` 通过 `UiFactory.CreatePanel/CreateText/CreateButton` 在运行时逐个拼装。这样可以快速验证玩法，但界面尺寸、间距、层级、字体、切图和按钮外观都隐藏在 C# 中，设计人员无法直接在 Unity Inspector 中调整。

本次改造把所有可见 UI 的结构和视觉属性迁移为可编辑 Prefab；玩法状态、文本/Sprite 数据和交互逻辑仍由现有 C# 代码驱动。

## 目标

- 运行时不再用 `UiFactory` 创建可见 UI 层级。
- 设计人员可以在 Unity 编辑器中打开主 UI Prefab，直接调整 RectTransform、Image、Text、Button、LayoutGroup、字体和切图。
- 保持当前 1080×1920 竖屏布局、英文 UI 文案、食物主题和现有 UI 切图。
- 保持棋盘拖拽/合成、订单提交、生成器、图鉴、删除确认、存档和状态刷新行为不变。
- 订单卡和棋盘格作为独立子 Prefab，可单独调整并重复实例化。
- 保留当前测试可使用的初始化入口，避免玩法域模型依赖 Prefab。

## 非目标

- 不改变 `GameState`、棋盘规则、订单奖励、能量或存档格式。
- 不把动态棋盘物品、订单人物或订单食物烘焙进静态 UI 图片。
- 不迁移原 Cocos 工程的非核心界面、商城、广告或活动系统。
- 不引入第三方 UI 框架或运行时依赖。

## 设计方案

采用三层 Prefab 结构：一个主界面 Prefab，加两个可复用重复项 Prefab。

### 1. `GameUi.prefab`

位置：`Assets/Resources/UI/Prefabs/GameUi.prefab`。

根节点包含 `Canvas`、`CanvasScaler` 和 `GraphicRaycaster`，并挂载 `GameUiView` 作为引用容器。其子层级固定为：

```text
GameUi
├── Background
├── KitchenBackdrop
├── TopStatus
│   ├── MoneyPill / MoneyText
│   ├── CashOutButton
│   └── LevelCapsule / LevelText
├── LevelPanel
│   ├── LevelLabel
│   ├── SmileIcon
│   ├── LevelProgressTrack / LevelFill / LevelProgressText
│   └── RestartButton
├── OrdersSection
│   ├── OrdersHeader
│   └── OrderViewport / OrderContent
├── BoardSection
│   ├── BoardFrame / BoardPanel
│   └── BoardGrid
├── StatusText
├── BottomBar
│   ├── TableCloth
│   ├── EncyclopediaButton
│   └── GeneratorButton
├── DragGhost
├── DeleteModal
└── EncyclopediaModal
```

`GameUiView` 序列化这些固定引用：动态文本、进度条 Image、按钮、订单内容根节点、棋盘网格根节点、弹窗根节点和拖拽幽灵。Prefab 内的布局值成为唯一的视觉布局来源。

### 2. `OrderCard.prefab`

位置：`Assets/Resources/UI/Prefabs/OrderCard.prefab`。

Prefab 根节点挂载 `OrderCardView` 和 `LayoutElement`，内部包含：卡片底图、Avatar 占位、三个需求物行、进度文本、奖励文本、提交按钮和完成徽章。`OrderCardView` 改为通过 `[SerializeField]` 引用这些节点；运行时只更新需求 Sprite、完成状态、进度、奖励和按钮状态。

### 3. `BoardCell.prefab`

位置：`Assets/Resources/UI/Prefabs/BoardCell.prefab`。

Prefab 根节点挂载 `BoardCellView`，内部包含空格背景、物品 Icon、Emoji/名称回退文本和 Lucky 标记。`BoardCellView` 改为通过序列化引用获取这些节点；运行时只更新当前物品和交互状态。63 个棋盘格由 `GamePresenter` 从 `BoardCell.prefab` 实例化到 `BoardGrid`。

## 运行时数据流

```text
GameBootstrap
  └─ Load/Create GameState + SaveService
  └─ Instantiate GameUi.prefab
  └─ GamePresenter.Initialize(state, saveService, fallbackReason, uiView)
       ├─ Instantiate OrderCard.prefab × OrderModel.SlotCount
       ├─ Instantiate BoardCell.prefab × BoardModel.CellCount
       ├─ 注册按钮、滚动区和拖拽回调
       └─ RefreshAll()：只写入文本、Sprite、颜色、显示状态和进度
```

Prefab 缺失时不再静默拼装第二套视觉 UI；启动应给出明确错误并停止初始化，避免设计人员看到与 Prefab 不一致的隐藏回退布局。测试可以通过显式传入 Prefab 引用，运行时则由 `Resources.Load<GameUiView>("UI/Prefabs/GameUi")` 取得默认资源。

## 代码边界

- `GameUiView.cs`：保存主界面 Prefab 的序列化引用，并提供 `OrderContent`、`BoardGrid`、`OrderCardPrefab`、`BoardCellPrefab` 等访问器。
- `GamePresenter.cs`：删除 `BuildTopBar/BuildLevelBar/BuildOrderArea/BuildBoard/BuildBottomBar/Build*Modal` 的运行时层级创建，改为实例化 Prefab、绑定 `GameUiView` 引用并刷新数据。
- `OrderCardView.cs`：删除运行时创建子节点，改为 `Bind(GamePresenter, int)` 和 `Render(...)`。
- `BoardCellView.cs`：删除运行时创建子节点，改为 `Bind(GamePresenter, int)` 和现有 `Render(...)`。
- `GameBootstrap.cs`：实例化 `GameUi.prefab`，将其 `GameUiView` 传给 Presenter；保留 EventSystem 兜底。
- `UiFactory.cs`：不再作为运行时 UI 构建入口；保留必要的静态资源加载辅助，待迁移完成后删除未引用的构建方法。
- `Assets/Editor/UiPrefabBuilder.cs`：在 Editor 中按当前效果图生成/更新三类 Prefab，设置切图、9-slice、尺寸、锚点、字体和序列化引用。

## 交互与持久化约束

- Prefab 只承载视觉层和事件目标，不保存 `GameState`、订单数据或棋盘数据。
- 所有按钮监听仍在 Presenter 中注册，Prefab 不保存运行时 delegate，避免场景/存档序列化旧引用。
- `OrderCardView`、`BoardCellView` 的公共名称和数据渲染行为保持兼容，现有玩法测试不改变断言语义。
- 弹窗默认隐藏状态由 Prefab 保存；打开/关闭只切换 `GameObject.SetActive`。
- 拖拽幽灵由 Prefab 提供 Image、Icon 和 Label，拖拽时只改位置、颜色、Sprite 与文本。

## 测试策略

### EditMode

- `UiPrefabReferenceTests`：从 `Resources` 加载三类 Prefab，确认关键引用不为空、Prefab 包含预期组件和静态 Sprite。
- `UiTextureIntegrationTests`：实例化 `GameUi.prefab`，确认 Level/Orders/Board/Bottom 区域使用切图，OrderCard 和 BoardCell 的子引用有效。
- 现有 Core、OrderCard、UI 布局规则测试继续运行，确认域模型与尺寸常量不受影响。

### PlayMode

- `MainScenePlayModeTests`：启动场景后确认 `GameUi` 由 Prefab 实例化，生成器按钮、订单提交、图鉴、删除和棋盘拖拽仍可交互。
- 截图测试继续验证 1080×1920 竖屏结构，重点检查 Prefab 实例化后没有重复 Canvas、重复 EventSystem 或空引用异常。

## 验收标准

1. Unity Project 窗口可以直接打开并编辑 `GameUi.prefab`、`OrderCard.prefab`、`BoardCell.prefab`。
2. 修改 Prefab 中的按钮尺寸、订单卡间距或棋盘格边框后，重新运行即可看到改动，无需修改 C# 布局代码。
3. 运行时层级包含 Prefab 实例，而不是由 `UiFactory` 新建同名视觉节点。
4. 动态金币、等级、进度、订单、棋盘物品和状态消息仍正确刷新。
5. 现有交互和存档行为不回归，Prefab 引用测试和可执行的 Unity 测试通过。

## 迁移与回滚

迁移采用独立步骤：先新增引用容器和 Prefab 构建器，再切换 Presenter，最后删除未使用的运行时构建路径。若某一步失败，可暂时恢复 `GamePresenter.BuildUi` 的旧入口并保留新 Prefab 资源，不修改 Core/存档数据；所有新增 Prefab 和引用代码均位于 Presentation/Editor/UI 资源范围内。
