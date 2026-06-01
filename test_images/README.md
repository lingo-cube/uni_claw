# 测试图片存放位置

将测试图片放在这个文件夹中。

## 支持格式
- PNG (.png)
- JPEG (.jpg, .jpeg)

## 使用方法

1. **放置图片**
   ```
   test_images/
   ├── screenshot1.png
   ├── screenshot2.jpg
   └── ...
   ```

2. **设置 API Key**
   ```bash
   export MIMO_API_KEY=your_key_here
   ```

3. **运行测试**
   ```bash
   python test_images_with_mimo.py
   ```

## 输出示例

```
[1/3] Testing: screenshot1.png
------------------------------------------------------------
   Size: 45678 bytes
   📸 Analyzing...
   ✅ Analysis complete:
      Current path: ['DiLink', '互联']
      Level1 menus: 2
         🟢 DiLink (0.08, 0.12)
         ⚪ DiPilot (0.08, 0.20)
      ...
```
