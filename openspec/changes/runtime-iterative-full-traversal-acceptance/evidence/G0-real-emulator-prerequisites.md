# G0 — Real Emulator & Real Settings Prerequisite Evidence (pre-Stage-A)

Collected tool-only (read-only world interaction: intent start + uiautomator dump), 2026-08-24 session.

## Emulator

- AVD: `scroll-test` — `system-images/android-35/default/arm64-v8a` (Android 15, API 35, arm64-v8a), lcd 320x640 @160dpi (rendered 1080x1920 bounds in dumps).
- Boot: headless `-no-window -no-audio -no-boot-anim -no-snapshot -port 5554`; boot completed ~17s; `adb devices` → `emulator-5554 device`.
- Settings package present: `package:com.android.settings`.

## Anchor verification (real Android Settings, API 35)

1. **Root (homepage)**: `am start -a android.settings.SETTINGS` → `topResumedActivity=com.android.settings/.homepage.SettingsHomepageActivity`.
   Dump contains `com.android.settings:id/search_action_bar` (the graduated root identity anchor used by
   `SettingsSingleRecursiveChildTests.ResolveSemanticPage`) plus homepage container ids
   (`homepage_container`, `main_content_scrollable_container`, `recycler_view`, `homepage_title`).
   NOTE: the homepage carries NO `collapsing_toolbar` and NO `Navigate up` — the anchor pair is
   disjoint between root and children, exactly the graduated page-class semantics.
2. **Child page**: `am start -a android.settings.WIRELESS_SETTINGS` → dump contains
   `content-desc="Navigate up"` AND `com.android.settings:id/collapsing_toolbar` (title-role anchor).
   Child rows visible: Internet, SIMs, Airplane mode, Hotspot & tethering, Data Saver, Private DNS …
   (scroll required — exercises the graduated viewport-exhaustion machinery).
3. **Launcher baseline**: `monkey -p com.android.settings 1` alone landed on the launcher
   (`com.android.launcher3`), NOT Settings — evidence that app arrival must go through the
   strategy-scope launch path (`launchIntentAction: android.settings.SETTINGS`, mirroring the
   graduated capstone wiring), not a bare monkey launch. This matches
   `Startup`'s `DeviceAction.LaunchApp(package, launchIntentAction)` →
   `AdbDispatchTarget` launch mapping.

## Conclusion

- The real Settings tree on this image satisfies the graduated page-identity anchor classes
  (search_action_bar root anchor; Navigate-up + collapsing_toolbar child anchors).
- The production `SettingsSemanticCapability` (settings.container / preference-row /
  search-role / navigate-up vocabulary) + the harness-local binding can resolve
  page identity and navigation candidates from real observations.
- Stage G can run on this emulator once the binding lands.

## Probe command log (for provenance)

```
adb -s emulator-5554 shell am start -a android.settings.SETTINGS     # → SettingsHomepageActivity
adb -s emulator-5554 shell uiautomator dump /sdcard/p26probe.xml     # → search_action_bar present
adb -s emulator-5554 shell am start -a android.settings.WIRELESS_SETTINGS  # → Network & internet page
adb -s emulator-5554 shell uiautomator dump /sdcard/p26probe3.xml    # → Navigate up + collapsing_toolbar
```
