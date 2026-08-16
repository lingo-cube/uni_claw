# Perception Actionable Toggle Evidence - Reality Repair Graduation

## Historical Context

1. **Original Graduation**: `PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED`
   - Change: `perception-actionable-toggle-evidence`
   - Initial maturity claimed: toggle type + row association + switch state via ImageSwitchStateProvider

2. **First Falsification Attempt**: Developer Options page
   - Premise: visible toggles existed but were undetected
   - Correction: Developer Options actually rendered NO toggles (all right-side pixels uniform background)
   - Disposition: INVALID_REALITY_SCENARIO

3. **Requalified Valid Buyer**: Wi-Fi Settings (`android.settings.WIFI_SETTINGS`)
   - Two visibly rendered toggle switches with blue track pixels (RGB 73,93,146)
   - Real YOLO output: text_block (9), icon (2), menu_item (4), input (1)
   - Current fusion rejected far-right toggles due to global distance threshold (0.5)

## Actual Live Defect

- **Root Cause**: GLOBAL_DISTANCE_ASSOCIATION_ASSUMPTION
- **Old Association**: Rejected controls where horizontal distance from row text to control > 0.5
- **Real Layout**: Text rows end at x≈0.06, toggles sit at x≈0.94, distance ≈ 0.87
- **Result**: Legitimate far-right Android Settings toggles were rejected

## Repair

- **Corrected Mechanism**: Generic structural association
  1. Vertical overlap with text row
  2. Right-side placement (candidate x >= 0.55)
  3. Toggle-like aspect ratio (1.0-5.0)
  4. Text rows excluded from serving as their own control candidates
- **Not**: A blind threshold patch (0.5 -> 0.9)
- **Real Result**: Wi-Fi Settings page produces 2 correct toggle candidates, 0 false positives

## Verification

- **Real Wi-Fi Asset**: `/tmp/requalify_wifi.png` (2 visible toggles)
- **Developer Options Hard Negative**: 34 text_block candidates, 0 fabricated toggles
- **Python Tests**: 42/42 PASS
- **Targeted C# Tests**: 37/37 PASS
- **Full Regression**: 1052/1056 PASS (4 pre-existing infrastructure failures)
- **Build**: 0 errors
- **Consistency**: ALL PASS
- **OpenSpec Validation**: PASS

## Authority / Contract

- **Python**: toggle TYPE + BOUNDS (candidate discovery)
- **C# ImageSwitchStateProvider**: ON/OFF/UNKNOWN (state extraction)
- **ObservedElement**: unchanged
- **Adapter contract**: unchanged
- **Binding**: unchanged
- **StateBeliefReducer**: unchanged
- **Agent**: unchanged
- **Traversal**: unchanged
- **YOLO**: NOT trained

## Decision

**GRADUATED**

- **Current Capability Maturity**: PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED
- **Reality Repair Status**: VERIFIED
- **Limitation**: Universal Android control recognition is NOT claimed. The repair proves the capability on a real API35 Settings buyer with visible toggles.
