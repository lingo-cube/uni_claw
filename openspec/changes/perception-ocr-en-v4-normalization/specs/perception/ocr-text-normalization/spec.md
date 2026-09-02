## Purpose

Defines the text-normalization contract applied to OCR token output as a fixed layer before
fusion: it repairs systematic recognition noise (concatenated multi-word tokens, trailing
punctuation bleed, digit/letter and connector variants) so downstream text matching,
dedup, and row stabilization receive stable input. The layer applies to any rec model,
not only the target English model.

## ADDED Requirements

### Requirement: Concatenated multi-word tokens are recovered

When a rec model glues a multi-word phrase into a single token (e.g.
`EnableBluetoothstacklog`), the normalization layer SHALL output the phrase with canonical
spaces so downstream word/matching logic can see the words. Correctly-spaced tokens SHALL
remain semantically unchanged.

#### Scenario: Glued token is split

- **WHEN** the normalization layer receives token `Disableadbauthorizationtimeout`
- **THEN** it outputs the canonical-spaced `Disable adb authorization timeout`

#### Scenario: Already-correct token is preserved

- **WHEN** the normalization layer receives token `Enable Bluetooth stack log`
- **THEN** the output preserves the correct form unchanged

### Requirement: Trailing punctuation is stripped, semantic punctuation kept

Trailing punctuation bled into a phrase (e.g. `Developer options.`) SHALL be stripped when
it does not affect meaning; punctuation with semantic content (e.g. `&` in
`Network & internet`) SHALL be preserved.

#### Scenario: Trailing period stripped

- **WHEN** the normalization layer receives token `Developer options.`
- **THEN** it outputs `Developer options`

#### Scenario: Semantic ampersand preserved

- **WHEN** the normalization layer receives token `Network & internet`
- **THEN** it outputs the token unchanged (the `&` is preserved)

### Requirement: Style variants normalize to a stable form

Digit/letter confusion and connector variants (e.g. `SCROLL_O2` vs `SCROLL_02`, `HCl` vs
`HCI`, `NAV_03 - Page B` vs `NAV_03- Page B` vs `NAV_03  Page B`) SHALL be normalized by
stable rules so the same semantic text yields consistent normalized output across
recognition runs.

#### Scenario: Digit/letter confusion normalized

- **WHEN** the layer sees `SCROLL_O2 DUPLICATE TITLES` and `SCROLL_02 DUPLICATE TITLES`
- **THEN** both normalize to the same canonical form per the stable rules

#### Scenario: Whitespace-hyphen variants collapse

- **WHEN** the layer sees `NAV_03 - Page B`, `NAV_03- Page B`, and `NAV_03  Page B`
- **THEN** all three produce the same canonical form

### Requirement: Unknown cases fail closed

The layer SHALL NOT invent missing words: concatenation recovery is applied only where
rules/dictionary support it; unsupported residual differences are preserved truthfully,
never swallowed, and never guessed.

#### Scenario: Unsupported residual is preserved, not invented

- **WHEN** a token's concatenation cannot be recovered by rules/dictionary
- **THEN** the layer preserves the remainder as-is without fabricating words

## Constraints

- The layer is a read-only transform: it SHALL NOT mutate raw detection/recognition
  evidence, only the token text entering fusion.
- Rules apply to any rec model; model switches SHALL NOT bypass the layer.
- The layer's effect SHALL be covered by text-level ground-truth assertions in the
  evaluation side.