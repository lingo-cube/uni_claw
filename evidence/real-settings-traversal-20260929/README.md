# AGT-003 real Settings traversal evidence

- Device: `emulator-5554` (API 35)
- DSH model: `opencode-go/deepseek-v4.1-flash`
- Run: `/private/tmp/uni-settings-traversal-live-20260929-agt003-final1/run-20260928-233940-085`
- DSH session: `session-e61e6777-cbf5-4b01-8e3c-f2f62bd69347` (from authenticated DSH launch log)
- Consultations: 4 (`act`, `act`, `defer`, `act`); no `decision-id-mismatch` or `one-in-flight` after the session/turn barrier fix.
- Effects: 2 completed taps; final device Wi-Fi state is `1` (enabled).
- Runtime status: `TerminalNotProven (evidence-insufficient)`; the final hierarchy shows the Wi-Fi switch checked, but the runtime did not emit a completion Outcome. This is retained as the current E2E blocker.
- A bounded model retry with `deepseekV41` was attempted and failed closed because DSH reported `Select an available model before sending a message`; the valid configured `deepseek-v4.1-flash` run above was retained.
- Output acceptance checks: plugin `18/18`, Agent.Dsh `129/129`, Kernel decision/policy/plan focused set `57/57`. The checks cover `act` plans, `policy`, `noAction`, and `defer`, positive bounds, malformed rejection, exact `DecisionId`, and response correlation by Product Run, request generation, and schema hash.
