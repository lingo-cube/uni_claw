# Scenario 03 — Scroll Does Not Change Container Identity

## Observation 1

```text
Items:
A B C

Fingerprint = F1
```

执行 Scroll。

## Observation 2

```text
Items:
D E F

Fingerprint = F2
```

要求：

```text
F1 != F2
```

但：

```text
ContainerIdentity remains the same
```

## Forbidden Behavior

系统不得仅因为：

```text
FingerprintChanged
```

就：

- 创建新 Container；
- PressBack；
- 判定 Navigation。

## Purpose

该 Scenario 锁定：

```text
Observation != Semantic Identity
Fingerprint != Page Identity
```
