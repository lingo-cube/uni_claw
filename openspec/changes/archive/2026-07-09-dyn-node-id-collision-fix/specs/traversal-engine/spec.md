## MODIFIED Requirements

### Requirement: TemplateInstantiator produces TraversalNode from template via 7-step flow

`TemplateInstantiator` SHALL be a sealed class that instantiates a `TraversalNode` from a template definition via a 7-step `instantiate()` flow: (1) resolve placeholders via `PlaceholderResolver` — all `{placeholder}` tokens in the template SHALL be substituted with values from the provided context; (2) construct `Operation` via `_create_operation` — using resolved values and the target/restore semantics from the template; (3) construct `Precondition` via `_create_precondition` — using resolved path values; (4) construct `ChildrenStrategy` via `_create_children_strategy` — mapping template strategy type to the corresponding `ChildrenStrategy` value; (5) construct `ErrorPolicy` via `_create_error_policy` — mapping template error handling to `ErrorPolicy`; (6) assemble the `TraversalNode` from all constructed components — NodeId SHALL use `dyn_{template.TemplateId}_{item_text}_{parentNodeId}` where parentNodeId is derived from the context or parentPath; (7) V6.9 path concatenation — `precondition.path` SHALL equal `parent_path + [node.name]`, where parent_path is the path of the parent node and node.name is the instantiated node's name. `TemplateInstantiator` SHALL depend on `PlaceholderResolver` (already implemented in C#) and SHALL NOT depend on AI or Traversal layers.

#### Scenario: TemplateInstantiator produces leaf node with disambiguated NodeId
- **WHEN** `TemplateInstantiator.instantiate(template, context={"item_text": "ON", "item_index": "0", "parent_node_id": "dyn_menu_container_Wi-Fi"}, parent_path=["root", "Wi-Fi"])` is called for a switch_leaf template
- **THEN** the resulting TraversalNode SHALL have NodeId `dyn_switch_leaf_ON_dyn_menu_container_Wi-Fi` — containing both item text and parent node ID for disambiguation

#### Scenario: TemplateInstantiator produces node with placeholder resolution
- **WHEN** `TemplateInstantiator.instantiate(template, context, parent_path=["home", "settings"])` is called with a template containing `{app_name}` placeholders in operation, precondition, and children_strategy fields
- **THEN** all `{app_name}` tokens SHALL be replaced with the value from context, and NodeId SHALL include parent context

#### Scenario: V6.9 path concatenation in instantiated nodes
- **WHEN** `TemplateInstantiator.instantiate(template, context, parent_path=["home", "settings"])` is called and the instantiated node's name is "wifi_switch"
- **THEN** `precondition.path` SHALL equal `["home", "settings", "wifi_switch"]`, and NodeId SHALL use the disambiguated formula

#### Scenario: TemplateInstantiator dependency constraints
- **WHEN** the `using` statements in `TemplateInstantiator` are inspected
- **THEN** there SHALL be no imports from AI or Traversal namespaces — only Domain, Graph.Models, and System namespaces are allowed

#### Scenario: Nested placeholder resolution in TemplateInstantiator
- **WHEN** `TemplateInstantiator.instantiate` encounters a template with nested placeholder references like `{action}_{target}`
- **THEN** `PlaceholderResolver` SHALL resolve both placeholders independently and concatenate the results, and NodeId SHALL use the disambiguated formula with parent context
