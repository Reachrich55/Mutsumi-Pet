# Mutsumi Pet v1.4 — 完整源码审查报告

> 生成日期：2026-06-24
> 审查范围：全项目源码（不含 obj/bin/artifacts）
> 审查目标：为个人定制化改造提供依据，不修改任何源文件

---

## 一、项目结构说明

### 1.1 技术栈

| 项目 | 详情 |
|------|------|
| 框架 | .NET 8 (net8.0-windows) |
| UI 框架 | WPF (Windows Presentation Foundation) |
| 语言 | C# 12, XAML |
| NuGet 依赖 | `Microsoft.Data.Sqlite` 8.0.6（本地 SQLite 存储） |
| 测试框架 | xUnit 2.8.1 + Microsoft.NET.Test.Sdk |
| 输出类型 | WinExe（Windows 桌面应用） |
| 启动入口 | `App.xaml.cs` → `OnStartup()` → `new MainWindow().Show()` |

### 1.2 目录与文件清单

```
Mutsumi-Pet-1.4/
├── App.xaml                        # WPF 应用入口声明
├── App.xaml.cs                     # 启动逻辑 + 全局异常处理
├── MainWindow.xaml                 # 桌宠主窗口布局（图片、气泡、按钮）
├── MainWindow.xaml.cs              # 主窗口交互逻辑（拖动、定时器、气泡）
├── ChatWindow.xaml                 # 浮动输入面板布局
├── ChatWindow.xaml.cs              # 输入面板交互逻辑
├── SettingsWindow.xaml             # 隐私设置窗口布局
├── SettingsWindow.xaml.cs          # 设置读写逻辑
├── MutsumiPet.csproj               # 项目文件
├── app.manifest                    # 高DPI感知清单
├── mutsumi.png                     # 角色图片（600×600）
├── assets/
│   ├── droste.png                  # README 展示图
│   └── enter.png                   # 对话按钮图标
├── .env.example                    # LLM 配置模板
├── .env                            # 实际配置（含 API Key，gitignore 中）
├── Models/                         # 数据模型（纯数据类/枚举）
│   ├── AppCategory.cs              # 应用分类枚举
│   ├── ChatCommand.cs
│   ├── ChatCommandKind.cs
│   ├── ChatCommandSuggestion.cs
│   ├── ChatConversationResult.cs
│   ├── FocusSessionSnapshot.cs
│   ├── FocusSessionState.cs
│   ├── InteractionTrigger.cs       # 互动触发条件枚举
│   ├── MessageNotification.cs
│   ├── NotificationSourceKind.cs
│   ├── SpeechSegment.cs
│   ├── UsageAppSummary.cs
│   ├── UsageEventKind.cs
│   ├── UsageSessionRecord.cs
│   ├── UsageSnapshot.cs
│   ├── UsageSummary.cs
│   └── UsageTrackingUpdate.cs
├── Services/                       # 业务逻辑层
│   ├── AppClassifierService.cs     # 进程名→应用类别映射
│   ├── AppSettings.cs              # LLM 配置加载
│   ├── AssetPathResolver.cs        # 资源文件路径查找
│   ├── ChatAppMessageMonitor.cs    # QQ/微信新消息检测
│   ├── ChatCommandService.cs       # 斜杠命令解析
│   ├── ChatConversationService.cs  # 对话窗口编排
│   ├── ConversationMemoryService.cs # 运行时对话记忆
│   ├── DotEnv.cs                   # .env 文件解析
│   ├── FocusSessionService.cs      # 专注/休息状态机
│   ├── ImageTransparencyService.cs # 图片白底透明化
│   ├── LlmClient.cs                # LLM API 请求核心
│   ├── NativeMethods.cs            # Win32 P/Invoke
│   ├── PetInteractionService.cs    # 互动触发编排
│   ├── SettingsService.cs          # 设置文件读写
│   ├── SpeechQueueService.cs       # 文本分段与显示时长
│   ├── UsageSessionStore.cs        # SQLite 会话存储
│   ├── UsageSessionTracker.cs      # 会话合并与分段
│   ├── UsageSummaryService.cs      # 聚合摘要生成
│   ├── UserSettings.cs             # 用户设置模型
│   └── WindowsUsageMonitor.cs      # Win32 前台窗口监控
├── MutsumiPet.Tests/               # 单元测试（6个测试文件）
└── LICENSE                         # MIT License
```

