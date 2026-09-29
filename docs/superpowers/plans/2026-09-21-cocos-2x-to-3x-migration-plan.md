# Cocos Creator 2.x to 3.8.8 Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a separate Cocos Creator 3.8.8 project migrated from the 2.4.13 source and run its main scene in Web Preview without modifying the original project.

**Architecture:** The original project is a read-only source of assets and code. Creator 3.8.8's built-in 2.x importer produces the new project; migration fixes are applied in small, categorized batches in the new project, with a Web Preview gate after every batch. Platform-specific packages remain behind adapters until the browser target is stable.

**Tech Stack:** Cocos Creator 3.8.8, TypeScript, macOS, Cocos Creator 3.x Web Preview, shell-based manifests and static checks.

**Spec:** `docs/superpowers/specs/2026-09-21-cocos-2x-to-3x-migration-design.md`

## Global Constraints

- The original `2.x_源码_装扮梦想家园_2.4.13ts/` directory is read-only during migration.
- All migrated project files live under `3.8.8-migrated/`.
- The original `project.json` must retain `version: 2.4.13` and `engine: cocos-creator-js`.
- Use Creator 3.8.8 `File → Import Cocos Creator 2.x project`; do not fake a migration by editing only `project.json`.
- Web Preview is the first required runtime target; mobile and mini-game publishing are later work.
- Old platform SDK calls must not block browser startup; isolate them behind browser-safe adapters or stubs.

## Review Focus

- Importer changes to `.fire`, `.prefab`, and `.meta` serialization must preserve the main scene and asset references; Task 2 checks UUID and asset counts.
- 2.x decorator/module syntax must compile under 3.8.8 TypeScript; Task 4 checks the first clean script compilation.
- Removed or renamed node/component/resource APIs must not fail at startup; Task 5 checks the Web Preview console and main scene load.
- Custom packages and platform SDKs must not execute browser-incompatible native code; Task 6 checks package load and browser stubs.
- A failed migration batch must be recoverable without touching the source; every task records a manifest and uses the isolated target directory.

---

### Task 1: Freeze the source and create the migration workspace

**Files:**
- Read-only source: `2.x_源码_装扮梦想家园_2.4.13ts/project.json`
- Read-only source: `2.x_源码_装扮梦想家园_2.4.13ts/assets/Scene/Main.fire`
- Create: `3.8.8-migrated/` (workspace directory)
- Create: `3.8.8-migrated/source-manifest.txt`
- Create: `3.8.8-migrated/migration-notes.md`

**Interfaces:**
- Consumes: the original project path and its start-scene UUID `007a43b6-f8dc-45d5-a373-012937b2de87`.
- Produces: an isolated target path and a baseline manifest used by every later task.

- [ ] **Step 1: Record the source baseline**

```bash
src='/Users/edy/Documents/ChatGPT/二合/2.x_源码_装扮梦想家园_2.4.13ts'
{
  printf 'source=%s\n' "$src"
  shasum "$src/project.json"
  printf 'ts='; rg --files "$src/assets" -g '*.ts' | wc -l
  printf 'fire='; rg --files "$src/assets" -g '*.fire' | wc -l
  printf 'prefab='; rg --files "$src/assets" -g '*.prefab' | wc -l
  rg -n '007a43b6-f8dc-45d5-a373-012937b2de87|Main.fire' "$src/settings" "$src/project.json" "$src/assets" -g '*.json' -g '*.fire' | head -n 20
} > '/Users/edy/Documents/ChatGPT/二合/3.8.8-migrated/source-manifest.txt'
```

- [ ] **Step 2: Copy only source-controlled project inputs**

Copy `assets/`, `packages/`, `config/`, `data/`, `move/`, `sarsgamexlsxparser/`, `creator.d.ts`, `jsconfig.json`, `tsconfig.json`, and `project.json` into `3.8.8-migrated/source-input/`; exclude `library/`, `temp/`, and `local/` from the migration input.

- [ ] **Step 3: Verify isolation**

```bash
test -f '/Users/edy/Documents/ChatGPT/二合/3.8.8-migrated/source-input/project.json'
test "$(sed -n 's/.*\"version\": \"\([^\"]*\)\".*/\1/p' '/Users/edy/Documents/ChatGPT/二合/2.x_源码_装扮梦想家园_2.4.13ts/project.json')" = '2.4.13'
```

