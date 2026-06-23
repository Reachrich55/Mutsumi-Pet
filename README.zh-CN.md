<h1 align="center">Mutsumi Pet</h1>
<p align="center">
  <img src="assets/droste.png" alt="Mutsumi Pet" width="520">
</p>
<p align="center">
  <a href="README.md">English</a>
  · 简体中文
</p>

Mutsumi Pet 是一个 WPF 桌面宠物应用。通过 Win32 API 轻量感知电脑使用状态，并调用大语言模型生成气泡台词。

> 当前版本：v1.3

---

## 功能概览

### 1. 桌宠展示

- 支持左键拖动桌宠位置
- 右键菜单可以刷新对话、暂停互动和退出
- 支持对话功能

### 2. 电脑使用状态感知

- `WindowsUsageMonitor` 通过 Win32 API 获取前台窗口和空闲状态
- 当前会感知：
  - 前台进程名
  - 前台窗口标题
  - 应用类型
  - 空闲秒数
  - 当前窗口连续使用时间
  - 最近使用事件
  - Windows 会话锁定状态

### 3. LLM 输入面板与命令

- `LlmClient` 调用 OpenAI兼容API 接口生成气泡台词
- 浮动输入面板支持普通聊天，也支持以 `/` 开头的命令
- 支持 `/专注`、`/结束专注`、`/休息`、`/结束休息`、`/摘要`、`/设置`、`/帮助`
- 普通聊天会携带当前运行期记忆；关闭应用后记忆清空
- prompt 会区分日常互动、消息提醒、普通聊天、专注总结和今日摘要

---

## 快速开始

运行要求：

- Windows 10/11
- .NET 8 SDK
- 一个 OpenAI-compatible chat-completions 接口

### 下载 MutsumiPet.exe

如果你只是想在本地使用，推荐下载 GitHub Release 中已经打包好的 Windows 版本。这种方式不需要克隆源码，也无需安装 .NET 8 SDK。

1. 打开本仓库的 [GitHub Releases](https://github.com/Reachrich55/Mutsumi-Pet/releases/latest) 页面
2. 下载最新版本中的 `MutsumiPet-*-win-x64.zip`
3. 解压到一个本地目录，例如：

```text
D:\Apps\MutsumiPet
```

4. 在解压目录中找到 `.env.example`，复制一份并命名为 `.env`
5. 编辑 `.env`，填入自己的 LLM 配置：

```text
LLM_API_KEY="YOUR_API_KEY"
LLM_BASE_URL="YOUR_BASE_URL"
LLM_MODEL="YOUR_MODEL_NAME"
LLM_TIMEOUT_SECONDS="60"
```

6. 双击运行 `MutsumiPet.exe`

`.env` 必须和 `MutsumiPet.exe` 放在同一个目录中。请不要把自己的 `.env` 或 API Key 上传到公开仓库、截图或 Issue 中。

如果启动后只显示本地备用台词，通常表示 LLM 配置、网络连接或接口权限存在问题。可以先检查 `.env` 文件名是否正确、API Key 是否可用、`LLM_BASE_URL` 是否指向兼容 OpenAI chat-completions 的 `/v1` 地址。

7. 可以在对话窗口输入 `/设置` 打开设置，开启或关闭本地追踪、LLM、消息提醒、窗口标题发送、窗口标题保存、摘要提醒和使用阈值。

### 目录速览：

| 路径 | 说明 |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | WPF 应用入口 |
| `MainWindow.xaml` | 桌宠窗口、角色图和气泡布局 |
| `ChatWindow.xaml` | 浮动输入面板和命令补全 |
| `MainWindow.xaml.cs` | 窗口事件、计时器、拖拽、气泡展示编排 |
| `Services/LlmClient.cs` | LLM 请求、prompt 构造、响应解析 |
| `Services/ChatCommandService.cs` | `/` 命令解析 |
| `Services/ChatConversationService.cs` | 对话窗口交互编排 |
| `Services/ConversationMemoryService.cs` | 当前运行期对话记忆 |
| `Services/WindowsUsageMonitor.cs` | 前台窗口、空闲时长、会话状态监控 |
| `Services/UsageSessionStore.cs` | 本地 SQLite 使用会话存储 |
| `Services/UsageSessionTracker.cs` | 应用会话合并、活跃/空闲分段 |
| `Services/FocusSessionService.cs` | 专注和休息计时 |
| `Services/SettingsService.cs` | 本地隐私与行为设置 |
| `Services/ChatAppMessageMonitor.cs` | 通信软件新消息信号识别 |
| `Services/SpeechQueueService.cs` | 长文本分段和展示时长计算 |
| `Services/ImageTransparencyService.cs` | 角色图片白底透明化处理 |
| `Models/` | 使用状态、触发器、消息信号等模型 |
| `mutsumi.png` | 桌宠角色形象图 |
| `.env.example` | LLM 配置模板 |

源码方式下，`.env` 位于项目根目录。该文件已被 `.gitignore` 忽略，不应提交到版本库。

---

## 配置与运行时数据

### LLM 配置

应用通过 `.env` 或环境变量读取配置。环境变量优先级高于 `.env`。

| 配置项 | 说明 |
| --- | --- |
| `LLM_API_KEY` | LLM API Key |
| `LLM_BASE_URL` | OpenAI-compatible API 基础地址，通常以 `/v1` 结尾 |
| `LLM_MODEL` | 模型名称 |
| `LLM_TIMEOUT_SECONDS` | 请求超时时间，单位秒 |

### 运行时产物

以下内容默认视为本地运行或构建产物，不应提交到 Git：

- `.env`
- `bin/`
- `obj/`
- `artifacts/`
- `.vs/`
- `TestResults/`
- `*.user`
- `*.suo`
- `*.db`
- `*.db-shm`
- `*.db-wal`
- `*.log`

---

## 隐私边界

应用可能会把下列上下文发送给你配置的 LLM：

- 前台进程名
- 前台窗口标题（可在设置中关闭）
- 应用类型
- 空闲时间
- 当前窗口连续使用时间
- 最近使用事件
- 会话锁定状态
- 通信软件新消息信号
- 通信软件显示名
- 消息信号时间
- 对话窗口中的用户输入
- 当前运行期对话记忆摘要
- 本地聚合摘要：活跃时长、空闲时长、主要应用、切换次数

应用不会采集或发送：

- 键盘输入内容
- 屏幕截图
- 通信软件聊天正文
- 通信软件本地数据库
- 网络抓包内容
- 用户电脑中的文件内容

---

## 打包与发布

建议发布自包含 Windows x64 包：

```powershell
dotnet publish .\MutsumiPet.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  -o .\artifacts\github-release\MutsumiPet-win-x64
```
---

## 鸣谢

感谢悠嗖老师绘制的可爱小睦

---

## 许可证

本项目使用 MIT License。详情见 [LICENSE](LICENSE)。