---

## 二、功能与文件对应关系

### 2.1 桌面宠物图片与动画

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| 图片显示 | `MainWindow.xaml` L27-33 | `<Image x:Name="PetImage">` 600×600 拉伸 |
| 图片加载 | `MainWindow.xaml.cs` L288-299 | `LoadPetImage()` → `AssetPathResolver.FindRequired("mutsumi.png")` |
| 白底透明 | `Services/ImageTransparencyService.cs` | Flood-fill 边缘白色→透明 |
| Canvas 画布 | `MainWindow.xaml` L28 | `<Canvas Width="600" Height="600">` 嵌入 Viewbox 中 |
| 窗口缩放 | `MainWindow.xaml` L27 | `<Viewbox Stretch="Fill">` 使内容自适应窗口 |

### 2.2 窗口大小、位置、拖动和右键菜单

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| 窗口大小 | `MainWindow.xaml` L5-6 | `Width="420" Height="420"` |
| 窗口属性 | `MainWindow.xaml` L7-11 | `WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`, `ShowInTaskbar="False"` |
| 初始位置 | `MainWindow.xaml.cs` L304-309 | `PositionNearBottomRight()` — 右下角偏移 24px |
| 拖动 | `MainWindow.xaml.cs` L102-117 | `Window_MouseLeftButtonDown` → `DragMove()` |
| 右键菜单 | `MainWindow.xaml` L18-25 | ContextMenu: 刷新对话 / 暂停互动 / 退出 |

### 2.3 对话气泡样式与显示时间

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| 气泡位置 | `MainWindow.xaml` L35-56 | Canvas.Left=58, Top=80, 宽度238×124 |
| 文本样式 | `MainWindow.xaml` L44-54 | FontSize=21, Microsoft YaHei UI, 颜色 #0B3418 |
| 文本分段 | `Services/SpeechQueueService.cs` | MaxSegmentLength=35, 按标点分割 |
| 翻页计时 | `Services/SpeechQueueService.cs` L137-141 | `CalculateDuration()`: 2.5 + 字符数×0.12 秒 |
| 队列管理 | `MainWindow.xaml.cs` L383-421 | `QueueSpeechText()` / `ShowNextSpeechSegment()` |

### 2.4 LLM API 请求

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| HTTP 调用 | `Services/LlmClient.cs` L34-75 | `GenerateLineAsync()` — POST to `/chat/completions` |
| 聊天回复 | `Services/LlmClient.cs` L80-119 | `GenerateChatReplyAsync()` |
| 配置加载 | `Services/AppSettings.cs` L48-55 | 从 .env 或环境变量读取 |
| .env 解析 | `Services/DotEnv.cs` | KEY=VALUE 格式解析 |

### 2.5 角色人格与提示词

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| System Prompt | `Services/LlmClient.cs` L282-293 | `BuildSystemPrompt()` — 角色定义 "你是 Mutsumi" |
| 任务指令 | `Services/LlmClient.cs` L389-429 | `BuildTaskInstructionBlock()` — 按触发类型区分 |
| 使用上下文 | `Services/LlmClient.cs` L434-457 | `BuildUsageContextBlock()` — Windows 状态 |
| 备用台词 | `Services/PetInteractionService.cs` L530-564 | `GetFallbackLine()` — 离线/LLM 失败时使用 |

### 2.6 主动说话触发条件

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| 触发选择 | `Services/PetInteractionService.cs` L365-394 | `SelectTrigger()` → 事件种类→Trigger |
| 冷却控制 | `Services/PetInteractionService.cs` L399-458 | GlobalCooldown=3min, 各类独立冷却 |
| 轮询定时器 | `MainWindow.xaml.cs` L58-62 | 每 15 秒检查一次 |
| 事件类型 | `Models/UsageEventKind.cs` | Startup/AppSwitch/IdleReturned/ContinuousUse 等 |

