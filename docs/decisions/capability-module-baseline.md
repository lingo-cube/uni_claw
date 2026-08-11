# Capability Module Baseline

> 2026-08-11 | Status: FROZEN
> Baseline: cfe02c6 (SwitchStateReader validated) + module structure applied
> Scope: Module topology, contracts, authority, dependency direction

---

## 1. Module Topology

```
                    Agent (semantic authority)
                         |
              capability consumption
                         |
          +--------------+--------------+
          |                             |
        Brain                       Perception
    (reasoning /                      |
     interpretation)               Vision
                                 (visual evidence)

    Traversal / Environment
          |
       Operator
    (physical execution)
          |
    External World
```

### Vision = Submodule of Perception

Vision is visual perception — a subset of perception capabilities. The structure is `Capabilities/Perception/Vision/`. Vision depends on Perception's output (frames, bounds); Perception provides the observation context. No circular dependency.

### Directory Structure

```
src/UniClaw.Runtime/Capabilities/
  Brain/                          — .gitkeep (empty, minimal)
  Perception/
    Vision/
      ISwitchStateReader.cs       — first vision capability port
  Operator/                       — .gitkeep (empty, minimal)
```

### Project Boundaries

All four modules currently share the Runtime assembly. Future adapter projects (e.g., `UniClaw.Runtime.Adapters`) will hold implementations that require external dependencies (SkiaSharp, ADB, platform-specific libraries). Module ports stay in Runtime; implementations go in adapters.

---

## 2. Module Contracts

### BRAIN

| Attribute | Value |
|---|---|
| **Purpose** | Bounded reasoning/interpretation capability domain |
| **Input** | Semantic evidence, observations, capability context |
| **Output** | Advisory reasoning — interpretation, disambiguation, inference |
| **Authority** | ADVISORY / MECHANISM — does not replace Agent |
| **State ownership** | Mechanism-local only (model resources); no semantic state |
| **Current implementation** | NONE — structurally present, functionally minimal |
| **Allowed dependencies** | Model types, Agent-consuming contracts |
| **Forbidden dependencies** | Agent decision internals, Container ownership, Traversal protocol |
| **Examples** | Future: model-assisted ambiguity reasoning, uncertain evidence analysis |
| **NOT** | LLM/VLM integration (not yet purchased), semantic memory, planner |

### PERCEPTION

| Attribute | Value |
|---|---|
| **Purpose** | External world → observable evidence / Observation inputs |
| **Input** | Raw perception signals (screenshots, accessibility, device data) |
| **Output** | Evidence/Observation inputs — not truth, not belief |
| **Authority** | EVIDENCE PRODUCER |
| **State ownership** | Immutable captured frames only; no semantic state |
| **Current implementation** | Module directory; real perception in uni-claw (Python pipeline) |
| **Allowed dependencies** | Model types, IEnvironment (adapter implements IEnvironment) |
| **Forbidden dependencies** | Agent decision authority, Container belief ownership |
| **Examples** | Screenshot acquisition, OCR, YOLO detection, fusion, accessibility extraction |
| **NOT** | Business intent, task completion, action authorization |

### VISION

| Attribute | Value |
|---|---|
| **Relationship to Perception** | SUBMODULE — Vision is visual perception |
| **Purpose** | Visual perception evidence — image → semantic visual signals |
| **Input** | Visual frame + normalized bounds |
| **Output** | Qualitative visual evidence (ON/OFF/UNKNOWN) |
| **Authority** | EVIDENCE PRODUCER |
| **State ownership** | None (stateless, frame-scoped) |
| **First capability** | ISwitchStateReader — toggle/switch ON/OFF/UNKNOWN reading |
| **Future** | Checkbox state, slider state, icon recognition, visual relationships |
| **Allowed dependencies** | Model types (ElementBounds), perception frame context |
| **Forbidden dependencies** | Agent decisions, Container belief, goal completion |
| **NOT** | VLM, semantic reasoning, model training |

### OPERATOR