Expected: both checks succeed; no file under the source path has a newer modification time caused by this task.

### Task 2: Import the 2.x project with Cocos Creator 3.8.8

**Files:**
- Read: `3.8.8-migrated/source-input/`
- Create/modify through Creator 3.8.8: `3.8.8-migrated/project.json`, `3.8.8-migrated/assets/`, `3.8.8-migrated/settings/`, `3.8.8-migrated/packages/`
- Create: `3.8.8-migrated/import-log.txt`

**Interfaces:**
- Consumes: Task 1's `source-input/` and start-scene UUID.
- Produces: a native 3.x project whose project metadata is not `cocos-creator-js` 2.4 metadata.

- [ ] **Step 1: Open Creator 3.8.8 in a graphical desktop session**

Open `/Applications/Cocos/Creator/3.8.8/CocosCreator.app`, choose `File → Import Cocos Creator 2.x project`, select `3.8.8-migrated/source-input/`, and choose `3.8.8-migrated/` as the destination. Accept conversion only for this isolated destination.

- [ ] **Step 2: Wait for asset database completion**

Keep the editor open until the Asset Database/import progress is complete. Save the editor console output to `3.8.8-migrated/import-log.txt` and record any unsupported asset types by path.

- [ ] **Step 3: Verify the imported project identity**

```bash
test -f '/Users/edy/Documents/ChatGPT/二合/3.8.8-migrated/project.json'
rg -n '2\.4\.13|cocos-creator-js' '/Users/edy/Documents/ChatGPT/二合/3.8.8-migrated/project.json' && exit 1 || true
test -d '/Users/edy/Documents/ChatGPT/二合/3.8.8-migrated/assets'
```

Expected: the destination has 3.x project metadata and an `assets/` tree; the source `project.json` hash remains equal to the Task 1 manifest.

### Task 3: Establish the imported scene and asset baseline

**Files:**
- Modify only if required by the importer: `3.8.8-migrated/settings/`
- Create: `3.8.8-migrated/import-baseline.txt`
- Read: imported scene and prefab files under `3.8.8-migrated/assets/`

**Interfaces:**
- Consumes: Task 2's imported project.
- Produces: a documented count of imported scenes/prefabs/scripts and a resolvable main-scene target.

- [ ] **Step 1: Count imported content**

```bash
dst='/Users/edy/Documents/ChatGPT/二合/3.8.8-migrated'
{
  printf 'scenes='; rg --files "$dst/assets" -g '*.scene' -g '*.fire' | wc -l
  printf 'prefabs='; rg --files "$dst/assets" -g '*.prefab' | wc -l
  printf 'scripts='; rg --files "$dst/assets" -g '*.ts' | wc -l
  rg -n '007a43b6-f8dc-45d5-a373-012937b2de87|Main' "$dst/assets" -g '*.scene' -g '*.fire' -g '*.json' | head -n 30
} > "$dst/import-baseline.txt"
```

- [ ] **Step 2: Open the imported main scene**

Use the Creator 3.8.8 Assets panel to locate the imported equivalent of `Main.fire`, open it, and confirm the Scene and Hierarchy panels render without serialization errors.

- [ ] **Step 3: Run an editor-only smoke check**

Press the editor Play/Preview control once. Expected: the scene opens or reports only categorized script/API errors; no missing-project or invalid-serialization dialog appears. Record the first error group in `migration-notes.md` before changing files.

### Task 4: Migrate script syntax and core engine APIs in batches

**Files:**
- Modify only under: `3.8.8-migrated/assets/**/*.ts`
- Modify only under: `3.8.8-migrated/creator.d.ts` or generated 3.x type declarations when the importer creates them
- Create: `3.8.8-migrated/script-migration-log.md`

**Interfaces:**
- Consumes: Task 3's first compiler/console error group.
- Produces: batches that compile in Creator 3.8.8 without changing platform adapters in the same batch.

- [ ] **Step 1: Fix module and decorator errors**

Run the Creator 3.8.8 TypeScript compiler and group errors by file and diagnostic code. Update one category at a time: `cc.Class`/property declarations, `@ccclass`/`@property` declarations, imports from `cc`, and component registration. Record each changed file and diagnostic in `script-migration-log.md`.

- [ ] **Step 2: Fix node/component and event API errors**

Convert only diagnostics that identify removed 2.x APIs, preserving behavior: node/component lookup, event registration/removal, touch/mouse input, and UI transform access. After each category, reopen `Main` and run the editor smoke check.