### 2.7 前台程序和窗口标题监控

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| Win32 采集 | `Services/WindowsUsageMonitor.cs` L44-95 | `CaptureSnapshot()` |
| 前台窗口 | `Services/NativeMethods.cs` L103 | `GetForegroundWindow()` |
| 窗口标题 | `Services/NativeMethods.cs` L109 | `GetWindowTextW()` |
| 进程名 | `Services/WindowsUsageMonitor.cs` L247-277 | `GetProcessName()` |
| 应用分类 | `Services/AppClassifierService.cs` | 6 个类别白名单 |

### 2.8 聊天窗口与斜杠命令

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| 浮动输入 | `ChatWindow.xaml` + `.cs` | 透明度面板，跟随主窗口 |
| 命令解析 | `Services/ChatCommandService.cs` L59-86 | 7 个命令：/专注 /休息 /摘要 /设置 /帮助 等 |
| 对话编排 | `Services/ChatConversationService.cs` L27-93 | `SubmitAsync()` |

### 2.9 本地记忆、使用记录和隐私设置

| 功能点 | 文件 | 关键位置 |
|--------|------|----------|
| 运行时记忆 | `Services/ConversationMemoryService.cs` | 最多8轮，内存存储，重启清空 |
| SQLite 存储 | `Services/UsageSessionStore.cs` | `%AppData%/MutsumiPet/mutsumi.db` |
| 设置文件 | `Services/SettingsService.cs` | `%AppData%/MutsumiPet/settings.json` |
| 隐私开关 | `Services/UserSettings.cs` | 8 个设置项 |
| 设置 UI | `SettingsWindow.xaml` + `.cs` | 6 个复选框 + 2 个数值输入 |

---

## 三、可个性化项目清单

### 3.1 必改项（硬编码角色名 / 项目名）

| # | 位置 | 当前值 | 建议改为 |
|---|------|--------|----------|
| A1 | `App.xaml.cs:27` | `"Mutsumi Pet"` (异常弹窗标题) | 自定义名 |
| A2 | `MainWindow.xaml:4` | `Title="Mutsumi Pet"` | 自定义窗口标题 |
| A3 | `ChatWindow.xaml:4` | `Title="Mutsumi Input"` | 自定义输入窗口标题 |
| A4 | `ChatWindow.xaml:95` | `"和小睦说点什么吧"` | 自定义占位文本 |
| A5 | `MainWindow.xaml.cs:37` | `"mutsumi.db"` | 自定义数据库名 |
| A6 | `MainWindow.xaml.cs:292` | `"mutsumi.png"` | 自定义图片文件名 |
| A7 | `MainWindow.xaml.cs:404` | `"小睦正在思考..."` | 自定义思考提示 |
| A8 | `Services/LlmClient.cs:286` | `"你是 Mutsumi，…"` | **核心 System Prompt** |
| A9 | `Services/ConversationMemoryService.cs:46` | `"小睦："` | 自定义角色前缀 |
| A10 | `Services/SettingsService.cs:36` | `%AppData%/MutsumiPet` | 自定义数据目录 |
| A11 | `app.manifest:3` | `name="MutsumiPet.app"` | 自定义程序集名 |
| A12 | `MutsumiPet.csproj` | 项目文件名 / 命名空间 | 新建项目时处理 |

### 3.2 强建议改项（人格 / 台词）

| # | 文件 | 关键行 | 说明 |
|---|------|--------|------|
| B1 | `Services/LlmClient.cs:282-293` | `BuildSystemPrompt()` | System prompt 完整重写 |
| B2 | `Services/LlmClient.cs:298-328` | `BuildUserPrompt()` | 用户 prompt 结构微调 |
| B3 | `Services/LlmClient.cs:389-429` | `BuildTaskInstructionBlock()` | 各场景任务指令 |
| B4 | `Services/PetInteractionService.cs:549-564` | `GetFallbackLine()` | 离线时的 15+ 句备用台词 |
| B5 | `Services/PetInteractionService.cs:570-577` | `GetMessageFallbackLine()` | 消息提醒备用台词 |

