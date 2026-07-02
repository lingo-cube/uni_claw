## MODIFIED Requirements

### Requirement: MapAndroidClass returns intermediate string type
`ElementTypeMapper.MapAndroidClass` SHALL return `string` (intermediate type string) instead of `TypeHint`. The private dictionary SHALL be `Dictionary<string, string>` (renamed from `AndroidClassToTypeHintMap` to `AndroidClassMap`), containing 14 entries that match Python `ANDROID_CLASS_MAP` verbatim. The 3-level match logic (exact → substring → fallback) SHALL remain, but the fallback value SHALL be `"button"` (not `TypeHint.Button`).

#### Scenario: MapAndroidClass returns string for mapped class
- **WHEN** `ElementTypeMapper.MapAndroidClass("ToggleButton")` is called
- **THEN** it returns `"toggle"` (not `TypeHint.Switch`)

#### Scenario: MapAndroidClass returns string for exact match
- **WHEN** `ElementTypeMapper.MapAndroidClass("Switch")` is called
- **THEN** it returns `"switch"` (exact short-name match)

#### Scenario: MapAndroidClass substring fallback returns string
- **WHEN** `ElementTypeMapper.MapAndroidClass("android.widget.Switch")` is called (className contains "Switch")
- **THEN** it returns `"switch"` (substring match)

#### Scenario: MapAndroidClass default fallback returns "button"
- **WHEN** `ElementTypeMapper.MapAndroidClass("UnknownWidget")` is called (no exact or substring match)
- **THEN** it returns `"button"` (string fallback, not `TypeHint.Button`)

#### Scenario: Full 14-row mapping matches Python ANDROID_CLASS_MAP
- **WHEN** every key in `AndroidClassMap` is enumerated
- **THEN** the 14 entries are: `"Switch"`→`"switch"`, `"CheckBox"`→`"switch"`, `"RadioButton"`→`"switch"`, `"ToggleButton"`→`"toggle"`, `"Button"`→`"button"`, `"ImageButton"`→`"button"`, `"TextView"`→`"menu_item"`, `"EditText"`→`"input"`, `"LinearLayout"`→`"menu_item"`, `"RelativeLayout"`→`"menu_item"`, `"FrameLayout"`→`"menu_item"`, `"ConstraintLayout"`→`"menu_item"`, `"SeekBar"`→`"slider"`, `"RatingBar"`→`"slider"` — matching Python `ANDROID_CLASS_MAP` row-for-row

#### Scenario: ToggleButton mapping chain is complete
- **WHEN** `MapAndroidClass("ToggleButton")` → `ToMenuItemType` → `ToExpectedAction` is called
- **THEN** the chain produces `"toggle"` → `MenuItemType.Toggle` → `ExpectedAction.Toggle`

#### Scenario: AndroidClassMap property returns string dictionary
- **WHEN** `ElementTypeMapper.AndroidClassMap` is accessed
- **THEN** it is `IReadOnlyDictionary<string, string>` (not `IReadOnlyDictionary<string, TypeHint>`)

### Requirement: ToTypeHint maps intermediate string to visual classification
`ElementTypeMapper` SHALL expose `public static TypeHint ToTypeHint(string typeString)` mapping intermediate type strings to their visual TypeHint classification. Known mappings: `"switch"`→Switch, `"toggle"`→Switch, `"menu_item"`→ClickableText, `"input"`→InputField, `"slider"`→Slider, `"button"`→Button. Unknown strings SHALL fall back to `TypeHint.Text`.

#### Scenario: ToTypeHint maps switch to Switch
- **WHEN** `ElementTypeMapper.ToTypeHint("switch")` is called
- **THEN** it returns `TypeHint.Switch`

#### Scenario: ToTypeHint maps toggle to Switch (visual equivalence)
- **WHEN** `ElementTypeMapper.ToTypeHint("toggle")` is called
- **THEN** it returns `TypeHint.Switch` (ToggleButton's visual appearance = Switch)

#### Scenario: ToTypeHint maps menu_item to ClickableText
- **WHEN** `ElementTypeMapper.ToTypeHint("menu_item")` is called
- **THEN** it returns `TypeHint.ClickableText`

#### Scenario: ToTypeHint maps input to InputField
- **WHEN** `ElementTypeMapper.ToTypeHint("input")` is called
- **THEN** it returns `TypeHint.InputField`

#### Scenario: ToTypeHint falls back to Text for unknown
- **WHEN** `ElementTypeMapper.ToTypeHint("unknown_type")` is called
- **THEN** it returns `TypeHint.Text`

### Requirement: MapAndroidClass null defense throws DomainValidationException
`MapAndroidClass` SHALL throw `DomainValidationException` when `className` is null, carrying `FieldName = "className"` and `IllegalValue = null`.

#### Scenario: MapAndroidClass rejects null input
- **WHEN** `ElementTypeMapper.MapAndroidClass(null)` is called
- **THEN** it throws `DomainValidationException` with `FieldName` = `"className"` and `IllegalValue` = null

### Requirement: ToMenuItemType and ToExpectedAction remain unchanged
`ToMenuItemType` and `ToExpectedAction` SHALL continue to accept string keys and use `GetValueOrDefault` fallback. Their dictionaries and logic SHALL NOT change.

#### Scenario: ToMenuItemType still works with intermediate strings
- **WHEN** `ElementTypeMapper.ToMenuItemType("toggle")` is called
- **THEN** it returns `MenuItemType.Toggle` (same as before, string key unchanged)

#### Scenario: ToExpectedAction still works with intermediate strings
- **WHEN** `ElementTypeMapper.ToExpectedAction("toggle")` is called
- **THEN** it returns `ExpectedAction.Toggle` (same as before, string key unchanged)

## ADDED Requirements

### Requirement: DirectionExtensions.Values uses reflection
`DirectionExtensions.Values` SHALL derive from `[JsonPropertyName]` attributes via reflection (same pattern as MenuItemTypeExtensions/ExpectedActionExtensions), not from a hardcoded string array.

#### Scenario: Values matches JsonPropertyName attributes
- **WHEN** `DirectionExtensions.Values` is enumerated
- **THEN** it contains exactly `"left"`, `"right"`, `"top"`, `"bottom"` — matching the `[JsonPropertyName]` attributes on Direction enum members

#### Scenario: Values is dynamically derived not hardcoded
- **WHEN** the implementation of `DirectionExtensions.Values` is inspected
- **THEN** it uses `Enum.GetValues<Direction>().Select(GetStringValue).ToList()` (or equivalent reflection), not a literal `new[] { ... }` array
