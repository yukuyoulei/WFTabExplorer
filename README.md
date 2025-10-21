Multi-Tab File Explorer (WinForms)

Overview
- A Windows Forms application that provides a multi-tab file explorer using native Windows controls (no custom drawing).
- You can store multiple folder paths and switch between them at any time via a Saved Paths list.

Features
- Native Windows UI: TabControl, ListView (Details view), standard FolderBrowserDialog, no owner-draw/custom-draw.
- Multiple tabs: open different folders in each tab.
- Saved paths: persist a list of frequently used folders under %APPDATA%/MultiTabExplorer/config.json.
- Basic navigation: address bar, browse dialog, and "Up" to parent folder.
- Double-click folders to navigate; double-click files to open with default app (Shell execute).

Project Structure
- src/MultiTabExplorer
  - Program.cs: App entry point
  - MainForm.cs: Main window and layout (tabs, address bar, saved paths panel)
  - ExplorerTab.cs: Encapsulates a single tab (ListView content and navigation)
  - Persistence.cs: Simple JSON config for saved paths
  - MultiTabExplorer.csproj: .NET SDK project file

Build & Run (on Windows)
1) Install .NET SDK 8 (or 6+) on Windows.
2) In a Developer PowerShell/Command Prompt:
   - cd src/MultiTabExplorer
   - dotnet build
   - dotnet run

Notes
- The application is designed for Windows only (TargetFramework: net8.0-windows; UseWindowsForms: true).
- If you need additional shell features (special folders, type description, system image list), we can extend the app with Shell APIs while still relying on native controls.

快捷键
- Ctrl+T：新建标签
- Ctrl+W：关闭标签
- Alt+Up：进入上级目录
- Alt+D / Ctrl+L：编辑地址栏
- Ctrl+N：在当前目录新建文件夹
- F5 / Ctrl+R：刷新当前视图