### 3.3 推荐改项（外观 / 交互）

| # | 文件 | 关键行 | 说明 |
|---|------|--------|------|
| C1 | `MainWindow.xaml:5-6` | Window Width/Height | 窗口大小（当前 420×420） |
| C2 | `MainWindow.xaml:35-41` | Bubble 位置/大小 | Canvas.Left/Top, Width/Height |
| C3 | `MainWindow.xaml:44-54` | 气泡文本样式 | 字体、字号、颜色 |
| C4 | `MainWindow.xaml.cs:307-308` | 初始位置偏移 | 右下角 24px 偏移量 |
| C5 | `Services/SpeechQueueService.cs:7` | `MaxSegmentLength=35` | 每段最大字符数 |
| C6 | `Services/SpeechQueueService.cs:137-141` | 显示时长公式 | `2.5 + chars * 0.12` 秒 |
| C7 | `MainWindow.xaml.cs:60` | 轮询间隔 15 秒 | `TimeSpan.FromSeconds(15)` |
| C8 | `mutsumi.png` | 角色图片 | 替换为新图片（保持 600×600 推荐） |
| C9 | `assets/enter.png` | 对话按钮图标 | 可选替换 |

### 3.4 可选改项（行为 / 配置）

| # | 文件 | 关键行 | 说明 |
|---|------|--------|------|
| D1 | `Services/AppSettings.cs:5` | DefaultBaseUrl | 默认 API 地址（阿里云百炼） |
| D2 | `Services/AppSettings.cs:6` | DefaultModel | 默认模型名（qwen3.7-plus） |
| D3 | `Services/PetInteractionService.cs:8` | `GlobalInteractionCooldown=3min` | 全局冷却时间 |
| D4 | `Services/PetInteractionService.cs:437-458` | 各类独立冷却 | 16 种触发器冷却时间 |
| D5 | `Services/AppClassifierService.cs:7-85` | 进程名白名单 | 6 类应用的识别列表 |
| D6 | `Services/ChatAppMessageMonitor.cs:12-24` | QQ/微信进程名 | 消息监控目标列表 |
| D7 | `Services/UserSettings.cs` | 默认设置值 | IdleThreshold(180s), ContinuousUse(45min) |
| D8 | `Services/LlmClient.cs:188-189` | Chat body temp/tokens | Chat 请求的 temperature=0.76, max_tokens=600 |

---

## 四、推荐修改顺序

按「成本最低 → 最核心」排列，每一步都可独立验证：

```
第 1 步：外观快速改造（10 分钟）
  └─ 替换 mutsumi.png → 新角色图
  └─ 替换 assets/enter.png → 新按钮图标
  └─ 调整 MainWindow.xaml 窗口大小、气泡颜色/字体

第 2 步：名字与品牌替换（15 分钟）
  └─ 修改 A1-A12 中所有硬编码的 "Mutsumi" / "小睦"
  └─ 修改 .csproj 命名空间或新建项目
  └─ 修改 app.manifest

第 3 步：人格重塑（20 分钟）
  └─ 重写 LlmClient.BuildSystemPrompt()
  └─ 重写 PetInteractionService.GetFallbackLine() 所有备用台词
  └─ 重写 GetMessageFallbackLine()
  └─ 调整 BuildTaskInstructionBlock() 任务指令描述

第 4 步：交互调整（20 分钟）
  └─ 调整冷却时间、轮询间隔
  └─ 调整气泡显示时长和分段长度
  └─ 调整窗口初始位置策略
  └─ 调整 API 默认值（如果需要）

第 5 步：隐私与监控调整（15 分钟）
  └─ 修改默认隐私设置
  └─ 增加/减少监控的进程白名单
  └─ 添加/移除聊天软件监控目标

第 6 步：编译与测试（10 分钟）
  └─ dotnet build
  └─ dotnet test（如有测试）
  └─ 运行验证
```

---

## 五、每项修改的具体文件、类和方法

### 第 1 步：外观改造

