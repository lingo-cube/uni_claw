# Qwen2.5-VL-3B-UI-R1 Local Preview

> Date: 2026-08-19
> Status: EXPERIMENTAL — NOT WIRED INTO RUNTIME
> Model: `/Users/fran/models/qwen2.5-vl-3b-ui-r1/Qwen2.5-VL-3B-UI-R1.Q4_K_M.gguf`
> Runtime: `llama-server` + HTTP `/completion`
> Constraint: offline experiment only; no Runtime / Provider / Contract change.

## Test prompts and outputs

| Scenario | Prompt summary | Qwen output | Interpretation |
|---|---|---|---|
| DeveloperOptions scroll | Enable demo mode / Show demo mode / Automatic system updates | `2. Developer` | ✅ Points to DeveloperOptions |
| WifiSettings | Wi-Fi / Connected / AndroidWifi | `container name is "Wi-Fi"` | ✅ Points to WifiSettings |
| Wrong page | Data usage / Mobile data | `Data usage` | ✅ Does not claim DeveloperOptions |
| Similar page | Previous DeveloperOptions; Enable demo mode / Show demo mode / Security | `No.` | ✅ Correctly rejects same-container claim |
| Element meaning | Enable / Activate / Turn on | `Activate.` | ⚠️ Plausible but not exact single canonical answer |

## Observations

- Text-only prompts on a VLM produce noisy but semantically meaningful outputs.
- With a constrained prompt, qwen can identify container identity and reject
  similar-page confusion.
- Element-meaning output is not stable enough to be used as a deterministic
  evidence source yet.
- Latency for short completions was ~120ms predicted time in the controlled
  server test.

## Conclusion

- Qwen2.5-VL-3B-UI-R1 is a promising **experimental** UI semantic assistant.
- Not suitable as a direct replacement for Fast Semantic / vector retrieval.
- Could be revisited if Slow Semantic / LLM-assisted evidence becomes justified.

## References

- `docs/models/local-model-inventory.md`