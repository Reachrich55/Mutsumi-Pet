# Mutsumi Pet 第一阶段改造总结

> 改造日期：2026-06-24  
> 基础版本：v1.4  
> 目标：让程序成为可通过双击启动、在图形界面中完成 API 配置的独立 Windows 应用

---

## 一、改造目标完成情况

| 优先级 | 目标 | 状态 |
|--------|------|------|
| P1 | 双击启动和桌面快捷方式 | ✅ 完成 |
| P2 | 扩展设置窗口（API 配置 + 加密存储） | ✅ 完成 |
| P3 | 人物交互入口优化（菜单 / 双击 / 拖动） | ✅ 完成 |
| P4 | 开机自启 | ✅ 完成 |
| — | 首次启动体验 | ✅ 完成 |
| — | 安全审查 | ✅ 完成 |

---

## 二、文件变更清单

### 新增文件（5 个）

| 文件 | 用途 |
|------|------|
| `Services/ApiKeyProtection.cs` | Windows DPAPI 加密/解密，`DataProtectionScope.CurrentUser` |
| `Services/ApiConnectionTester.cs` | LLM 连接测试，6 种结果：成功/Key无效/不可达/模型不存在/超时/格式不兼容 |
| `Services/StartupService.cs` | 注册表 `HKCU\...\Run` 管理开机自启，自动检测开发模式 |
| `创建桌面快捷方式.ps1` | 为当前目录的 `MutsumiPet.exe` 创建桌面快捷方式 |
| `创建桌面快捷方式.bat` | 双击调用 PowerShell 脚本 |

### 修改文件（9 个）

| 文件 | 变更要点 |
|------|----------|
| `Services/AppSettings.cs` | 改为可变对象；新增 `Reload()`；配置优先级：**settings.json → 环境变量 → .env → 默认值** |
| `Services/UserSettings.cs` | 新增 `ApiBaseUrl`, `ApiModel`, `ApiTimeoutSeconds`, `ApiKeyEncrypted`, `EnableAutoStart` |
| `Services/SettingsService.cs` | `Save()` 新增可选参数 `plaintextApiKey`，保存时自动 DPAPI 加密 |
| `Services/LlmClient.cs` | 新增 `SyncTimeout()`，每次请求前从 `AppSettings` 同步超时时间 |
| `SettingsWindow.xaml` | 完全重新设计：**TabControl 4 标签页** |
| `SettingsWindow.xaml.cs` | 完全重写：加密保存、API 连接测试、开机自启管理 |
| `MainWindow.xaml` | 右键菜单重排序；增加 `MouseMove` / `MouseLeftButtonUp` 事件支持双击与拖动互斥 |
| `MainWindow.xaml.cs` | 双击打开聊天；拖动只在实际移动后触发；设置窗口单例；首次无 API Key 时自动弹出设置 |
| `MutsumiPet.csproj` | 添加快捷方式脚本到发布输出 |

### 未修改（按约束保留）

角色图片、角色名、人格提示词、备用台词、数据库名、项目命名空间、使用监控逻辑、气泡视觉样式。

---

## 三、配置存储架构

### 存储位置

```
%AppData%\MutsumiPet\
├── settings.json    ← API 配置 + 隐私 + 行为 + 启动设置
└── mutsumi.db       ← 使用会话记录（原有）
```

### settings.json 结构

```json
{
  "ApiBaseUrl": "https://api.example.com/v1",
  "ApiModel": "model-name",
  "ApiTimeoutSeconds": 60,
  "ApiKeyEncrypted": "<Base64 DPAPI ciphertext>",
  "EnableTracking": true,
  "EnableLlm": true,
  "EnableMessageReminders": true,
  "SendWindowTitleToLlm": true,
  "StoreWindowTitles": false,
  "EnableUsageSummary": true,
  "EnableAutoStart": false,
  "IdleThresholdSeconds": 180,
  "ContinuousUseMinutes": 45
}
```

### 配置优先级（从高到低）

1. **settings.json** — 用户通过设置窗口保存的配置
2. **环境变量** — `LLM_API_KEY`, `LLM_BASE_URL`, `LLM_MODEL`, `LLM_TIMEOUT_SECONDS`
3. **.env 文件** — 项目目录下（向后兼容）
4. **代码默认值** — Base URL / Model / 60s 超时

### API Key 加密方案

| 项目 | 详情 |
|------|------|
| 加密算法 | Windows DPAPI (`ProtectedData.Protect`) |
| 作用域 | `DataProtectionScope.CurrentUser` — 仅当前 Windows 用户可解密 |
| 熵值 | `"MutsumiPet.ApiKey.v1"` |
| 存储格式 | Base64 字符串 |
| 日志安全 | 打码函数 `ApiKeyProtection.Mask()` — 仅显示前 4 + 后 4 位 |
| 测试连接 | 不输出完整 Key，仅显示分类结果 |

---

## 四、设置窗口布局

