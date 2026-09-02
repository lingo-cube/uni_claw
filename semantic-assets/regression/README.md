# Regression Families Registry (Reference)

Historical failure families that motivated the current rules. Held-out corpus
contains NEW variants of these families (never the verbatim tuning cases).

| Family | Documented source | Held-out variants |
|---|---|---|
| Wrong-page Data usage (dev-D) | `DeveloperOptionsBenchmarkCorpus` dev-D; `docs/benchmarks/semantic-embedding-*` | ho-dev-F1, ho-wifi-F1, ho-net-F1 |
| Similar-page interference (dev-E) | `DeveloperOptionsBenchmarkCorpus` dev-E; Expanded golden E | ho-dev-E1, ho-wifi-E1, ho-net-E1, ho-root-E1 |
| Scrolled drift / text resolver | Expanded regression `reg-scrolled-drift`, `reg-text-resolver-failure` | ho-*-B1/B2 scroll subsets |
| Wrong container (root-negative) | `ContainerIdentityCorpora` root-negative-001 | ho-root-F1, ho-root-E2 |
| Adversarial similar page | Expanded adversarial `adv-similar-page` | ho-*-F2 text-overlap probes |