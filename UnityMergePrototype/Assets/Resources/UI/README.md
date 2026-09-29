# Unity UI slices (v4)

切图来源：`food-merge-level-ui-v4-closed-recipe-book.png`。

本目录只包含静态 UI：顶部状态条、等级/进度底图、空进度槽、订单卡片外壳、完成徽章、奖励 chip、空棋盘框/空格、图鉴入口和 SERVE ITEMS 按钮。棋盘合成物、订单人物和订单食物没有被导出。

`ui_level_panel_base.png` 是不含 LEVEL 文案、笑脸、绿色进度和 3/7 数字的底图；`ui_level_progress_track.png` 是可叠加动态填充的空槽。

## Unity 导入建议

- Texture Type：`Sprite (2D and UI)`
- Alpha Is Transparency：开启
- Mesh Type：`Full Rect`
- Filter Mode：`Bilinear`
- Compression：UI 资源建议 `None` 或高质量压缩
- `ui_level_panel_base`、`ui_level_progress_track`、`ui_orders_header`、`ui_order_card_frame`、`ui_order_complete_badge`、`ui_board_frame_empty`、`ui_board_cell_empty`、`ui_serve_items_button` 可使用 9-slice；Unity `.meta` 已预设边距。
- `ui_atlas.png` + `ui_atlas.json` 可作为批量导入参考；若使用 Unity Sprite Atlas，可按 JSON 中的 rect 建立切片。

## Prefab UI

主界面已经拆成可编辑的 `GameUi.prefab`、`OrderCard.prefab` 和 `BoardCell.prefab`。
如果从仓库首次打开项目，执行 Unity 菜单 `Merge Prototype > Build UI Prefabs` 生成或刷新这三个资源；生成后可直接在 Inspector 中调整布局、颜色、字体和按钮样式，运行时只会实例化 Prefab 并写入金额、等级、订单、棋盘格和图鉴数据。

Prefab 输出目录：`Assets/Resources/UI/Prefabs/`。