```
┌─ 设置 ──────────────────────────────────────┐
│ [模型服务] [隐私与监控] [交互行为] [启动设置] │
│                                               │
│  Tab 1: 模型服务                              │
│  ├─ API Base URL 输入框                       │
│  ├─ API Key 输入框 + [显示] 复选框            │
│  ├─ 模型名称 输入框                           │
│  ├─ 请求超时（秒）输入框                      │
│  └─ [测试连接] 按钮 + 结果文本                │
│                                               │
│  Tab 2: 隐私与监控                            │
│  ├─ ☑ 记录本地使用时间线                     │
│  ├─ ☑ 启用 LLM 台词生成                      │
│  ├─ ☑ 启用 QQ/微信消息提醒                   │
│  ├─ ☑ 允许窗口标题进入 LLM 上下文            │
│  ├─ ☑ 保存窗口标题到本地数据库               │
│  └─ ☑ 启用使用摘要                           │
│                                               │
│  Tab 3: 交互行为                              │
│  ├─ 空闲阈值（秒）                            │
│  └─ 连续使用提醒（分钟）                      │
│                                               │
│  Tab 4: 启动设置                              │
│  └─ ☑ Windows 登录后自动启动                 │
│                                               │
│                              [取消]  [保存]    │
└───────────────────────────────────────────────┘
```

---

## 五、交互入口变更

### 右键菜单顺序（新）

```
打开聊天
─────────
刷新对话
设置
暂停互动  ☑/☐
─────────
退出
```

- "设置" 打开设置窗口（单例，重复点击激活已有窗口）
- `/设置` 命令同样打开这个窗口

### 双击与拖动

| 操作 | 行为 |
|------|------|
| 单击不动 | 无操作 |
| 单击 + 拖动 | 移动桌宠窗口（原逻辑保留） |
| 双击（400ms 内） | 打开聊天窗口 |
| 双击 + 拖动 | 互斥，不会误触 |

实现方式：`MouseLeftButtonDown` 记录起始位置 + 时间戳，`MouseMove` 超过系统拖动阈值（约 4px）后才调用 `DragMove()`。400ms 内连续两次单击且未移动 → 双击 → 打开聊天。

---

## 六、开机自启

| 项目 | 详情 |
|------|------|
| 注册表路径 | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| 键名 | `MutsumiPet` |
| 键值 | 发布版 exe 的完整路径（含空格时用引号包裹） |
| 启用 | 勾选 → 检查是否为发布版 → 创建/更新注册表项 |
| 禁用 | 取消勾选 → 删除注册表项 |
| 开发模式 | `dotnet run` 检测到 `dotnet.exe` → 禁用复选框，红色提示 |
| 权限 | 无需管理员 |

---

## 七、API 连接测试

| 测试结果 | 触发条件 |
|----------|----------|
| ✅ 连接成功 | HTTP 200 + `choices[0].message.content` 存在 |
| ❌ Key 无效 | HTTP 401 / 403 |
| ❌ 地址不可达 | DNS 失败 / 连接拒绝 / `HttpRequestException` |
| ❌ 模型不存在 | HTTP 404 |
| ❌ 请求超时 | `TaskCanceledException`（非用户取消） |
| ❌ 格式不兼容 | HTTP 200 但 JSON 结构不符合 OpenAI 规范 |

- 使用当前输入框中的值（不读取已保存配置）
- 发送 `{"messages":[{"role":"user","content":"ping"}],"max_tokens":1}`
- 不修改已保存的配置，只有点"保存"才写入
- 失败时显示中文说明，不显示原始响应头和完整 Key

---

## 八、首次启动体验

```
启动 MutsumiPet.exe
  │
  ├─ AppSettings.Load() → 检查 API Key 是否可用
  │
  ├─ 有 Key → 正常启动，桌宠显示在右下角
  │
  └─ 无 Key → 正常启动 +
              自动弹出设置窗口（模型服务标签页）+
              提示可填写 API 或暂时跳过
                 │
                 ├─ 填写并保存 → 下次启动有 Key
                 └─ 跳过 → 使用本地备用台词
```

---

## 九、构建与发布

### 编译

```bash
dotnet build -c Release
# → 0 warnings, 0 errors
```

### 测试

```bash
dotnet test
# → 23 passed, 0 failed, 0 skipped
```

### 发布

```bash
dotnet publish MutsumiPet.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o artifacts/personal-release/MutsumiPet
```

### 发布产物

```
artifacts/personal-release/MutsumiPet/
├── MutsumiPet.exe           ← 双击启动
├── MutsumiPet.dll
├── mutsumi.png              ← 角色图片
├── .env                     ← LLM 配置模板
├── assets/
│   └── enter.png            ← 对话按钮图标
├── 创建桌面快捷方式.ps1      ← PowerShell 脚本
├── 创建桌面快捷方式.bat      ← 可双击运行
├── *.dll                    ← .NET 运行时和依赖
└── (SQLite 依赖)
```

---

## 十、安全性

| 检查项 | 结果 |
|--------|------|
| `.env` 在 `.gitignore` | ✅ 第 8 行已忽略 |
| 当前是否为 Git 仓库 | ❌ 不是（无 `.git` 目录） |
| Git 历史中是否有 Key | ✅ 无历史记录 |
| API Key 明文存储 | ❌ 仅 `.env`（本地），`settings.json` 中 DPAPI 加密 |
| 窗口标题默认发送到 LLM | ⚠️ `SendWindowTitleToLlm = true`（用户可在设置中关闭） |

---

## 十一、发布后使用步骤

1. 将 `artifacts/personal-release/MutsumiPet/` 整个目录复制到目标位置（如 `D:\Apps\MutsumiPet`）
2. 双击 `创建桌面快捷方式.bat` 生成桌面图标
3. 双击桌面 `Mutsumi Pet` 快捷方式启动
4. 首次启动会自动打开设置窗口，填写 API 配置
5. 在设置窗口：
   - 填写 API Base URL、API Key、模型名称
   - 点击"测试连接"验证
   - 点击"保存"
6. 如果希望开机自启，切换到"启动设置"标签页勾选
7. 右键桌宠可访问聊天、刷新、设置、暂停、退出功能
