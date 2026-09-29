# 游戏内合成链编辑器实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Cocos Creator 2.4.13 核心合成游戏增加一个能本机保存并即时生效的生成链/合成元素 CRUD 编辑器。

**Architecture:** `MergeChainRepository` 保存版本化覆盖数据并提供校验后的 CRUD；`FoodMgr` 将其与启动配置组合成统一运行时目录，模型和玩法消费者继续通过现有查询接口取数据。编辑器从设置面板打开，所有写入先校验关系并以一次存档更新提交；数据重置同时清除此存档并保留声音偏好。

**Tech Stack:** Cocos Creator 2.4.13、TypeScript、现有 `BaseView`/`BaseController`、`FoodMgr`/`FoodItemModel`/`FoodTypeModel`、`engine.localStorage`。

**Spec:** `docs/superpowers/specs/2026-09-23-runtime-merge-chain-editor-design.md`

## Global Constraints

- 游戏引擎固定为 Cocos Creator 2.4.13。
- `assets/resources/data/game_info.json` 及随包资源只读；自定义修改保存到独立本机存档。
- 不引入网络、依赖包、图片导入或配置导入/导出流程。
- 所有链/元素引用使用稳定数字 ID，不以显示顺序作为身份。
- 保持 `FoodMgr` 的现有查询接口可供棋盘、生成器、合成、图鉴与选择界面使用。
- 静态元素使用 30000–30999 的食物编号空间；无可用 ID 时拒绝创建并提示。
- 删除有棋盘、初始生成物、下一阶或产出引用的数据时拒绝删除并说明引用。
- 不恢复能量和市场字段；链/元素更改需要通过一个目录变更事件刷新当前运行时界面。
- “重置数据”清除编辑存档和普通游戏进度，但保留 `soundVolume`、`musicVolume`、`soundFlag`、`musicFlag`。

## Review Focus

- 自定义 ID 被所有静态行占满时，新增元素应清楚失败且不得部分保存；在目录分配器任务中处理。
- 损坏或未知版本的编辑存档不得覆盖原始存档或污染静态目录；在仓库加载任务中处理。
- 下一阶循环、跨链引用和不同 `FoodOutType` 的输出格式必须被拒绝；在校验任务中处理。
- 删除棋盘上已有元素或被生成关系引用的元素必须显示引用来源并保持目录不变；在引用检查任务中处理。
- 修改正在棋盘上的元素后模型视图应刷新，已保存棋盘项目不会变成空模型；在运行时接入任务中处理。

---

### Task 1: 建立可编辑目录存档与 CRUD 仓库

**Files:**
- Create: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/model/MergeChainRepository.ts`
- Create: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Script/logic/game/model/MergeChainTypes.ts`
- Read: `assets/Script/cfw/tools/StorageHelper.ts`, `assets/Script/config/Config.ts`

**Interfaces:**
- `EditableChainRecord`: `id`, `name`, `unlockLevel`, `weight`, `state`, `initialFoodID`。
- `EditableElementRecord`: `id`, `name`, `rare`, `chainID`, `state`, `iconFrame`, `nextID`, `scale`, `price[]`, `outType`, `output[]`, `outCount`, `cooldownSeconds`, `goldOpenPrice`, `splitChance`, `level`, `sellPrice`。
- `MergeChainRepository.load(baseChains, baseElements): void` — 载入静态基础行，解析版本化本机 overlay；遇到损坏/未知版本时保留原始存档、回退基础目录并返回可展示的恢复错误。
- `listChains()`, `getChain(id)`, `listElements(chainID?)`, `getElement(id)` — 返回当前有效目录的数据副本。
- `createChain(input)`, `updateChain(id, patch)`, `deleteChain(id)`、`createElement(chainID, input)`, `updateElement(id, patch)`, `deleteElement(id)` — 返回 `{ ok, value?, issues[] }`，失败时目录和存档都不变。
- `serializeOverlay()` / `replaceOverlay(nextOverlay)` — 只写覆盖、新增和删除集合；先完整验证，再通过一个 `StorageHelper.set` 写入。

- [ ] **Step 1: 定义类型和本地存档格式**

建立 `MergeChainTypes.ts`，明确版本号为 `1`、存档键为 `MergeChainRepository.v1`，并定义基础字段、overlay 集合和验证问题结构。产出数据使用与 `FoodOutType` 匹配的数值数组结构，避免保存 UI 节点对象。

- [ ] **Step 2: 实现基础目录合并和查询副本**

仓库以静态目录为基础，按顺序应用 element/chain 覆盖、新增和 tombstone。每个列表/记录查询返回浅拷贝，避免 UI 在未提交时直接改动当前运行目录。

- [ ] **Step 3: 实现稳定 ID 分配和 CRUD 事务**

链 ID 从当前最大非负 ID 后递增；元素 ID 从 30000–30999 中找最小未使用 ID。所有 CRUD 都构造候选 overlay，在候选目录校验成功后一次替换存档；任何失败都不改变当前目录。

- [ ] **Step 4: 实现删除引用检测**

