# Design

## New Function

`normalize_coordinates(x: float, y: float) -> Coordinate`

- Accept raw float coordinates
- Clamp to [0.0, 1.0]
- Return Coordinate dataclass

## Design Decisions
- Place in src/models/content_models.py alongside existing Coordinate class
- Pure function, no side effects
- Follow DI pattern — injectable via constructor where needed
