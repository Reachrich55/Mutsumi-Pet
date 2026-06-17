# Mutsu Pet

Windows WPF 桌面宠物应用，使用当前目录的 `mutsu.png` 作为角色形象，通过 Win API 感知前台窗口、空闲时长和会话状态，并调用 OpenAI-compatible LLM 生成对话气泡文案。

## 运行要求

- Windows 10/11
- .NET 8 SDK

安装 SDK 后在项目目录运行：

```powershell
dotnet run
```

## 配置

本地 `.env` 已按当前需求创建，包含：

- `LLM_API_KEY`
- `LLM_BASE_URL`
- `LLM_MODEL`
- `LLM_TIMEOUT_SECONDS`

`.env` 已加入 `.gitignore`，不要提交到版本库。

## 交互

- 左键拖动桌宠。
- 右键菜单刷新对话、暂停互动或退出。
- 自动互动采用轻量提醒策略：启动、空闲返回、会话解锁、连续使用和切换到高专注应用时触发，并带有冷却时间。
- LLM 长回复会按标点自动切成多段，并按阅读时间在气泡中依次展示。

## 消息提醒

- 消息提醒不再依赖 Windows toast、MSIX 包身份或通知读取权限。
- 应用启动后会通过 Win32 窗口事件、任务栏闪烁 Shell hook 和周期补扫观察 QQ/微信桌面客户端。
- 当前只提醒“有新消息”，不会读取聊天正文、不会访问 QQ/微信本地数据库、不会注入进程或抓包。
- 如果 QQ/微信没有暴露窗口标题、未读状态或提示窗口，桌宠可能无法识别该次消息。

## 发布

普通发布可以直接使用 `dotnet publish`：

```powershell
dotnet publish .\MutsuPet.csproj -c Release -r win-x64 --self-contained false
```

## 隐私边界