给仓库提供 `setBoardReferenceProvider(provider)`；provider 返回棋盘元素引用。检查初始生成物、下一阶、产出目标、消耗目标以及棋盘中的元素实例，并把每个引用源写入 `issues[]`。

- [ ] **Step 5: 检查类型完整性和引擎 API**

人工对照 `StorageHelper` 的 `get/set/remove` 签名和 Cocos TypeScript 版本核对导入、序列化类型及 class decorators；不修改静态配置资产。

### Task 2: 让现有玩法读取合并后的目录

**Files:**
- Modify: `assets/Script/logic/game/model/FoodMgr.ts`
- Modify: `assets/Script/logic/game/model/FoodItemModel.ts`
- Modify: `assets/Script/logic/game/model/FoodTypeModel.ts`
- Modify: `assets/Script/config/Config.ts`
- Read/modify consumers only where direct static-manager use prevents the merged directory: `GridFoodMgr.ts`, `GameView.ts`, `BuyMgr.ts`, `FoodInfoView.ts`, `SelectFoodView.ts`, `CoreActionView.ts`

**Interfaces:**
- `FoodMgr.getFoodItemModel(id)`, `getFoodItemModelList()`, `getFoodTypeModel(id)`, `getFoodTypeModelList()`, `getFoodIndexData(chainID)` — static及自定义行的统一模型查询。
- `FoodMgr.getCatalogRevision(): number` — 目录变更后的递增版本。
- `EventName.MERGE_CHAIN_DATA_CHANGED` — 目录保存成功后发出的底层事件，附新版本号。
- `FoodItemModel` and `FoodTypeModel` effective getters — static defaults are overridden by repository fields; dynamic rows resolve without calling static `XlsxData` for a missing ID.

- [ ] **Step 1: Connect FoodMgr to the repository**

在 `FoodMgr.initData()` 建立默认 models 后，把可编辑字段转换成仓库基础记录并调用 `load()`；随后创建自定义 model registry。`getFoodItemModelList()`/`getFoodTypeModelList()` 合并默认及自定义 model 并排除 tombstone，`getFoodIndexData(chainID)` 根据有效模型实时过滤 `ftype`，避免继续使用静态 `ModelManager.indexData`。

- [ ] **Step 2: Make model getters resolve effective fields**

让 `FoodItemModel.getValue/getCfgValue` 和 `FoodTypeModel.getValue/getCfgValue` 对已编辑/新增行先从仓库读取字段，再回退到静态 `DataModel`。创建自定义 model 时将 ID 和完整行数据初始化，并且保证 `getName()` 对自定义字符串直接显示、默认语言键继续通过现有语言表处理。

- [ ] **Step 3: Keep runtime collections coherent after commits**

`FoodMgr` 订阅/接收 repository 提交回调：重建自定义行 map 和链索引、更新 revision、发出 `MERGE_CHAIN_DATA_CHANGED`。重建过程以候选目录一次替换，不暴露一半更新状态。

- [ ] **Step 4: Audit direct config consumers**

逐个检查上列消费者是否只通过 `FoodMgr` API 查询。替换任何绕过 API、直接从静态 `ModelManager` 查询的路径；检查 generator seed、`GridFoodMgr.getNextModel()`、board output、codex 和食物选择面板均能定位自定义行。

- [ ] **Step 5: Preserve saved board data during catalog edits**

确保已有 `GridFoodItemModel` 引用某个仍存在 ID 时，后续 getter 解析到有效模型。仓库禁止删除仍被引用的 ID；变更事件只刷新名称/图标/详情和新产出关系，不自动清空棋盘或计时数据。

### Task 3: 建立链/元素编辑器界面和设置入口

**Files:**
- Create: `assets/Script/logic/game/MergeChainEditorC.ts`
- Create: `assets/Script/logic/game/view/MergeChainEditorView.ts`
- Create: `assets/Script/logic/game/view/MergeChainEditorRowView.ts` (only if row behavior cannot stay in the main view)
- Modify: `assets/Script/logic/sound/view/SettingView.ts`
- Modify: `assets/public/prefabs/SettingView.prefab` and associated `.meta` only if required for the new action binding
- Read: `assets/Script/cfw/tools/ViewManager.ts`, `assets/Script/config/UIConfig.ts`, existing public UI prefab patterns

**Interfaces:**
- `MergeChainEditorC.open(): void` opens the editor in the existing stack layer using the loaded public bundle.
- `MergeChainEditorView.openChain(id)`, `refreshChains()`, `refreshElements(chainID)`, `showIssues(issues)` drive the selected chain and user feedback.
- Editor form callbacks invoke the repository methods defined in Task 1; the view does not edit static models directly.

- [ ] **Step 1: Add the settings panel action**

Add a “合成链编辑” action to `SettingView`. Use `ViewManager.pushUIView` with the existing public module and stack conventions. Closing the editor returns to settings; closing settings returns to the current board.

- [ ] **Step 2: Build chain list and detail screen**

