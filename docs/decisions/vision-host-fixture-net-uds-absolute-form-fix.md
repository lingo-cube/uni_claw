# Vision Host Test Fixture — .NET UDS Absolute-Form Request Fix

> VISION_HOST_FIXTURE_NET_UDS_ABSOLUTE_FORM_FIX. 2026-08-18.
> Root cause verified by evidence; fix confined to the test fixture; Vision suite 79/79 green.

## 1. Result

`VISION_HOST_FIXTURE_NET_UDS_ABSOLUTE_FORM_FIX` — the path-normalization fix is **correct
and verified**. Full Vision suite passes 79/79 (previously 10 failures). The change is
scoped to one test fixture; no production/contract change.

## 2. Symptom

10 Vision tests failed with `HttpRequestException : 404 (Not Found)` despite the fixture
and Python environment being valid:
- `VisionHostBehavioralProofs`: H6, H8, H11, H12, H14
- `VisionIdentityVerificationTests`: DI16 (Config/Model/Pipeline/Schema), IdentityMatch_NoThrow

These are the tests that drive the minimal Python `http.server` fixture through
`.NET HttpClient` with a Unix-socket `ConnectCallback`. The real-uvicorn tests
(`VisionHostFactoryCompositionTests`) already passed — the failure was specific to the
minimal fixture path.

## 3. Root cause (evidence-backed)

Hypothesis of an HTTP/1.0-vs-1.1 framing mismatch was **refuted first**: setting
`protocol_version = "HTTP/1.1"` on the fixture kept failing with 404.

Capture of the actual request line received by the fixture:

```text
requestline='GET http://localhost/version HTTP/1.1'
path='http://localhost/version'
```

`.NET HttpClient` with a custom UDS `ConnectCallback` emits an **absolute-form**
request-target (`GET http://localhost/version`) rather than origin-form (`GET /version`).
The fixture routes on `self.path == "/version"`, which does not match the absolute-form
value → falls through to `404`.

Controlled comparison on the **same Unix socket** proved the server was healthy:
- raw socket write of `GET /health HTTP/1.1 ... Connection: close` → `200 {"warm": false}`
- `.NET HttpClient` same socket → `404`

Conclusion: root cause is **request-target form (absolute vs origin)**, triggered by the
UDS `ConnectCallback` behavior — not UDS itself, not missing Python deps, not the venv.

## 4. Fix

`tests/UniClaw.Runtime.Tests/Vision/vh_test_server.py` — normalize the path once on request
receipt, covering both `do_GET` and `do_POST` (no GET/POST asymmetry):

```python
def _normalize_path(self) -> None:
    # .NET HttpClient with a UDS ConnectCallback emits absolute-form
    # request-targets ("GET http://localhost/version"), so self.path
    # arrives as "http://localhost/version" and would miss the
    # origin-form route matchers → 404. Normalize once on receipt, for
    # both do_GET and do_POST, so routing is uniform.
    import urllib.parse
    if self.path.startswith(("http://", "https://")):
        self.path = urllib.parse.urlparse(self.path).path or "/"
```

## 5. Verification

| Scope | Result |
|---|---|
| Full Vision suite | ✅ 79/79 (10 failing → 0) |
| Full solution suite | ✅ 1428/1430 |

Remaining 2 failures are unrelated to this fix and pre-existing:
1. `Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete` — requires a live Android
   emulator (environmental).
2. `DiscoveredBranchEffectRevalidationScenarioTests.Positive_Assertions1to5And8to11_...` —
   part of the in-progress OpenWorld working-tree change, not consumed via the fixture.

## 6. Side-effect assessment

- Consumers of the fixture are exclusively `VisionHostBehavioralProofs` and
  `VisionIdentityVerificationTests`; no production C#, wire protocol, Runtime, or uvicorn
  path is touched.
- Absolute-form emission is standard `.NET` behavior for a UDS `ConnectCallback`; the real
  uvicorn chain (which correctly handles origin-form) was already green, so the fix does
  not mask a production defect.
- No behavioral-proof assertion semantics were changed — only routing so the crafted
  payloads actually reach the handler.
- Authority: implementation/test-scope only (fixture fix); not architecture authority.
