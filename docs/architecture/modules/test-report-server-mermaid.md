# Test Report Server - Architecture Diagrams

**Version**: 1.0
**Date**: 2026-06-06

## Component Interaction Diagram

```mermaid
flowchart TB
    subgraph Clients
        WEB[Web Browser Dashboard]
        API[REST API Client]
        CLI[CLI Tools]
    end

    subgraph HTTP["HTTP Server (Port 8003)"]
        HANDLER[RequestHandler]
        ROUTER[API Router]
    end

    subgraph Business["Business Logic Layer"]
        DS[TestResultsDataSource]
        ANALYZER[TestResultsAnalyzer]
        RUNNER[TestRunnerAPI]
    end

    subgraph Data["Data Layer"]
        RESULTS[test_results/*.json]
        RUNNER_SKILL[module-test Skill]
        COVERAGE[coverage.xml]
    end

    WEB -->|GET /api/*| HANDLER
    API -->|GET/POST /api/*| HANDLER
    CLI -->|POST /api/trigger| HANDLER

    HANDLER --> ROUTER
    ROUTER -->|read| DS
    ROUTER -->|analyze| ANALYZER
    ROUTER -->|trigger| RUNNER

    DS -->|load| RESULTS
    ANALYZER -->|aggregate| DS
    RUNNER -->|spawn subprocess| RUNNER_SKILL
    RUNNER_SKILL -->|write| RESULTS
    ANALYZER -->|optional| COVERAGE

    style WEB fill:#e1f5fe
    style API fill:#e1f5fe
    style CLI fill:#e1f5fe
    style HANDLER fill:#fff3e0
    style DS fill:#c8e6c9
    style ANALYZER fill:#c8e6c9
    style RUNNER fill:#c8e6c9
    style RESULTS fill:#f3e5f5
    style RUNNER_SKILL fill:#f3e5f5
```

## Class Diagram

```mermaid
classDiagram
    class TestResultsDataSource {
        -Path results_dir
        -int cache_ttl
        -Dict cache
        -TestResultValidator validator
        +load_results(module?)
        +get_cached_data(module?)
        +invalidate_cache(module?)
        +list_modules()
        +validate_schema(data)
    }

    class TestResultValidator {
        +validate_required_fields(data)
        +validate_summary_counts(data)
        +validate_timestamp_format(data)
    }

    class TestResultsAnalyzer {
        -TestResultsDataSource data_source
        -List history
        +aggregate_statistics()
        +calculate_pass_rate(module?)
        +identify_failing_tests()
        +detect_trends(window?)
        +get_freshness_report()
        +generate_summary()
    }

    class TestRunnerAPI {
        -Path project_root
        -Path test_runner_path
        -Dict active_runs
        -Dict run_status
        +run_module_tests(module)
        +run_all_tests()
        +cancel_run(run_id)
        +get_run_status(run_id)
        +list_active_runs()
    }

    class TestReportRequestHandler {
        +TestResultsDataSource data_source
        +TestResultsAnalyzer analyzer
        +TestRunnerAPI test_runner
        +do_GET()
        +do_POST()
        -_serve_results()
        -_serve_aggregate()
        -_handle_trigger()
    }

    TestResultsDataSource --> TestResultValidator
    TestResultsAnalyzer --> TestResultsDataSource
    TestReportRequestHandler --> TestResultsDataSource
    TestReportRequestHandler --> TestResultsAnalyzer
    TestReportRequestHandler --> TestRunnerAPI
```

## Data Flow Diagram

```mermaid
sequenceDiagram
    participant C as Client
    participant H as Handler
    participant DS as DataSource
    participant A as Analyzer
    participant R as RunnerAPI
    participant TR as Test Runner

    C->>H: GET /api/results
    H->>DS: load_results()
    DS->>DS: check cache
    alt cache valid
        DS-->>H: cached data
    else cache expired
        DS->>DS: read *.json files
        DS->>DS: validate schema
        DS->>DS: update cache
        DS-->>H: fresh data
    end
    H-->>C: JSON response

    C->>H: GET /api/aggregate
    H->>A: aggregate_statistics()
    A->>DS: get_cached_data()
    DS-->>A: all results
    A->>A: calculate aggregates
    A-->>H: statistics
    H-->>C: JSON response

    C->>H: POST /api/trigger {"module": "trace"}
    H->>R: run_module_tests("trace")
    R->>R: generate run_id
    R->>TR: spawn subprocess
    TR-->>R: process handle
    R-->>H: {run_id, status: started}
    H-->>C: 202 Accepted

    Note over C,R: Test runs in background...

    C->>H: GET /api/runs/{run_id}
    H->>R: get_run_status(run_id)
    alt still running
        R-->>H: {status: running}
    else completed
        R->>DS: load_results("trace")
        DS-->>R: new results
        R-->>H: {status: completed, results}
    end
    H-->>C: JSON response
```

