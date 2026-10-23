# 浏览器验收中的失败与处置
- ego TaskSpace 7 的 DOM/真实交互可用，但 Page.captureScreenshot（默认与 fromSurface=false）均 CDP timeout。未声称截图通过；改用 Codex IAB 对相同 localhost 页面做实际截图/视觉检查，保留独立来源。
- IAB 首轮滚动范围验收：切换到 child 时点仍保留旧 root 选择，导致真实 Slice 列表为空。根因 chooseFrame 只在旧 Container 不存在时更新选择。修正为每次时间推进自动跟随该时点实际 currentPageObservation.ContainerId；仍允许手动选择上下文。重跑此失败交互并记录。

- 首轮导出扩展读取无Assurance fixture的PostActionVerifications导致6失败；未改产品，改测试导出器在RunView不存在时保留未采集null，最终37/37通过。
- 两次UI检查脚本假设下半区必有控件、累计必有仅历史项，实际capture-2的全部控件位于上区且均在当前观察，故修正为capture-1上下范围内容与实际registration refs检查，不改变Owner素材。
- Ego播放暂停时序脚本未通过；IAB立即播放/暂停、推进后暂停均实际通过。局部初稿JSON为空，正式browser-checks-1/2/3及boundary记录为有效验收证据。
