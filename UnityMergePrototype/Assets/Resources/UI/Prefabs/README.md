# Editable UI Prefabs

Run `Merge Prototype > Build UI Prefabs` in the Unity Editor to create or refresh:

- `GameUi.prefab` — static screen hierarchy and serialized references.
- `OrderCard.prefab` — reusable order card visual.
- `BoardCell.prefab` — reusable merge-board cell visual.

`GameUi.prefab/EncyclopediaModal/Card` 的图鉴弹窗按可编辑层级生成：

- `Header`：图鉴书本、标题和关闭按钮。
- `Summary`：总收集进度槽、进度文字和奖励说明。
- `ChainScroll/Viewport/Content`：10 条普通合成链的可滚动卡片；每条卡片有 `Icon`、`Title`、`Reward` 和 8 个 `Grid/Catalog_*` 元素格。

图鉴刷新时只改变这些节点的文本、颜色、图标和问号状态；只有 `ProgressTrack/Fill` 会按进度更新填充宽度。其他布局锚点与尺寸保留给 Inspector 调整。未出现过的元素会隐藏图标并显示 `?`。

Runtime code instantiates these assets and only binds game data and interactions.