## API Endpoint Map

```mermaid
mindmap
    root((Test Report API))
        GET Endpoints
            /api/results
                Per-module results
                Query: ?module=name
            /api/aggregate
                Overall statistics
                Pass rates
            /api/failures
                Failing tests list
                Grouped by module
            /api/freshness
                Data age report
                Stale warnings
            /api/trends
                Trend analysis
                Query: ?window=N
            /api/runs
                Active runs list
                Recent history
            /api/runs/{id}
                Run status
                Results when done
            /
                Dashboard HTML
        POST Endpoints
            /api/trigger
                Run specific module
                Body: {module}
            /api/trigger/all
                Run all modules
            /api/cancel/{id}
                Cancel running test
```

## Dashboard Layout

```mermaid
graph TB
    subgraph Dashboard
        HEADER[Header: Logo, Title, Last Updated]
        ROW1[Row 1: Overall Metrics]
        ROW2[Row 2: Two Columns]
        COL1[Column 1: Module Breakdown]
        COL2[Column 2: Coverage Summary]
        ROW3[Row 3: Failing Tests Table]
        ROW4[Row 4: Active Runs Panel]
        FOOTER[Footer: Controls, Settings]

        HEADER --> ROW1
        ROW1 --> ROW2
        COL1 --> ROW3
        COL2 --> ROW3
        ROW3 --> ROW4
        ROW4 --> FOOTER
    end
```

## Error Handling Flow

```mermaid
flowchart TD
    START[API Request] --> CHECK{Request Type}
    CHECK -->|GET| GET_FLOW[Get Data Flow]
    CHECK -->|POST| POST_FLOW[Post Action Flow]

    GET_FLOW --> DS_CHECK{Data Source}
    DS_CHECK -->|Missing dir| EMPTY[Return empty results]
    DS_CHECK -->|Valid dir| LOAD[Load JSON files]
    LOAD --> VALIDATE{Schema Valid?}
    VALIDATE -->|Yes| CACHE[Cache & Return]
    VALIDATE -->|No| SKIP[Skip file, Log warning]

    POST_FLOW --> TRIGGER{Trigger Type}
    TRIGGER -->|Module| RUN_MODULE[Run module tests]
    TRIGGER -->|All| RUN_ALL[Run all tests]
    TRIGGER -->|Cancel| CANCEL_RUN[Cancel run]

    RUN_MODULE --> CHECK_RUN{Runner exists?}
    CHECK_RUN -->|No| ERR_RUN[Return 500]
    CHECK_RUN -->|Yes| SPAWN[Spawn subprocess]
    SPAWN --> GEN_ID[Generate run_id]
    GEN_ID --> STORE_RUN[Store in active_runs]
    STORE_RUN --> RETURN_OK[Return 202 with run_id]

    EMPTY --> DONE[Return response]
    CACHE --> DONE
    SKIP --> DONE
    ERR_RUN --> DONE
    RETURN_OK --> DONE
```

## Timeline / Phases

```mermaid
gantt
    title Test Report Server Implementation Timeline
    dateFormat  HH
    axisFormat  %Hh

    section Phase 1
    Core Data Layer           :p1, 0, 2h

    section Phase 2
    Analysis Layer            :p2, after p1, 2h

    section Phase 3
    Test Runner Integration   :p3, after p2, 2h

    section Phase 4
    HTTP Server               :p4, after p3, 3h

    section Phase 5
    Web Dashboard             :p5, after p4, 3h

    section Phase 6
    Integration & Testing     :p6, after p5, 2h
```
