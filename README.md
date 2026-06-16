# Mutsu Pet

Windows WPF 桌面宠物应用，使用当前目录的 `mutsu.png` 作为角色形象，通过 Win API 感知前台窗口、空闲时长和会话状态，并调用 OpenAI-compatible LLM 生成对话气泡文案。

## 运行要求

- Windows 10/11
- .NET 8 SDK

当前机器只有 .NET 运行时，没有 SDK。安装 SDK 后在项目目录运行：

```powershell
dotnet run
```

## 配置

本地 `.env` 已按当前需求创建，包含：

- `LLM_API_KEY`
- `LLM_BASE_URL`
- `LLM_MODEL`

`.env` 已加入 `.gitignore`，不要提交到版本库。

## 交互

- 左键拖动桌宠。
- 右键菜单刷新对话、暂停互动或退出。
- 自动互动采用轻量提醒策略：启动、空闲返回、会话解锁、连续使用和切换到高专注应用时触发，并带有冷却时间。

## 隐私边界

应用会把前台进程名、窗口标题、空闲秒数、当前窗口连续使用时间和最近事件发送给配置的 LLM。应用不采集键盘输入内容、不截屏、不读取文件内容。
