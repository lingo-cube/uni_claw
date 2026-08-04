# conditional-page-analyzer-assembly Specification

## ADDED Requirements

### Requirement: Local mode skips ObservationPipeline, uses bare PageAnalyzer

When `--provider local` is specified, the Host SHALL wire PageAnalyzer directly via `InvalidatingPageAnalysisCache`, skipping the ObservationPipeline. When provider is NOT local, the existing ObservationPipeline → InvalidatingPageAnalysisCache chain SHALL be preserved unchanged.

#### Scenario: Local mode uses bare PageAnalyzer

- **WHEN** `--provider local` is specified
- **THEN** `InvalidatingPageAnalysisCache` wraps `brain.PageAnalyzer` directly, NOT an ObservationPipeline

#### Scenario: Cloud mode uses ObservationPipeline

- **WHEN** provider is "claude", "sensenova", "qwen", or "mock"
- **THEN** assembly path is `ObservationPipeline → InvalidatingPageAnalysisCache` (existing behavior unchanged)