Create a scrollable chain list with search, create, rename/edit, and delete actions. Selecting a chain displays its metadata and all its elements ordered by `level`, then ID. Empty search results and chains with zero elements have explicit empty-state text.

- [ ] **Step 3: Build element CRUD form**

Implement fields from `EditableElementRecord`; use numeric inputs for bounded values, chain/element pickers for references, and a sprite-frame picker populated from existing game atlas frame names. New element creation defaults to selected chain, zero output, no next ID, and a valid existing icon selected by the user.

- [ ] **Step 4: Build format-specific output editing controls**

Render ID/weight pairs for random output types, ordered ID rows for fixed/choice outputs, ID plus quantity for cost output, and no controls for `NO_OUT`. Add/reorder/remove output rows. Convert rows back to the exact flattened numeric array expected by `GameView` and `FoodMgr`.

- [ ] **Step 5: Connect CRUD actions and confirmation flows**

On submit, call repository CRUD; leave the form open and preserve input on validation failure, showing each issue. On delete, confirm once, then show returned reference issues without mutating state if blocked. On successful save, refresh the selected list and show a concise saved state.

### Task 4: Complete validation, live update and reset integration

**Files:**
- Modify: `MergeChainRepository.ts`
- Modify: `FoodMgr.ts`, `GameView.ts`, `CoreActionView.ts`, `SettingView.ts`
- Inspect: `GridFoodMgr.ts`, `GridItemModel.ts`, `GridFoodItemModel.ts`, `FoodItemView.ts`, `FoodInfoView.ts`

**Interfaces:**
- `MergeChainRepository.validateCatalog(chains, elements): ValidationIssue[]` is the single validation entry for load and commits.
- `MergeChainRepository.resetToDefaults(): void` removes only the editor overlay and rebuilds the runtime directory from bundled configuration.
- `CoreActionView.resetData()` removes editor storage along with the current game save, while restoring the four audio preference values already preserved by this button.

- [ ] **Step 1: Validate IDs, scalar fields and references**

Reject duplicate IDs, blank names, non-finite values, invalid ranges, nonexistent chain IDs, cross-chain `nextID`, broken initial generator IDs, broken output IDs, and `nextID` cycles. Validate element and chain deletion with reference-provider output before writing storage.

- [ ] **Step 2: Validate `FoodOutType` array semantics**

For `OUT_RANDOM_AND_CD` and `OUT_RANDOM_COUNT_DESTROY`, require even length with valid element-ID/weight pairs and positive total weight. For fixed/choice outputs require existing element IDs in order. For `OUT_COST_ITEM`, require an existing item/resource ID and positive quantity. Require empty output for `NO_OUT`.

- [ ] **Step 3: Refresh board dependent UI from the change event**

Subscribe `GameView` and any active food detail/editor view to `MERGE_CHAIN_DATA_CHANGED`; refresh visible element labels/icons and generator summaries. New gameplay actions use updated model getters immediately. Remove listeners on destroy to preserve the internal event-bus lifecycle.

- [ ] **Step 4: Include editor data in the existing reset path**

Update `CoreActionView.resetData()` to clear the new repository key and continue restoring `soundVolume`, `musicVolume`, `soundFlag`, `musicFlag` after `StorageHelper.clear()`. Restart so `FoodMgr` reloads the original bundled directory.

- [ ] **Step 5: Confirm no storage or asset side effects**

Inspect modified asset metadata and prefab references. Confirm the new editor data key is not shared with configuration, audio, or SDK storage. Confirm reset preserves only the declared sound keys and that invalid repository JSON falls back to base config without deletion.

### Task 5: Build and hand off the integrated editor

**Files:**
- Revisit all files from Tasks 1–4.
- Update: `docs/superpowers/sdd/2026-09-22-core-merge-mode/progress.md` if this project ledger is still used for the active implementation.

**Interfaces:**
- Final game entry: settings → merge-chain editor → chain selection → element CRUD → save → updated core board.
- Final persistence key: `MergeChainRepository.v1`.

- [ ] **Step 1: Review requirements against the spec**

Walk the spec acceptance criteria: settings entry, all default records visible, chain/element CRUD, persistent reopen, live generation/merge/codex update, validation and referenced deletion, reset-to-default with audio preservation.

- [ ] **Step 2: Build with Cocos Creator 2.4.13**

Run:

```sh
/Applications/Cocos/Creator/2.4.13/CocosCreator.app/Contents/MacOS/CocosCreator --path '/Users/edy/Documents/ChatGPT/二合/2.x_源码_装扮梦想家园_2.4.13ts' --build 'platform=web-mobile;buildPath=/private/tmp/runtime-merge-chain-editor'
```

Expected: Cocos build exits successfully and writes `/private/tmp/runtime-merge-chain-editor/web-mobile/index.html`.

- [ ] **Step 3: Check final worktree and report limits**

Review changed file names, ensure no game configuration source file was overwritten, and report the build output path. If a browser preview cannot run because local server binding is blocked, state that the compile/build completed and browser interaction was not exercised.

---
