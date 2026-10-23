# 验证与复现

级别：SCENARIO，真实核心导出 + 实际浏览器；未声称live设备/模型或人类视觉签字。

| method | expected | actual | evidence |
|---|---|---|---|
| node --test tools/world-visualizer/model.test.mjs | 矩形像素比例、负坐标、缺失尺寸及原有版本读取约束 | 修复前2项失败（portrait比例与累计范围）；修复后7/7通过 | geometry-red.txt、packaged-green.txt |
| dotnet test tests/UniClaw.Agent.Dsh.Tests/UniClaw.Agent.Dsh.Tests.csproj --no-restore --filter FullyQualifiedName~RecordedSettingsLayout_FlowsThroughWorldDriverAndDshConsults_AndExportsFormalViewsWhenRequested | 15项实际Slice全部投影，ID/坐标保持 | 修复前期望15项但实际[]；修复后1/1通过 | slice-red.txt、slice-green.txt |
| 设置UNICLAW_WORLD_VIS_RECORDED_EXPORT为changes/WORLD-VISUALIZATION-001/evidence/recorded-world-visualization-source.json的绝对路径，再运行上项命令；python3 tools/world-visualizer/package_examples.py | 正式导出重生成，摘要与实际文件匹配 | recorded Slice及presentation均15项；十个示例摘要均匹配 | export-green.txt、正式源文件（见下） |
| python3 tools/world-visualizer/serve.py；选择recorded.json，切换Owner与Slice | 同样本均显示15项，1080×2400像素比例保持，点击联动来源 | 两种模式均15项；SVG内容区280×622.222，外框含20px边距为300×642.222；桌面选择Network对象联动来源rev-2 | browser-desktop.json、desktop.png |
| 390×844 Chromium设备尺寸模拟 | 无页面横向溢出，最后一个对象可达 | innerWidth390、无溢出、15对象保留；向底部滚动并以DOM派发Enter选择Sound & vibration | browser-narrow.json、narrow.png |
| 受控几何输入：reuse示例只保留首尾帧，将C1尺寸改为1080×2400、C2改为2400×1080，各处相同capture元数据同步调整 | 历史解释仍用C1比例，当前/累计用C2比例 | 原历史SVG高642.222，当前/累计高146；真实正方形reuse高300 | browser-source-checks.json |
| 从recorded示例删除capture.space中的pixelWidth/pixelHeight，仅作前端缺失字段回归 | 不按ID猜造设备比例 | 显示“屏幕宽高比未采集”，15对象仍可读 | browser-source-checks.json |
| 实际scroll.json第3时点累计内容 | 比例修复不破坏已采信内容 | 5个可见对象、1项已采信配准 | browser-source-checks.json |
| SHA256核对入场6份已有dirty记录 | 无覆盖其他工作 | 六项完全一致；src无diff | protected-files.json |

正式源文件：changes/WORLD-VISUALIZATION-001/evidence/recorded-world-visualization-source.json；打包文件：tools/world-visualizer/examples/recorded.json和index.json。本次没有手填修复后的投影对象。

浏览器工具的动作封装会恢复默认视口：桌面截图为实际默认宽1708，已视觉检查；窄窗用CDP尺寸模拟和原生captureScreenshot取得390宽像素，Enter由DOM派发，未将此包装成窄窗真实指针验证。桌面鼠标选择及来源联动已实测。一次窄窗自动点击中断后未用于PASS依据；设备模拟不是手机真机证据。

生成命令用受控provider测试，无真实设备动作。NuGet缓存权限告警未影响目标测试。此次只运行相关Dsh场景与JS回归，没有把旧核心全量验证当作本次新跑。