```
替换角色图：
  文件: 项目根目录 mutsumi.png
  引用: MainWindow.xaml.cs:292 AssetPathResolver.FindRequired("mutsumi.png")
  建议: 保持 PNG 格式，600×600 像素，白色背景（程序自动透明化）

替换按钮图标：
  文件: assets/enter.png
  引用: MainWindow.xaml:67 <Image Source="assets/enter.png">

调整窗口大小：
  文件: MainWindow.xaml:5-6
  当前: Width="420" Height="420"
  
调整气泡位置：
  文件: MainWindow.xaml:35-41
  当前: Canvas.Left="58" Canvas.Top="80" Width="238" Height="124"

调整气泡文本样式：
  文件: MainWindow.xaml:44-54
  当前: FontFamily="Microsoft YaHei UI" FontSize="21" Foreground="#0B3418"
```

### 第 2 步：名字与品牌

每一个硬编码位置的详细说明：

```
A1) App.xaml.cs:27
    当前: "Mutsumi Pet"
    方法: OnDispatcherUnhandledException()
    说明: 异常弹窗标题

A2) MainWindow.xaml:4
    当前: Title="Mutsumi Pet"

A3) ChatWindow.xaml:4
    当前: Title="Mutsumi Input"

A4) ChatWindow.xaml:95
    当前: Text="和小睦说点什么吧"
    
A5) MainWindow.xaml.cs:37
    当前: Path.Combine(SettingsService.AppDataDirectory, "mutsumi.db")
    方法: MainWindow() 构造函数

A6) MainWindow.xaml.cs:292
    当前: AssetPathResolver.FindRequired("mutsumi.png")
    方法: LoadPetImage()

A7) MainWindow.xaml.cs:404
    当前: SpeechText.Text = "小睦正在思考...";
    方法: ShowThinkingBubble()

A8) Services/LlmClient.cs:286
    当前: "你是 Mutsumi，一只运行在 Windows 桌面上的陪伴型桌宠。"
    方法: BuildSystemPrompt()

A9) Services/ConversationMemoryService.cs:46
    当前: builder.Append("小睦：").Append(assistant).AppendLine();
    方法: BuildPromptMemory()

A10) Services/SettingsService.cs:36
     当前: Path.Combine(..., "MutsumiPet")
     方法: AppDataDirectory (static property)

A11) app.manifest:3
     当前: name="MutsumiPet.app"
```

### 第 3 步：人格重塑

```
System Prompt 完整位置:
  文件: Services/LlmClient.cs
  方法: BuildSystemPrompt() (L282-293)
  当前内容: 7 行中文，定义角色、安全边界、隐私、风格、格式
  包含: "你是 Mutsumi" 硬编码
  
用户 Prompt 结构 (按场景):
  文件: Services/LlmClient.cs
  方法: BuildUserPrompt() (L298-329)
  包含: 任务指令块 + 上下文块 + 最终自检
  
  方法: BuildTaskInstructionBlock() (L389-429)
  包含: 3 种场景模板
    - 消息提醒: "类型：聊天软件新消息提醒。" (L397-404)
    - 摘要/专注结束: "类型：{trigger}。" (L407-418)
    - 日常互动: "类型：{trigger}。" (L420-429)

备用台词 (共 18+ 句):
  文件: Services/PetInteractionService.cs
  方法: GetFallbackLine() (L530-564)
  - "我醒啦，今天也慢慢来。"
  - "欢迎回来，刚才休息得还好吗？"
  - "お帰りなさい"
  - "已经专注很久啦，眼睛也需要休息。"
  - ... (共 16 个触发分支)

  方法: GetMessageFallbackLine() (L570-577)
  - "QQ 有新消息。..."
  - "微信有新消息。..."
```

### 第 4 步：交互参数

```
冷却时间调整:
  文件: Services/PetInteractionService.cs
  全局冷却: L8 const TimeSpan GlobalInteractionCooldown = 3min
  各类独立冷却: 方法 GetCooldown() (L437-458)
    高专注应用: 10min, 连续使用: 30min, 启动: 5min, 消息: 20s

轮询间隔:
  文件: MainWindow.xaml.cs:60
  当前: TimeSpan.FromSeconds(15)

气泡分段:
  文件: Services/SpeechQueueService.cs:7
  当前: MaxSegmentLength = 35

LLM 参数:
  文件: Services/LlmClient.cs
  Chat 对话: temperature=0.76, max_tokens=600 (L188-189)
  主动说话: temperature 0.72~0.78, max_tokens 360~800 (L544-564)
```

