# Required report delivery HTTP endpoints

`ReportDeliveryEndpoints.Map(routes, tools, runsRoot)` maps the required-report delivery API under `/api/uniclaw-runtime`. It projects delivery state and artifact links; report generation remains owned by `RuntimeToolHost` and its deterministic Python tool.

`POST /api/uniclaw-runtime/runs` accepts optional `requireReport: true`. The Host durably accepts the report requirement before attaching or dispatching the task, with owner `runtime-run:{runId}` and request `primary`. Omission retains the existing optional report behavior. Replaying an activation cannot add a requirement that was absent at admission. Execution reaches its terminal result before report generation; subsequent artifact projection updates preserve that result.

An explicit `run-report` tool invocation accepts a requirement with owner `artifact-set:{runDir}`. `reportRequestId` selects a new report version (default `primary`); `strictReport` selects the generator's existing strict input rules. A repeat key reuses the pinned generator and input bytes. Returned report paths reference the verified publication relative to `runsRoot`; the old configured report directory is a compatibility projection for diagnosis. Its loss does not invalidate delivery.

Runtime report sources must carry the matching `facts.runId`; manual artifact-set requests must use their original directory name. The Host checks the original before binding and checks the private runtime inputs again before generation. Each Python generator holds its own per-delivery OS lease, so a surviving child continues to block a second generator even after its Host is killed. Normal Host shutdown waits up to 10 seconds for tracked Runtime work and then cancels/waits up to 5 seconds for report processes; unfinished work references are emitted and their durable state remains queryable. These bounds do not claim power-loss or cross-node durability.

## Routes

| Method and path | Purpose |
|---|---|
| `GET /api/uniclaw-runtime/report-deliveries?ownerRef=<ref>` | List deliveries for an explicit owner. `ownerRef` is required. |
| `GET /api/uniclaw-runtime/runs/{runId}/report-deliveries` | List deliveries owned by `runtime-run:{runId}`. |
| `GET /api/uniclaw-runtime/report-deliveries/{deliveryId}` | Inspect one delivery; unknown IDs return 404. |
| `POST /api/uniclaw-runtime/report-deliveries/{deliveryId}/retry` | Retry a delivery. The body is optional; send `{"runDir":"<run-directory-name>"}` when the source has not been bound yet. |
| `GET /api/uniclaw-runtime/report-deliveries/{deliveryId}/artifacts/{fileName}` | Read a currently verified, declared output artifact. |

Inspection and list entries expose `deliveryId`, `status`, `readable`, `reason`, `sourceFingerprint`, `revision`, `attempts`, `ownerRef`, `requestId`, and `artifacts` (`fileName` plus an `href`). They do not expose storage paths. `status` describes the delivery lifecycle; `readable` separately reports whether the saved publication currently passes integrity checks.

Artifact reads are limited to filenames present in both the verified delivery outputs and the generator's declared output list. Missing or undeclared artifacts return 404; a detected byte/hash mismatch returns 409. A retry that still needs source inputs returns 409 with the current delivery projection. Internal paths and exception details are not returned in HTTP error bodies.

## Focused tests

`tests/UniClaw.Host.Dsh.Tests/ReportDeliveryEndpointTests.cs` uses a real temporary `WebApplication` and the repository's real tool registry. It covers empty and run-scoped lists, delivery inspection, a generated facts-only report, declared artifact reads, rejection of undeclared files, repeat retry, retry after deleting the original source directory, and the `AwaitingInputs` retry response. The test does not invoke diagnosis, a model, or a device.

Run only this suite with:

```sh
dotnet test tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj --no-restore --filter 'FullyQualifiedName~ReportDeliveryEndpointTests' -m:1 -nr:false
```
