# Uni-Claw - Core AI Context

> **Project**: AI-driven mobile UI automation traversal framework
> **Version**: V6.3 | Updated: 2026-06-08

---

## Project Identity

**What**: Modular, testable mobile app UI automation framework using AI vision analysis + ADB control
**Tech Stack**: Python 3.10+, ADB, DeepSeek/Anthropic AI
**Architecture**: Interface-driven, dependency injection, event-driven
**V6 Features**: Simulation engine, state machine, distributed tracing

---

## Core Design Principles

1. **Interface-first** - Core components use abstract interfaces (Protocol/ABC)
2. **Dependency injection** - Testability through loose coupling
3. **State separation** - State management independent of business logic
4. **Observability-first** - Built-in tracing, metrics, logging everywhere
5. **Simulation priority** (V6) - Test without devices using mock runners
6. **Testing discovers problems** - Write tests before/with implementation

---

## Essential Module Map

| Module | Responsibility | Key Location |
|--------|-----------------|--------------|
| **AI Service** | AI strategy decisions | `src/ai/` |
| **Traversal Engine** | Core traversal logic | `src/traversal/` |
| **GraphEngine** (V6) | Graph-based execution | `src/traversal/graph_engine.py` |
| **Simulation** (V6) | Offline testing/validation | `src/simulation/` |
| **State Management** | State persistence | `src/state/`, `src/state_machine/` |
| **Exception Handling** | Exception chain processing | `src/exception/` |
| **Observability** | Tracing/metrics/logs | `src/trace/`, `src/analysis/` |
| **Graph Model** (V6) | Declarative traversal plans | `src/graph/` |

---

## Before You Work

1. **Read relevant module README** - Start with `src/{module}/README.md`
2. **Follow code conventions** - See `CLAUDE_CONVENTIONS.md`
3. **Check current status** - See `CLAUDE_STATUS.md`
4. **Use proper workflow** - See `CLAUDE_WORKFLOW.md` for OpenSpec process

---

## File Placement Rules

> **CRITICAL**: NEVER create files at project root. Use the structure below.

| File Type | Location | Examples |
|-----------|----------|----------|
| **CLAUDE context** | `*.md` at root only if specifically for Claude | `CLAUDE.md`, `CLAUDE_*.md` |
| **User docs** | `docs/` | `README.md`, `SETUP.md`, `GUIDES.md` |
| **Architecture** | `docs/architecture/` | `ARCHITECTURE.md`, `modules/*.md` |
| **Testing** | `tests/` or `docs/testing/` | Unit tests, test plans |
| **Scripts** | `scripts/` | `verify_*.py`, `setup_*.sh` |
| **Temporary** | `temp/` | Draft designs, scratch work |
| **Specs/PRDs** | `docs/superpowers/specs/` or `docs/` | Design documents |

**temp/ directory**: For transient work (draft designs, experiments). Not for committed code.
Before creating any file: 1) Check if it belongs in existing structure, 2) Use appropriate subdirectory, 3) Ask if uncertain.

---

## Quick Reference

| Resource | Path | Purpose |
|----------|------|---------|
| **Full doc index** | `docs/INDEX.md` | Complete documentation catalog |
| **Current status** | `CLAUDE_STATUS.md` | Project state and validation |
| **Workflow** | `CLAUDE_WORKFLOW.md` | OpenSpec change process |
| **Conventions** | `CLAUDE_CONVENTIONS.md` | Code style and patterns |
| **Testing guide** | `docs/testing/README.md` | Testing standards |
| **Architecture** | `docs/architecture/ARCHITECTURE.md` | System design overview |

---

## Common Commands

```bash
# Run validation
python scripts/verify_refactor.py

# Run tests
pytest tests/ -v
pytest tests/v6/ -v  # V6 simulation tests

# Check coverage
pytest tests/ --cov=src --cov-report=term-missing
```

---

**For detailed documentation**: See `docs/INDEX.md`
**For architecture**: See `docs/architecture/ARCHITECTURE.md`
