using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MultiTabExplorer;

public class MainForm : Form
{
    private readonly SplitContainer _split;
    private readonly GroupBox _savedGroup;
    private readonly ListBox _savedList;
    private readonly FlowLayoutPanel _savedButtons;
    private readonly Button _btnSaveCurrent;
    private readonly Button _btnBrowseAdd;
    private readonly Button _btnOpenSavedNewTab;

    private readonly GroupBox _driveGroup;
    private readonly FlowLayoutPanel _drivePanel;
    private readonly Dictionary<string, DriveStatusControl> _driveControls = new(StringComparer.OrdinalIgnoreCase);
    private System.Windows.Forms.Timer _driveRefreshTimer;

    private readonly ToolStrip _tool;
    private readonly ToolStripButton _btnNewTab;
    private readonly ToolStripButton _btnCloseTab;
    private readonly ToolStripButton _btnUp;
    private readonly ToolStripButton _btnBrowse;
    private readonly ToolStripLabel _lblAddress;
    private readonly ToolStripTextBox _txtAddress;
    private readonly ToolStripButton _btnGo;

    private readonly ToolStrip _bottomTool;
    private readonly ToolStripLabel _hotKeyLabel;
    private readonly ToolStripTextBox _freqTextBox;
    private readonly ToolStripButton _startButton;
    private readonly ToolStripButton _setHotKeyButton;

    private readonly TabControl _tabs;

    private readonly BindingList<string> _savedPaths = new();
    private AppConfig _config = new();

    // 面包屑管理
    private readonly List<ToolStripItem> _breadcrumbItems = new();
    private int _breadcrumbInsertIndex;

    // 地址编辑状态
    private bool _isEditingAddress = false;

    private System.Threading.Timer _autoClickTimer;
    private Keys _autoClickHotKey = Keys.None;
    private bool _isAutoClicking = false;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // [DllImport("user32.dll")]
    // static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const int INPUT_MOUSE = 0;
    private const int MOUSEEVENTF_LEFTDOWN = 0x02;
    private const int MOUSEEVENTF_LEFTUP = 0x04;

    public MainForm()
    {
        Text = "多标签资源管理器";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true; // 使窗体能捕获快捷键
        KeyDown += OnMainFormKeyDown;

        // Left: saved paths (will be hidden per requirements)
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 260,
        };

