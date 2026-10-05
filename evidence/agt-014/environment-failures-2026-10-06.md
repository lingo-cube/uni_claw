# 环境失败与修复证据

## 首轮观察

- `adb devices` 曾出现 `emulator-5556 offline`，随后设备消失。
- 复用同一个 `p26_pixel` AVD 时，emulator 日志出现多实例锁冲突；原因是
  克隆实例未以 `-read-only` 启动。
- 设备刚完成 boot 时，`uiautomator dump` 偶发不可用。
- DSH 入口在设备离线时于首个 `RunAdb` 停止，未进入 Agent/Kernel。

## 局部修复

- `tools/android/start-test-device.sh` 使用 `-read-only` 和 `nohup`，并在
  shell 退出后保持 emulator 进程。
- `AdbEnvironmentTests` 对 uiautomator dump 做 4 次、每次 750ms 的有界重试。
- `Host.Dsh/Program.RunAdb` 对 10 秒启动等待超时终止整个进程树，输出
  `ENVIRONMENT_UNAVAILABLE` 和设备号。

## 修复后复验

修复后的同一 API 35 emulator 完成：typed live 1/1、Host full 1/1、坐标门
2/2、ADB environment 1/1、Settings coverage 1/1。对应输出在 `live/`。
