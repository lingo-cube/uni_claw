## MODIFIED Requirements

### Requirement: TypeHint.FromString uses precise alias dictionary with fallback
`TypeHint.FromString` SHALL resolve input using a `Dictionary<string, TypeHint>` alias map with `TryGetValue` for exact key match. Unknown keys SHALL fall back to `TypeHint.Text`. The alias map SHALL contain:
- 8 exact enum values: `"clickable_text"`→ClickableText, `"switch"`→Switch, `"slider"`→Slider, `"button"`→Button, `"icon"`→Icon, `"input_field"`→InputField, `"text"`→Text, `"image"`→Image
- 7 Python aliases: `"clickable"`→ClickableText, `"click"`→ClickableText, `"toggle"`→Switch, `"checkbox"`→Switch, `"check"`→Switch, `"btn"`→Button, `"input"`→InputField, `"field"`→InputField, `"img"`→Image, `"picture"`→Image
- 3 C# extension aliases: `"seekbar"`→Slider, `"edit"`→InputField, `"textbox"`→InputField

Substring matching (`Contains`) SHALL NOT be used.

#### Scenario: FromString exact enum value match
- **WHEN** `TypeHint.FromString("button")` is called
- **THEN** it returns `TypeHint.Button` (exact key in alias map)

#### Scenario: FromString Python alias match
- **WHEN** `TypeHint.FromString("click")` is called
- **THEN** it returns `TypeHint.ClickableText` (Python alias, previously missing)

#### Scenario: FromString Python alias check maps to Switch
- **WHEN** `TypeHint.FromString("check")` is called
- **THEN** it returns `TypeHint.Switch` (Python alias, previously missing)

#### Scenario: FromString C# extension alias match
- **WHEN** `TypeHint.FromString("seekbar")` is called
- **THEN** it returns `TypeHint.Slider` (C# extension alias for Android SeekBar)

#### Scenario: FromString unknown value falls back to Text
- **WHEN** `TypeHint.FromString("scrollable")` is called
- **THEN** it returns `TypeHint.Text` (not `TypeHint.Slider` — no substring "scroll" match)

#### Scenario: FromString unknown value falls back to Text for any unrecognized input
- **WHEN** `TypeHint.FromString("dropdown")` is called
- **THEN** it returns `TypeHint.Text` (not any other TypeHint value)

#### Scenario: FromString is case-insensitive
- **WHEN** `TypeHint.FromString("BUTTON")` is called
- **THEN** it returns `TypeHint.Button` (lowercased before lookup)

#### Scenario: FromString is whitespace-tolerant
- **WHEN** `TypeHint.FromString(" button ")` is called
- **THEN** it returns `TypeHint.Button` (trimmed before lookup)

### Requirement: SelectionState.FromString uses precise alias sets with fallback
`SelectionState.FromString` SHALL resolve input using two `HashSet<string>` alias sets with exact `Contains` match. Disabled aliases SHALL be checked first (before Selected aliases) to prevent `"inactive"` from matching any `"active"` substring. Unknown keys SHALL fall back to `SelectionState.Normal`.

The alias sets SHALL contain:
- SelectedAliases (5 values): `"selected"`, `"active"`, `"checked"`, `"highlight"`, `"highlighted"`
- DisabledAliases (6 values): `"disabled"`, `"inactive"`, `"hidden"`, `"gray"`, `"grayed"`, `"dimmed"`

Substring matching (`Contains` on the original value) SHALL NOT be used.

#### Scenario: FromString Python alias highlight maps to Selected
- **WHEN** `SelectionState.FromString("highlighted")` is called
- **THEN** it returns `SelectionState.Selected` (Python alias, previously missing)

#### Scenario: FromString Python alias highlight without -ed maps to Selected
- **WHEN** `SelectionState.FromString("highlight")` is called
- **THEN** it returns `SelectionState.Selected` (Python alias, previously missing)

#### Scenario: FromString Disabled aliases checked before Selected
- **WHEN** `SelectionState.FromString("inactive")` is called
- **THEN** it returns `SelectionState.Disabled` (not `SelectionState.Selected`, even though "inactive" contains "active" substring)

#### Scenario: FromString unknown value falls back to Normal
- **WHEN** `SelectionState.FromString("activated")` is called
- **THEN** it returns `SelectionState.Normal` (not `SelectionState.Selected` — not in SelectedAliases)

#### Scenario: FromString grayed maps to Disabled
- **WHEN** `SelectionState.FromString("grayed")` is called
- **THEN** it returns `SelectionState.Disabled` (Python alias)

#### Scenario: FromString is case-insensitive
- **WHEN** `SelectionState.FromString("SELECTED")` is called
- **THEN** it returns `SelectionState.Selected` (lowercased before lookup)

## ADDED Requirements

### Requirement: TypeHint.IsValid validates string input including aliases
`TypeHintExtensions` SHALL expose `public static bool IsValid(string value)` that checks whether the given string can be successfully parsed by `FromString`. It SHALL return true for all keys in the alias map (including aliases), false for unknown values.

#### Scenario: IsValid returns true for exact enum value
- **WHEN** `TypeHint.IsValid("button")` is called
- **THEN** it returns `true`

#### Scenario: IsValid returns true for alias
- **WHEN** `TypeHint.IsValid("btn")` is called
- **THEN** it returns `true` (alias is parseable by FromString)

#### Scenario: IsValid returns false for unknown value
- **WHEN** `TypeHint.IsValid("scrollable")` is called
- **THEN** it returns `false` (not parseable, would fall back to Text)

#### Scenario: IsValid returns false for null or empty
- **WHEN** `TypeHint.IsValid("")` is called
- **THEN** it returns `false`

### Requirement: SelectionState.IsValid validates string input including aliases
`SelectionStateExtensions` SHALL expose `public static bool IsValid(string value)` that checks whether the given string can be successfully parsed by `FromString` to a known state (not the fallback Normal). It SHALL return true for values in SelectedAliases or DisabledAliases, true for `"normal"`, false for unknown values.

#### Scenario: IsValid returns true for exact enum value
- **WHEN** `SelectionState.IsValid("selected")` is called
- **THEN** it returns `true`

#### Scenario: IsValid returns true for alias
- **WHEN** `SelectionState.IsValid("checked")` is called
- **THEN** it returns `true` (alias maps to Selected)

#### Scenario: IsValid returns true for normal
- **WHEN** `SelectionState.IsValid("normal")` is called
- **THEN** it returns `true` (exact enum value)

#### Scenario: IsValid returns false for unknown value
- **WHEN** `SelectionState.IsValid("activated")` is called
- **THEN** it returns `false` (would fall back to Normal, not a known alias)
