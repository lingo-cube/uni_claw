# LiteLLMBar Maintenance Convention

> 项目约定：LiteLLMBar 工具的单一维护点 + 部署流程。
> 最后更新: 2026-07-30

## 四个位置的角色

| 位置 | 角色 | 是否手改 |
|------|------|---------|
| `tools/litellm-bar/` | **唯一源** — src/, scripts/, install.sh, README.md, assets/ | ✅ 手改 |
| `~/.litellm/bar/` | 部署目标（运行时引用）— config_ops.py + scripts/* + AppIcon.iconset，无 main.swift | ❌ 产物 |
| `~/.litellm/quota_alert.py` | LiteLLM callback，= tools 源同步 | ❌ 产物 |
| `/Applications/LiteLLMBar.app/` | **程序入口**，编译产物 | ❌ 产物 |

## 维护规则

1. **改代码只在 `tools/litellm-bar/`**
2. **跑 `cd tools/litellm-bar && ./install.sh` 部署**
3. **绝不手改** `~/.litellm/bar/`、`/Applications/LiteLLMBar.app/`、`~/.litellm/quota_alert.py`
4. **install.sh 是唯一部署入口**（不直接调用 swiftc / cp）

## 部署特性

- install.sh 直接从 `tools/src/main.swift` 编译到 .app，不经 `~/.litellm/bar/src` 中转
- install.sh **不杀活网关**（保会话），末尾确保网关运行
- 部署期间 LiteLLM 网关 PID 不变、4000 端口持续可用

## 故障排查

如果运行二进制与 tools 源行为不符：**先跑 `install.sh` 重新部署**再判断，不要直接改产物文件。

## 来源

- Memory: [[litellmbar-single-source]]
- 权威清单: `tools/litellm-bar/README.md` 的「清单」小节
