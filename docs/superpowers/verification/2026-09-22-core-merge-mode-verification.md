# Core merge mode verification

Date: 2026-09-22

## Checks passed

- `python3 tools/verify_core_merge_mode.py` → `CORE MERGE MODE: PASS`.
- JSON parsing passed for `MoneyView.prefab`, `GameBottomView.prefab`, and `GameView.prefab`.
- A delimiter scan passed for every changed TypeScript entry point and retained-system file.
- Cocos Creator 2.4.13 web-mobile build completed successfully:
  `/private/tmp/core-merge-preview-final/web-mobile` contains `index.html`, `main.js`, and generated assets.

## Native note

Cocos Creator 2.4.13 was invoked with `platform=ios` to validate the Native pipeline. The project reached Native project generation, but this host has no Python command/Xcode command-line developer tools, so the iOS generator could not finish. The source compilation and asset build completed in the web-mobile build; install the Cocos-required Python 2/Xcode command-line tools to produce an installable iOS package.
