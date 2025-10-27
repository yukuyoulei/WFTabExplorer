# 多标签资源管理器 (MultiTabExplorer)

一个基于 Windows Forms 的多标签文件资源管理器，支持多标签浏览、面包屑导航和自动点击功能。

## ✨ 功能特性

### 📁 文件管理
- **多标签浏览**：同时打开多个文件夹标签页，轻松切换
- **面包屑导航**：清晰的路径导航栏，快速跳转到上级目录
- **驱动器概览**：左侧面板实时展示系统中各驱动器的容量和可用空间，点击即可跳转
- **路径保存**：自动保存打开的标签页路径，下次启动自动恢复
- **快速导航**：
  - 支持上级目录跳转
  - 文件夹浏览对话框
  - 地址栏直接输入路径（Alt+D 激活）

### 🖱️ 自动点击
- **可配置频率**：设置每秒点击次数（Hz）
- **热键控制**：自定义热键快速启动/停止自动点击（默认 F8）
- **后台运行**：自动点击在后台持续执行

### ⌨️ 快捷键支持
- `Ctrl+T`：新建标签页
- `Ctrl+W`：关闭当前标签页
- `Ctrl+F4`：关闭当前标签页
- `Alt+D`：激活地址栏编辑模式
- `Ctrl+N`：在当前目录新建文件夹
- `F5`：刷新当前视图
- `自定义热键`：控制自动点击开关（默认 F8）

## 🛠️ 技术栈

- **框架**：.NET 8.0 (Windows)
- **UI**：Windows Forms
- **浏览器控件**：WebBrowser 组件（原生文件浏览体验）
- **配置持久化**：JSON 文件存储

## 📋 系统要求

- **操作系统**：Windows 10 或更高版本
- **.NET 运行时**：.NET 8.0 Desktop Runtime
- **架构**：x64

## 🚀 快速开始

### 编译项目

```bash
# 克隆仓库
git clone <repository-url>
cd MultiTabExplorer

# 编译项目
dotnet build src/MultiTabExplorer/MultiTabExplorer.csproj -c Release

# 运行程序
dotnet run --project src/MultiTabExplorer/MultiTabExplorer.csproj
```

### 发布单文件可执行程序

```bash
dotnet publish src/MultiTabExplorer/MultiTabExplorer.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

生成的可执行文件位于：`src/MultiTabExplorer/bin/Release/net8.0-windows/win-x64/publish/`

## 📖 使用说明

### 基本操作

1. **打开文件夹**：
   - 点击左侧驱动器列表中的任意驱动器快速进入
   - 点击"浏览"按钮选择文件夹
   - 双击文件夹项进入子目录
   - 点击面包屑导航快速跳转

2. **标签管理**：
   - 点击"新建标签"或按 `Ctrl+T` 创建新标签
   - 点击"关闭标签"或按 `Ctrl+W` 关闭当前标签
   - 标签页会自动保存，重启后恢复

3. **地址栏编辑**：
   - 按 `Alt+D` 激活地址栏
   - 输入路径后按 `Enter` 跳转
   - 按 `Esc` 取消编辑

### 自动点击功能

1. **设置频率**：在底部工具栏输入点击频率（Hz），如 10 表示每秒点击 10 次
2. **设置热键**：
   - 点击"设置热键"按钮
   - 在弹窗确认后，按下想要设置的按键
   - 热键会自动保存
3. **启动/停止**：
   - 点击底部工具栏的"开始"按钮
   - 或按下设置的热键（默认 F8）

## 📂 项目结构

```
MultiTabExplorer/
├── src/
│   └── MultiTabExplorer/
│       ├── Program.cs               # 程序入口
│       ├── MainForm.cs              # 主窗体（工具栏、标签管理、自动点击、驱动器列表）
│       ├── ExplorerTab.cs           # 单个资源管理器标签页
│       ├── DriveStatusControl.cs    # 驱动器状态显示控件
│       ├── Persistence.cs           # 配置持久化服务
│       ├── NativeIcons.cs           # Windows 原生图标支持
│       ├── app.manifest             # 应用清单文件
│       └── MultiTabExplorer.csproj
├── MultiTabExplorer.sln
├── .gitignore
└── README.md
```

## ⚙️ 配置文件

配置文件自动保存在：`%APPDATA%\MultiTabExplorer\config.json`

示例配置：
```json
{
  "SavedPaths": [
    "C:\\Users\\YourName\\Documents",
    "C:\\Projects"
  ],
  "ClickFrequency": 10,
  "HotKey": "F8"
}
```

### 配置项说明
- `SavedPaths`：保存的标签页路径列表
- `ClickFrequency`：自动点击频率（Hz）
- `HotKey`：自动点击热键（如 "F8", "F9" 等）

## 🔧 开发说明

### 核心组件

- **MainForm**：主窗体，负责工具栏、标签管理、自动点击功能
- **ExplorerTab**：单个文件浏览器标签，使用 WebBrowser 控件
- **ConfigService**：配置文件读写服务
- **NativeIcons**：调用 Windows API 获取文件/文件夹图标

### 关键特性实现

- **面包屑导航**：动态生成 ToolStripButton，监听路径变化
- **自动点击**：使用 `SendInput` Windows API 模拟鼠标点击
- **热键注册**：使用 `RegisterHotKey` / `UnregisterHotKey` API
- **标签持久化**：启动时恢复上次打开的所有标签

## 📝 许可证

本项目使用 MIT 许可证。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📮 反馈

如有问题或建议，请提交 Issue。

---

**注意**：本程序仅适用于 Windows 平台，依赖 Windows Forms 和部分 Windows API。
