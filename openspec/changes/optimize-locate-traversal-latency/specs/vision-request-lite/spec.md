## ADDED Requirements

### Requirement: Vision screenshots are downscaled before encoding

Screenshots sent to remote vision models SHALL be downscaled to a maximum analysis width (e.g., 720px) before base64 encoding. The original full-resolution capture SHALL remain available for evidence assets.

#### Scenario: Full-size capture encoded small

- **WHEN** a page analysis issues a remote vision request
- **THEN** the request payload contains a downscaled image while the evidence asset retains the original resolution

#### Scenario: Downscale preserves element detectability

- **WHEN** the analyzer receives a vision response for a downscaled image
- **THEN** the response parses with the same item and coordinate schema as a full-resolution response

### Requirement: Verify-only calls use a lightweight change-check prompt

The prompt registry SHALL provide a lightweight verify variant that requests only a minimal change/page-identity answer, and verify-only vision calls SHALL use it instead of the full analysis prompt.

#### Scenario: Verify call uses light prompt

- **WHEN** a post-action verification invokes the remote vision model
- **THEN** the request uses the lightweight change-check prompt variant with a reduced response budget