### 第 5 步：进程白名单与隐私默认值

```
应用分类:
  文件: Services/AppClassifierService.cs
  6 个 HashSet: FocusProcesses, CommunicationProcesses, BrowserProcesses,
                MediaProcesses, GameProcesses, SystemProcesses

聊天消息监控:
  文件: Services/ChatAppMessageMonitor.cs:12-24
  QqProcessNames: "QQ","QQNT","TIM"
  WeChatProcessNames: "WeChat","Weixin","WeChatAppEx"

默认隐私设置:
  文件: Services/UserSettings.cs
  EnableTracking = true
  EnableLlm = true
  EnableMessageReminders = true
  SendWindowTitleToLlm = true   ← 注意：默认会发送窗口标题
  StoreWindowTitles = false
  EnableUsageSummary = true
  IdleThresholdSeconds = 180
  ContinuousUseMinutes = 45
```

---

## 六、可能的风险

### 6.1 高风险

| 风险 | 说明 | 缓解措施 |
|------|------|----------|
| **API Key 泄漏** | `.env` 文件包含真实 API Key (`sk-a9f00...`)，虽在 .gitignore 中但仍存在于本地 | 确认仓库中未提交，考虑使用系统环境变量 |
| **默认 API 地址** | 硬编码指向阿里云百炼北京节点 (`token-plan.cn-beijing.maas.aliyuncs.com`)，换用其他厂商需改代码 | 通过 .env 覆盖即可，但无 .env 时会 fallback 到该地址 |
| **窗口标题默认发送** | `SendWindowTitleToLlm` 默认为 `true`，会向前台窗口标题发送到 LLM | 建议首次启动提示用户配置，或默认改为 false |

### 6.2 中风险

| 风险 | 说明 |
|------|------|
| 命名空间改动 | 修改 `namespace MutsumiPet` 需要同步更新所有 .cs 和 .xaml 文件 |
| 数据库路径 | 改数据库名后旧数据不可见，需迁移或告知用户 |
| NuGet 兼容性 | Microsoft.Data.Sqlite 8.0.6 锁定，升级 .NET 版本需同步升级 |

### 6.3 低风险

| 风险 | 说明 |
|------|------|
| 图片尺寸 | 替换图片尺寸不同时，Canvas/Viewbox 布局可能需要调整 |
| 字体替换 | Microsoft YaHei UI 是 Windows 内置字体，替换需确认目标系统可用 |
| 测试关联 | 修改命名空间会影响 test project 引用 |

---

## 七、第一阶段最小改造方案

如果只想快速拥有一个可运行的个性化版本，推荐按以下顺序完成**最小 5 步**（预计 30 分钟）：

### Step 1: 换图 (5 min)
```
1. 准备一张 600×600 的 PNG 角色图（白色背景最佳）
2. 替换 mutsumi.png
3. 替换 assets/enter.png（可选）
```

### Step 2: 改名 (10 min)
```
修改以下 5 个文件中的 8 处文本（不用改命名空间）:

MainWindow.xaml:4          Title="Mutsumi Pet"       → 你的宠物名
ChatWindow.xaml:4          Title="Mutsumi Input"     → 你的输入窗口名
ChatWindow.xaml:95         "和小睦说点什么吧"          → 你的提示语
MainWindow.xaml.cs:404     "小睦正在思考..."           → "正在思考..."
App.xaml.cs:27             "Mutsumi Pet"             → 你的应用名
Services/LlmClient.cs:286  "你是 Mutsumi，..."        → 你的角色定义
Services/ConversationMemoryService.cs:46  "小睦："     → "你的角色名："
Services/SettingsService.cs:36  "MutsumiPet"          → 你的应用数据目录名
```

### Step 3: 改人格 (5 min)
```
修改 Services/LlmClient.cs BuildSystemPrompt() 方法和
Services/PetInteractionService.cs GetFallbackLine() 方法
```

