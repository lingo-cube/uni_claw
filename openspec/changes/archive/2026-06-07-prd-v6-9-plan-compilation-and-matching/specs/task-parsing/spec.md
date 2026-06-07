## ADDED Requirements

### Requirement: Natural language to intent slots parsing
The system SHALL parse natural language tasks into IntentSlots using heuristic rules.

#### Scenario: Extract target_app from Chinese keywords
- **WHEN** task contains "设置"
- **THEN** target_app = "设置"
- **WHEN** task contains "显示" or "屏幕"
- **THEN** target_app = "显示" or "屏幕"
- **WHEN** task contains "声音" or "音频"
- **THEN** target_app = "声音" or "音频"
- **WHEN** task contains "网络" or "wifi" or "蓝牙"
- **THEN** target_app = matched keyword
- **WHEN** task contains "存储"
- **THEN** target_app = "存储"
- **WHEN** task contains "应用" or "应用程序"
- **THEN** target_app = "应用"
- **WHEN** task contains "微信"
- **THEN** target_app = "微信"
- **WHEN** task contains "相册" or "照片"
- **THEN** target_app = "相册" or "照片"

#### Scenario: Extract target_app from English keywords
- **WHEN** task contains "settings"
- **THEN** target_app = "settings"
- **WHEN** task contains "display" or "screen"
- **THEN** target_app = matched keyword
- **WHEN** task contains "sound" or "audio"
- **THEN** target_app = matched keyword
- **WHEN** task contains "network" or "wifi" or "bluetooth"
- **THEN** target_app = matched keyword
- **WHEN** task contains "storage"
- **THEN** target_app = "storage"
- **WHEN** task contains "apps" or "applications"
- **THEN** target_app = "apps"
- **WHEN** task contains "wechat"
- **THEN** target_app = "wechat"
- **WHEN** task contains "gallery" or "photos"
- **THEN** target_app = matched keyword

#### Scenario: Extract scope from search keywords
- **WHEN** task contains "找到" or "查找" or "搜索"
- **THEN** scope = "target_only"
- **WHEN** task contains "部分" or "一些"
- **THEN** scope = "partial"
- **WHEN** no search/partial keywords present
- **THEN** scope = "full" (default)

#### Scenario: Extract target from search keywords
- **WHEN** task contains "找到" followed by text
- **THEN** target = text after "找到" (trimmed of punctuation)
- **WHEN** task contains "查找" followed by text
- **THEN** target = text after "查找" (trimmed of punctuation)
- **WHEN** task contains "搜索" followed by text
- **THEN** target = text after "搜索" (trimmed of punctuation)
- **WHEN** task contains "查看" followed by text
- **THEN** target = text after "查看" (trimmed of punctuation)

#### Scenario: Chinese task example
- **WHEN** task = "遍历设置找到版本号"
- **THEN** target_app = "设置"
- **THEN** scope = "target_only"
- **THEN** target = "版本号"

#### Scenario: Punctuation stripping from target
- **WHEN** extracted target ends with "。" or "." or "！" or "!"
- **THEN** punctuation is stripped from target
- **THEN** target contains only the meaningful text

#### Scenario: No app keyword found
- **WHEN** task contains no recognized app keywords
- **THEN** target_app = None
- **THEN** parsing continues with other fields

#### Scenario: No target keyword found
- **WHEN** task contains no search keywords
- **THEN** target = None
- **THEN** scope may default to "full"

#### Scenario: Return IntentSlots object
- **WHEN** parsing completes
- **THEN** system returns IntentSlots with extracted fields
- **THEN** fields with no matches are None or default values