        _savedGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "已保存的路径",
            Padding = new Padding(8),
        };
        _savedList = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
        };
        _savedList.DoubleClick += (_, __) =>
        {
            if (_savedList.SelectedItem is string path && Directory.Exists(path))
            {
                NavigateCurrentTo(path);
            }
        };
        _savedList.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                RemoveSelectedSavedPath();
            }
        };

        _savedButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Height = 40,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _btnSaveCurrent = new Button { Text = "保存当前", AutoSize = true };
        _btnBrowseAdd = new Button { Text = "浏览添加", AutoSize = true };
        _btnOpenSavedNewTab = new Button { Text = "在新标签打开", AutoSize = true };

        _btnSaveCurrent.Click += (_, __) =>
        {
            var path = GetActiveExplorerTab()?.CurrentPath ?? _txtAddress.Text.Trim();
            TryAddSavedPath(path);
        };
        _btnBrowseAdd.Click += (_, __) =>
        {
            using var dlg = new FolderBrowserDialog();
            dlg.Description = "选择要保存的文件夹";
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                TryAddSavedPath(dlg.SelectedPath);
            }
        };
        _btnOpenSavedNewTab.Click += (_, __) =>
        {
            if (_savedList.SelectedItem is string path && Directory.Exists(path))
            {
                AddNewTab(path);
            }
        };

        _savedButtons.Controls.AddRange(new Control[] { _btnSaveCurrent, _btnBrowseAdd, _btnOpenSavedNewTab });
        _savedGroup.Controls.Add(_savedList);
        _savedGroup.Controls.Add(_savedButtons);

        _driveGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "驱动器列表",
            Padding = new Padding(8),
        };
        _drivePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0),
        };
        _drivePanel.Resize += (_, __) => UpdateDriveControlWidths();
        _driveGroup.Controls.Add(_drivePanel);

        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        leftLayout.Controls.Add(_driveGroup, 0, 0);
        leftLayout.Controls.Add(_savedGroup, 0, 1);
        _split.Panel1.Controls.Add(leftLayout);

        _driveRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 3000
        };
        _driveRefreshTimer.Tick += (_, __) => RefreshDriveList();

        // Right: tool + tabs
        _tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top, RenderMode = ToolStripRenderMode.System };
        _btnNewTab = new ToolStripButton("新建标签");
        _btnCloseTab = new ToolStripButton("关闭标签");
        _btnUp = new ToolStripButton("上级");
        _btnBrowse = new ToolStripButton("浏览");
        _lblAddress = new ToolStripLabel("地址:");
        _txtAddress = new ToolStripTextBox { AutoSize = false, Width = 600 };
        _btnGo = new ToolStripButton("转到");

        _btnNewTab.Click += (_, __) => AddNewTab(GetActiveExplorerTab()?.CurrentPath);
        _btnCloseTab.Click += (_, __) => CloseActiveTab();
        _btnUp.Click += (_, __) => GetActiveExplorerTab()?.NavigateUp();
        _btnBrowse.Click += (_, __) =>
        {
            using var dlg = new FolderBrowserDialog();
            dlg.Description = "选择文件夹";
            var current = GetActiveExplorerTab()?.CurrentPath;
            if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current)) dlg.SelectedPath = current!;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                NavigateCurrentTo(dlg.SelectedPath);
            }
        };
        // address textbox for Alt+D 编辑模式
        _txtAddress.Visible = false;
        _txtAddress.KeyDown += OnAddressBoxKeyDown;
        _txtAddress.LostFocus += (_, __) => FinishAddressEdit(false);
        _btnGo.Visible = false;

        // Build base toolbar items; breadcrumbs will be injected after _lblAddress
        _tool.Items.AddRange(new ToolStripItem[]
        {
            _btnNewTab,
            _btnCloseTab,
            new ToolStripSeparator(),
            _btnUp,
            _btnBrowse,
            new ToolStripSeparator(),
            _lblAddress
        });
        _breadcrumbInsertIndex = _tool.Items.IndexOf(_lblAddress) + 1;

        _tabs = new TabControl { Dock = DockStyle.Fill };
        _tabs.SelectedIndexChanged += (_, __) =>
        {
            var tab = GetActiveExplorerTab();
            if (tab != null)
            {
                UpdateBreadcrumb(tab.CurrentPath);
            }
        };

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(_tabs);
        rightPanel.Controls.Add(_tool);
        _split.Panel2.Controls.Add(rightPanel);

        _bottomTool = new ToolStrip { Dock = DockStyle.Bottom };
        _hotKeyLabel = new ToolStripLabel("热键: " + _autoClickHotKey.ToString());
        _freqTextBox = new ToolStripTextBox { Text = _config.ClickFrequency.ToString(), Width = 50 };
        _startButton = new ToolStripButton("开始");
        _setHotKeyButton = new ToolStripButton("设置热键");

        _startButton.Click += (_, __) =>
        {
            if (_isAutoClicking)
            {
                StopAutoClick();
            }
            else
            {
                StartAutoClick();
            }
        };

        _setHotKeyButton.Click += (_, __) =>
        {
            var result = MessageBox.Show("请按下新的热键，或者按 Esc 取消", "设置热键", MessageBoxButtons.OKCancel);
            if (result == DialogResult.OK)
            {
                // Temporarily listen for the next key press
                KeyEventHandler handler = null;
                handler = (s, e) =>
                {
                    if (e.KeyCode != Keys.Escape)
                    {
                        OnHotKeyChanged(e.KeyCode);
                        _hotKeyLabel.Text = "热键: " + e.KeyCode.ToString();
                        _config.HotKey = e.KeyCode.ToString();
                        SaveConfig();
                    }
                    KeyDown -= handler;
                };
                KeyDown += handler;
            }
        };

        _bottomTool.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripLabel("点击频率 (Hz):"),
            _freqTextBox,
            _startButton,
            new ToolStripSeparator(),
            _hotKeyLabel,
            _setHotKeyButton
        });
        Controls.Add(_bottomTool);
        Controls.Add(_split);

        Load += (_, __) => OnLoaded();
        FormClosing += (_, __) =>
        {
            _driveRefreshTimer?.Stop();
            SaveConfig();
        };

        _autoClickTimer = new System.Threading.Timer(callback: _ =>
        {
            INPUT[] inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new InputUnion
                    {
                        mi = new MOUSEINPUT
                        {
                            dwFlags = MOUSEEVENTF_LEFTDOWN
                        }
                    }
                },
                new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new InputUnion
                    {
                        mi = new MOUSEINPUT
                        {
                            dwFlags = MOUSEEVENTF_LEFTUP
                        }
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void OnLoaded()
    {
        _split.Panel1Collapsed = false;
        _driveGroup.Visible = true;
        _savedGroup.Visible = true;

        _config = ConfigService.Load();

        if (Enum.TryParse<Keys>(_config.HotKey, out Keys hotKey))
        {
            OnHotKeyChanged(hotKey);
            _hotKeyLabel.Text = "热键: " + hotKey.ToString();
        }
        _freqTextBox.Text = _config.ClickFrequency.ToString();
        
        foreach (var p in _config.SavedPaths.Distinct().Where(Directory.Exists))
            _savedPaths.Add(p);
        _savedList.DataSource = _savedPaths;

        var existing = _savedPaths.Distinct().Where(Directory.Exists).ToList();
        if (existing.Count > 0)
        {
            foreach (var p in existing)
                AddNewTab(p);
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddNewTab(Directory.Exists(home) ? home : Environment.CurrentDirectory);
        }

        RefreshDriveList();
        _driveRefreshTimer.Start();
    }

    private void TryAddSavedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Directory.Exists(path))
        {
            MessageBox.Show(this, "路径不存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!_savedPaths.Contains(path))
        {
            _savedPaths.Add(path);
            SaveConfig();
        }
    }

    private void RemoveSelectedSavedPath()
    {
        if (_savedList.SelectedItem is string toRemove)
        {
            _savedPaths.Remove(toRemove);
            SaveConfig();
        }
    }

    private ExplorerTab? GetActiveExplorerTab()
    {
        if (_tabs.SelectedTab?.Tag is ExplorerTab tab) return tab;
        return null;
    }

    private void AddNewTab(string? initialPath)
    {
        string path = !string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath)
            ? initialPath!
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var tab = new ExplorerTab();
        tab.Dock = DockStyle.Fill;
        tab.PathChanged += p =>
        {
            if (_tabs.SelectedTab?.Tag == tab)
            {
                UpdateBreadcrumb(p);
                _tabs.SelectedTab.Text = Path.GetFileName(p).Length > 0 ? Path.GetFileName(p) : p;
            }
            SaveOpenTabsToConfig();
        };
        tab.ItemActivated += (fullPath, isDirectory) =>
        {
            if (isDirectory)
            {
                tab.NavigateTo(fullPath);
            }
            else
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        };

        var page = new TabPage { Text = Path.GetFileName(path).Length > 0 ? Path.GetFileName(path) : path, Tag = tab };
        page.Controls.Add(tab);
        _tabs.TabPages.Add(page);
        _tabs.SelectedTab = page;

        tab.NavigateTo(path);
    }

    private void CloseActiveTab()
    {
        if (_tabs.TabPages.Count <= 1) return; // 保留至少一个标签
        var page = _tabs.SelectedTab;
        if (page != null)
        {
            _tabs.TabPages.Remove(page);
            page.Dispose();
            SaveOpenTabsToConfig();
        }
    }

    private void NavigateCurrentTo(string path)
    {
        if (!Directory.Exists(path))
        {
            MessageBox.Show(this, "路径不存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var tab = GetActiveExplorerTab();
        tab?.NavigateTo(path);
    }

    private void SaveOpenTabsToConfig()
    {
        var paths = new List<string>();
        foreach (TabPage page in _tabs.TabPages)
        {
            if (page.Tag is ExplorerTab t)
            {
                var p = t.CurrentPath;
                if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                {
                    var full = Path.GetFullPath(p);
                    if (!paths.Any(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
                        paths.Add(full);
                }
            }
        }

        _savedPaths.Clear();
        foreach (var p in paths)
            _savedPaths.Add(p);
        SaveConfig();
    }

    private void UpdateBreadcrumb(string path)
    {
        // 移除旧的面包屑项
        foreach (var it in _breadcrumbItems)
        {
            _tool.Items.Remove(it);
            it.Dispose();
        }
        _breadcrumbItems.Clear();

        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full) ?? string.Empty;
            var segments = new List<(string text, string fullPath)>();

            if (!string.IsNullOrEmpty(root))
            {
                var rootDisplay = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (rootDisplay.EndsWith(":"))
                    rootDisplay = rootDisplay;
                segments.Add((rootDisplay, root));
            }

            var remainder = full.Substring(root.Length);
            if (!string.IsNullOrEmpty(remainder))
            {
                var parts = remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                string accum = root;
                foreach (var part in parts)
                {
                    accum = Path.Combine(accum, part);
                    segments.Add((part, accum));
                }
            }

            for (int i = 0; i < segments.Count; i++)
            {
                var (text, dest) = segments[i];
                var btn = new ToolStripButton(text) { Tag = dest };
                btn.Click += (_, __) => NavigateCurrentTo(dest);
                _breadcrumbItems.Add(btn);
                if (i < segments.Count - 1)
                {
                    var sep = new ToolStripLabel(">") { Enabled = false };
                    _breadcrumbItems.Add(sep);
                }
            }

            for (int i = 0; i < _breadcrumbItems.Count; i++)
            {
                _tool.Items.Insert(_breadcrumbInsertIndex + i, _breadcrumbItems[i]);
            }
        }
        catch
        {
            // ignore invalid paths
        }
    }

    private void SaveConfig()
    {
        _config.SavedPaths = _savedPaths.ToList();
        if (int.TryParse(_freqTextBox.Text, out int freq))
        {
            _config.ClickFrequency = freq;
        }
        else
        {
            _config.ClickFrequency = 10;
        }
        ConfigService.Save(_config);
    }

    private void OnHotKeyChanged(Keys newHotKey)
    {
        // Unregister the old hot key
        if (_autoClickHotKey != Keys.None)
        {
            UnregisterHotKey(Handle, 0);
        }

        // Register the new hot key
        _autoClickHotKey = newHotKey;
        if (_autoClickHotKey != Keys.None)
        {
            RegisterHotKey(Handle, 0, 0, (int)_autoClickHotKey);
        }
    }

    private void OnMainFormKeyDown(object? sender, KeyEventArgs e)
    {
        // Alt+D 开启地址编辑
        if (e.Alt && e.KeyCode == Keys.D)
        {
            e.Handled = true;
            StartAddressEdit();
        }
    }

    private void StartAutoClick()
    {
        if (int.TryParse(_freqTextBox.Text, out int freq) && freq > 0)
        {
            _isAutoClicking = true;
            _startButton.Text = "停止";
            _autoClickTimer.Change(0, 1000 / freq);
        }
        else
        {
            MessageBox.Show("无效的频率");
        }
    }

    private void StopAutoClick()
    {
        _isAutoClicking = false;
        _startButton.Text = "开始";
        _autoClickTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }


    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_isEditingAddress)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // 全局快捷键：确保在目录视图(WebBrowser)聚焦时也可触发
        if (keyData == (Keys.Control | Keys.T))
        {
            AddNewTab(GetActiveExplorerTab()?.CurrentPath);
            return true;
        }
        if (keyData == (Keys.Control | Keys.W))
        {
            CloseActiveTab();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.Up))
        {
            var tab = GetActiveExplorerTab();
            tab?.NavigateUp();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.D) || keyData == (Keys.Control | Keys.L))
        {
            StartAddressEdit();
            return true;
        }
        if (keyData == (Keys.Control | Keys.N) || keyData == (Keys.Control | Keys.Shift | Keys.N))
        {
            var tab = GetActiveExplorerTab();
            tab?.CreateNewFolder();
            return true;
        }
        if (keyData == Keys.F5 || keyData == (Keys.Control | Keys.R))
        {
            var tab = GetActiveExplorerTab();
            tab?.RefreshView();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == 0x0312)
        {
            if (_isAutoClicking)
            {
                StopAutoClick();
            }
            else
            {
                StartAutoClick();
            }
        }
    }
    
    private void StartAddressEdit()
    {
        if (_isEditingAddress) return;

        // 隐藏面包屑
        foreach (var it in _breadcrumbItems)
        {
            it.Visible = false;
        }

        // 插入并显示文本框
        if (!_tool.Items.Contains(_txtAddress))
        {
            _tool.Items.Insert(_breadcrumbInsertIndex, _txtAddress);
        }
        _txtAddress.Text = GetActiveExplorerTab()?.CurrentPath ?? string.Empty;
        _txtAddress.Visible = true;
        _isEditingAddress = true;

        _txtAddress.Focus();
        _txtAddress.SelectAll();
    }

    private void FinishAddressEdit(bool commit)
    {
        if (!_isEditingAddress) return;

        if (commit)
        {
            var input = (_txtAddress.Text ?? string.Empty).Trim();
            if (input.Length == 2 && char.IsLetter(input[0]) && input[1] == ':')
            {
                input += Path.DirectorySeparatorChar; // 兼容 "C:" -> "C:\"
            }
            if (!string.IsNullOrWhiteSpace(input))
            {
                if (Directory.Exists(input))
                {
                    NavigateCurrentTo(input);
                }
                else
                {
                    MessageBox.Show(this, "路径不存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // 移除文本框，恢复面包屑
        if (_tool.Items.Contains(_txtAddress))
        {
            _tool.Items.Remove(_txtAddress);
        }
        _txtAddress.Visible = false;

        // 重新生成面包屑以反映最新路径
        var current = GetActiveExplorerTab()?.CurrentPath ?? string.Empty;
        UpdateBreadcrumb(current);

        _isEditingAddress = false;
    }

    private void OnAddressBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            FinishAddressEdit(true);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            FinishAddressEdit(false);
        }
    }

    private void RefreshDriveList()
    {
        _drivePanel.SuspendLayout();

        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d =>
                {
                    try
                    {
                        return d.IsReady;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var drive in drives)
            {
                var key = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                activeKeys.Add(key);

                if (!_driveControls.TryGetValue(key, out var control))
                {
                    control = new DriveStatusControl();
                    control.DriveClicked += path => NavigateCurrentTo(path);
                    _driveControls[key] = control;
                    _drivePanel.Controls.Add(control);
                }

                control.UpdateFromDrive(drive);
            }

            foreach (var key in _driveControls.Keys.ToList())
            {
                if (!activeKeys.Contains(key))
                {
                    if (_driveControls[key] is Control ctrl)
                    {
                        _drivePanel.Controls.Remove(ctrl);
                        ctrl.Dispose();
                    }
                    _driveControls.Remove(key);
                }
            }

            for (int i = 0; i < drives.Count; i++)
            {
                var key = drives[i].Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (_driveControls.TryGetValue(key, out var ctrl))
                {
                    _drivePanel.Controls.SetChildIndex(ctrl, i);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            _drivePanel.ResumeLayout();
            UpdateDriveControlWidths();
        }
    }

    private void UpdateDriveControlWidths()
    {
        if (_drivePanel.Controls.Count == 0) return;

        var targetWidth = _drivePanel.ClientSize.Width - _drivePanel.Padding.Horizontal;
        if (_drivePanel.VerticalScroll.Visible)
        {
            targetWidth -= SystemInformation.VerticalScrollBarWidth;
        }
        targetWidth = Math.Max(80, targetWidth);

        foreach (Control control in _drivePanel.Controls)
        {
            control.Width = targetWidth;
        }
    }
}