- [ ] **Step 3: Fix resource and asset API errors**

Convert `cc.loader`/2.x resource calls to the imported 3.x asset pipeline, preserving bundle paths and resource types. Verify every changed load path against an actual asset in the imported `assets/` tree.

- [ ] **Step 4: Verify the script gate**

Expected: Creator 3.8.8 reports no blocking TypeScript compile errors for the main-scene dependency graph. Any remaining errors must be outside that graph and listed with file, diagnostic, and reason in `script-migration-log.md`.

### Task 5: Isolate custom packages and platform SDKs

**Files:**
- Modify only under: `3.8.8-migrated/packages/`
- Modify only under: `3.8.8-migrated/assets/**/platform*`, `**/sdk*`, or the exact files named by compiler errors
- Create: `3.8.8-migrated/platform-compat.md`

**Interfaces:**
- Consumes: Task 4's clean main-scene script dependency graph.
- Produces: browser-safe adapters for login, advertising, payment, and platform-specific calls.

- [ ] **Step 1: Inventory package entry points**

List each package under `3.8.8-migrated/packages/`, its declared extension entry point, and whether it is required by `Main`. Write this inventory to `platform-compat.md`.

- [ ] **Step 2: Gate native-only calls**

For each package or script used by `Main`, guard native-only calls behind the 3.x platform/runtime check already used by the migrated project. Provide a deterministic browser stub that resolves without network access for login, ad, payment, and device APIs.

- [ ] **Step 3: Verify package loading**

Restart Creator 3.8.8 and reopen the project. Expected: package loading completes without a package initialization exception; browser stubs log their use without attempting native SDK loading.

### Task 6: Run the Web Preview acceptance gate

**Files:**
- Modify only if required by preview diagnostics: `3.8.8-migrated/settings/`, main-scene dependency files, and `migration-notes.md`
- Create: `3.8.8-migrated/preview-verification.md`

**Interfaces:**
- Consumes: Tasks 3–5's imported scene and compiled dependency graph.
- Produces: a reproducible Web Preview URL and a recorded startup result.

- [ ] **Step 1: Set the imported main scene as the 3.x start scene**

Use Creator 3.8.8 Project Settings/Build Settings to select the imported `Main` scene. Do not edit the old `.fire` file by hand to force a start-scene UUID.

- [ ] **Step 2: Start Web Preview**

Press Preview in Creator 3.8.8 and record the generated URL, port, and time in `preview-verification.md`.

- [ ] **Step 3: Verify runtime behavior**

In the preview browser, confirm the page loads, the main scene renders, the first interactive UI appears, and the console has no blocking script/serialization/resource errors. Capture the first-screen screenshot and list any non-blocking platform-stub warnings.

- [ ] **Step 4: Verify source preservation**

```bash
src='/Users/edy/Documents/ChatGPT/二合/2.x_源码_装扮梦想家园_2.4.13ts'
test "$(sed -n 's/.*\"version\": \"\([^\"]*\)\".*/\1/p' "$src/project.json")" = '2.4.13'
cmp -s "$src/project.json" '/Users/edy/Documents/ChatGPT/二合/3.8.8-migrated/source-input/project.json'
```

Expected: both checks succeed, proving the migration did not rewrite the original source metadata.

### Task 7: Record residual work and hand off the migrated project

**Files:**
- Modify: `3.8.8-migrated/migration-notes.md`
- Modify: `3.8.8-migrated/platform-compat.md`
- Modify: `3.8.8-migrated/preview-verification.md`

**Interfaces:**
- Consumes: all previous task logs and verification outputs.
- Produces: a clear status report separating Web Preview-complete work from later mobile/mini-game work.

- [ ] **Step 1: Classify remaining diagnostics**

For every remaining warning or error, record file/path, category (`editor`, `runtime`, `platform`, or `non-blocking`), reproduction step, and whether it blocks the Web Preview acceptance gate.

- [ ] **Step 2: Write the handoff commands**

Record the exact Creator 3.8.8 project path and the preview start procedure in `migration-notes.md`; include the original 2.4.13 path as the immutable baseline.

- [ ] **Step 3: Final verification**

Reopen the project from a clean Creator 3.8.8 process, run Web Preview once more, and compare the result with `preview-verification.md`. Do not claim completion unless the main scene is visibly running and all required checks pass.

