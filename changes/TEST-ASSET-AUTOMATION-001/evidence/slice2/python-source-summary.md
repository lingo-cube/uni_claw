# Python operations slice source summary

- `tools/test-assets` preserves the legacy `resolved/list/validate/path` commands and dispatches the new operation commands to `test_asset_operations`.
- `begin` binds an immutable layout through `AssetRun`, writes local provenance, and indexes only logical baseline references; `scope` resolves only an unfinalized run through its historical binding.
- `finish` reuses `AssetRun.finish`, carries the operation into the receipt, retains pending-finalization state without rewriting a completed run, and reports finalizer failure separately from the caller's business result. `sourceStable` remains boolean/null compatible; the detailed measurement is `sourceStability`.
- `replay` creates an isolated replay run and starts only its own child process group; cancellation/timeout stop and wait for that group before finalization. Its CLI exit code is preserved independently from the carrier result.
- Historical bindings are revalidated against the indexed namespace/digest, and every category path is rechecked for link escape when returned by `scope`.
