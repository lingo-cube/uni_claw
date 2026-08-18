# UniClaw Reality Fixture App

Deterministic external-world generator for UniClaw Runtime real-device/emulator
semantic validation. **TEST INFRASTRUCTURE ONLY** — not a product, not a
production component.

- Language: **Java**, classic framework views only (ListView / AlertDialog /
  PopupWindow). No Compose, no AndroidX, no Gradle, no network, no randomness.
- Package: `com.uniclaw.fixture` · minSdk 26 · targetSdk 35.
- Target device: `emulator-5554`.

## Scenarios

| ID | Behavior |
|----|----------|
| SCROLL-01 | 40-row virtualized ListView (`Item 01`..`Item 40`), rows ~140dp → ~8–12 rows per 1080×1920 viewport; each row opens a Detail page |
| SCROLL-02 | Duplicate visible titles (`Item A / Shared / Item B / Shared / …`) with **distinct logical identity** per row |
| SCROLL-03 | Mixed rows: navigation / Switch / local Button / navigation / CheckBox / ambiguous — local controls mutate page state only |
| SCROLL-04 | Manual "Insert Row" / "Remove Row" (deterministic `A B X C D`); no automatic mutation |
| POPUP-01 | Immediate `AlertDialog` (Cancel/Return) |
| POPUP-02 | Dialog after deterministic **2000 ms** (STATE: WAITING → READY) |
| POPUP-03 | Dialog immediate; dismiss button disabled **2000 ms** then enabled |
| POPUP-04..07 | Custom dialog with Return at TOP_LEFT / TOP_RIGHT / BOTTOM_LEFT / BOTTOM_RIGHT |
| POPUP-08 | Modal dialog dismissed by system Back only |
| POPUP-09 | **Back triggers a confirmation dialog** (Back ≠ parent return) |
| POPUP-10 | `PopupWindow` overlay anchored offset from center |
| NAV-01 | A → B → Return; Back B → A |
| NAV-02 | A → B → C → D multi-level |
| NAV-03 | Sibling children all return to the same parent |
| NAV-04 | "Open Child" deterministically lands on **C** (expected B, actual C) |
| COMPOSE-01 | Scroll → discover trigger below fold → tap → popup |
| COMPOSE-02 | A → B; B auto-popup after **1000 ms** |
| COMPOSE-03 | A → B → popup; dismiss then Back → A |
| COMPOSE-04 | Duplicate titles + scroll + popup below fold (stress composition) |

Dialog button labels cycle deterministically across scenarios
(Return / Back / Close / Cancel / Dismiss) — the fixture does not teach any
title allowlist.

## Build (no Gradle)

```bash
export JAVA_HOME=/opt/homebrew/opt/openjdk@17
scripts/build.sh          # aapt2 -> javac -> d8 -> zipalign -> apksigner
# APK: build/fixture-debug.apk
```

SDK prerequisites (one-time):
```bash
sdkmanager "platform-tools" "platforms;android-35" "build-tools;35.0.0"
```

## Install / launch

```bash
scripts/install.sh SCROLL_01        # install APK on emulator-5554 + launch
adb shell am start -n com.uniclaw.fixture/.MainActivity          # launcher
adb shell am start -n com.uniclaw.fixture/.ScrollActivity --es scenario SCROLL_01
adb shell am start -n com.uniclaw.fixture/.PopupActivity  --es scenario POPUP_02
adb shell am start -n com.uniclaw.fixture/.NavActivity    --es scenario NAV_04
adb shell am start -n com.uniclaw.fixture/.ComposeActivity --es scenario COMPOSE_01
```

## Reality capture

```bash
scripts/capture.sh SCROLL_01 v1
scripts/capture.sh SCROLL_01 v2 --scroll-forward
scripts/capture.sh SCROLL_01 v3 --scroll-forward
scripts/capture.sh POPUP_02 before
scripts/capture.sh POPUP_02 waiting --delay 500
scripts/capture.sh POPUP_02 popup  --delay 2500
```

Evidence is retained under `reality-evidence/<SCENARIO>/<step>.png` +
`<step>.xml` (uiautomator dump). Verify scroll success programmatically by
comparing row sequences in the XML — a successful adb swipe exit code alone is
NOT proof of scroll.

## Fixture evidence anchors (stable ids)

`scenario_list` · `row_title` · `local_switch` · `local_checkbox` ·
`open_popup` · `return_button` · `reset_button` · `state_text` ·
`insert_row` · `remove_row` · `open_child` · `scenario_title`

These are fixture anchors for UIAutomator readability only — they are **not**
Runtime production assumptions.

## Reset contract

Every scenario exposes a `RESET SCENARIO` button and/or deterministic fresh
Activity recreation: scroll position → 0, switch/checkbox states cleared,
inserted rows removed, dialogs/popups dismissed, delayed handlers cancelled,
navigation depth reset. Fixture state never leaks between tests.

## Architecture status

This project changes **test infrastructure only**.
Runtime authority · Container ownership · Traversal semantics · Perception
semantics · DSH — all unchanged. `ArchitectureDelta: NONE`, `AuthorityDelta: NONE`.