### Step 4: 调窗口外观 (5 min)
```
MainWindow.xaml:5-6        窗口大小
MainWindow.xaml:44-54      气泡文字颜色/字体/字号
```

### Step 5: 编译验证 (5 min)
```
dotnet build
dotnet run
验证：图片显示、名字、气泡文字、LLM 对话
```

完成以上 5 步即可拥有一个**外观和人格完全不同的桌面宠物**，且保持了与原项目的向后兼容。

---

## 八、特别检查结果

### 8.1 硬编码角色名

**确认存在**。共发现 12 处 "Mutsumi" / "小睦" 硬编码（详见第三部分 A1-A12）。

### 8.2 硬编码图片路径

**确认存在**。`mutsumi.png` 和 `assets/enter.png` 在代码中以**字符串常量**形式引用，非配置文件。

### 8.3 硬编码人格提示词和台词

**确认存在**。核心 System Prompt 在 `LlmClient.BuildSystemPrompt()` 中完整硬编码。备用台词 18 句在 `PetInteractionService.GetFallbackLine()` / `GetMessageFallbackLine()` 中硬编码。

### 8.4 API 地址 / 模型名称 / 密钥

| 项目 | 文件 | 状态 |
|------|------|------|
| 默认 API 地址 | `AppSettings.cs:5` | ⚠️ 硬编码阿里云百炼地址 |
| 默认模型名称 | `AppSettings.cs:6` | ⚠️ 硬编码 `qwen3.7-plus` |
| API Key | `.env` (本地) | ⚠️ 真实的 API Key 存在于磁盘 |
| API Key 传输 | `LlmClient.cs:50` | `Authorization: Bearer {key}` 通过 HTTPS |
| API Key 存储 | `.env` 文件 → `.gitignore` 已包含 | ✅ 不会被提交到 git |

**结论**：API Key 未被硬编码到源码中，使用 .env 加载。但 `.env` 文件本身含有真实 key，需确认从未被 git commit。默认 API 地址和模型名在代码中硬编码。

### 8.5 隐私信息采集逻辑

| 采集数据 | 文件 | 是否发送到 LLM | 用户可控 |
|----------|------|----------------|----------|
| 前台进程名 | `WindowsUsageMonitor.cs:80` | ✅ 是 | ❌ 始终发送 |
| 前台窗口标题 | `WindowsUsageMonitor.cs:86` | ✅ 是 | ✅ `SendWindowTitleToLlm` 设置 |
| 空闲秒数 | `WindowsUsageMonitor.cs:56` | ✅ 是 | ❌ 始终发送 |
| 应用类别 | `AppClassifierService.cs` | ✅ 是 | ❌ 始终发送 |
| 连续使用时长 | `WindowsUsageMonitor.cs:70` | ✅ 是 | ❌ 始终发送 |
| 会话锁定状态 | `WindowsUsageMonitor.cs:93` | ✅ 是 | ❌ 始终发送 |
| 窗口标题存本地 | `UsageSessionTracker.cs:180` | ❌ 本地 SQLite | ✅ `StoreWindowTitles` 设置 |
| QQ/微信新消息 | `ChatAppMessageMonitor.cs` | ✅ 是 | ✅ `EnableMessageReminders` 设置 |
| 聊天消息正文 | — | ❌ 不采集 | — |
| 键盘输入 | — | ❌ 不采集 | — |

**注意**：即使关闭 `SendWindowTitleToLlm`，进程名和窗口标题仍会被采集（只是不发送给 LLM）。关闭 `EnableTracking` 可停止本地 SQLite 记录。

---

## 九、项目编译验证

项目结构完整，使用标准 .NET 8 WPF 模板，理论上可直接编译：

```bash
# 在项目根目录执行
dotnet restore
dotnet build
dotnet test   # 运行 6 个单元测试
```

唯一 NuGet 依赖 `Microsoft.Data.Sqlite 8.0.6` 为纯托管库，无平台依赖。测试项目额外引用 xUnit，无集成测试，不依赖特定环境。

---

*报告完毕。请审阅后告知需要修改哪些内容。*