| Attribute | Value |
|---|---|
| **Purpose** | Authorized execution intent → external-world physical operation |
| **Input** | DeviceAction (already authorized by Agent/Traversal) |
| **Output** | ActionResult (dispatch outcome only — Dispatched/Rejected/TimedOut) |
| **Authority** | AUTHORIZED EFFECT MECHANISM — not semantic decision |
| **State ownership** | Mechanism-local only (device/connection handles); no semantic state |
| **Current reality pressure** | uni-claw `AdbActionExecutor` — Tap, Swipe, KeyEvent, Launch via ADB |
| **Allowed dependencies** | IEnvironment, DeviceAction, platform execution libraries |
| **Forbidden dependencies** | Capability selection, target identity, business intent, goal completion |
| **Examples** | Tap, swipe, text input, key event, ADB execution |
| **NOT** | "Should Wi-Fi be enabled?", semantic recovery policy |

---

## 3. Authority Rules (Frozen)

| Authority | Owner |
|---|---|
| Semantic decision | Agent |
| Local semantic state | Container |
| Execution protocol | Traversal |
| External-world boundary | Environment |
| Reasoning / interpretation | Brain (advisory, not authoritative) |
| Evidence production | Perception / Vision |
| Physical mechanism execution | Operator |

No module may steal another module's authority.

---

## 4. Dependency Direction (Frozen)

```
Agent → Container → Traversal → Environment  (graduated Core)
                                         ↑
                              Capability adapters implement IEnvironment

Brain → Model types only (advisory to Agent)
Perception → Model types + IEnvironment (adapter)
  Vision → Model types + Perception frame context
Operator → DeviceAction + IEnvironment (adapter)

Forbidden:
  Perception/Vision → Agent decision internals
  Operator → Capability selection
  Brain → Agent replacement
  Runtime Core → Physical adapter implementations
  External libraries → Semantic Core
```

---

## 5. State Rule (Frozen)

- One mutable semantic state → one owner (Container)
- Capability modules may own mechanism-local state only
- Immutable frames, connection handles, model resources are allowed
- Duplicated belief or decision state is forbidden

---

## 6. Facade / Provider Policy (Frozen)

| Decision | Status |
|---|---|
| BrainFacade | NOT PURCHASED |
| PerceptionFacade | NOT PURCHASED |
| VisionFacade | NOT PURCHASED |
| OperatorFacade | NOT PURCHASED |
| ProviderRegistry | NOT PURCHASED |
| CapabilityRegistry | NOT PURCHASED |
| IProvider / ICapabilityProvider | NOT PURCHASED |
| Plugin system | NOT PURCHASED |

**ISwitchStateReader** is the only approved specific port. Facade is purchased only when multiple consumers require a stable coarse-grained entrypoint.

---

## 7. Harness Integration

All capability module proofs use the graduated Harness:

```
COMPONENT → SIMULATION → REPLAY → REALITY-SEEDED/REALITY → LIVE
```

Capability modules do not get separate truth systems. Reuse: Reality Asset, Frame, Trace, Scenario, Simulation, Replay, Provenance.

---

## 8. Future Locations

| Item | Location |
|---|---|
| ISwitchStateReader | `Capabilities/Perception/Vision/ISwitchStateReader.cs` (Core port) |
| ImageSwitchStateProvider | Future adapter project with SkiaSharp dependency |
| Physical perception adapter | Future adapter project translating uni-claw PageAnalysis → Observation |
| ADB operator | uni-claw `UniClaw.Device/AdbActionExecutor.cs` (existing, separate assembly) |
| Brain concrete types | NONE until capability purchase |

---

## 9. Architecture Guards

G1: Brain cannot own/replace Agent.
G2: Perception/Vision cannot depend on Agent decision internals.
G3: Operator cannot choose semantic business Capability.
G4: Runtime Core cannot depend outward on physical adapter implementations.
G5: External/image/device libraries do not leak into semantic Core.
G6: No duplicate semantic mutable-state owner appears in capability modules.

---

## 10. First Falsifiers

| Falsifier | Status |
|---|---|
| F1: OCR/YOLO/fusion maps into Perception | PASS — uni-claw pipeline is the perception implementation |
| F2: ISwitchStateReader maps into Vision | PASS — port at `Perception/Vision/ISwitchStateReader.cs` |
| F3: AdbActionExecutor maps into Operator | PASS — uni-claw `UniClaw.Device/AdbActionExecutor.cs` |
| F4: Brain can remain minimal | PASS — no concrete capability requires Brain implementation yet |
| F5: No module requires Agent/Container/Traversal changes | PASS — zero Core delta |
| F6: SwitchStateReader continues as Vision vertical slice | PASS — unchanged contract |

STOP.
