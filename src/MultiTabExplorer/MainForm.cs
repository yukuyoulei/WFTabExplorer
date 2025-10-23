using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    private readonly ToolStrip _tool;
    private readonly ToolStripButton _btnNewTab;
    private readonly ToolStripButton _btnCloseTab;
    private readonly ToolStripButton _btnUp;
    private readonly ToolStripButton _btnBrowse;
    private readonly ToolStripLabel _lblAddress;
    private readonly ToolStripTextBox _txtAddress;
    private readonly ToolStripButton _btnGo;

    private readonly TabControl _tabs;

    private readonly BindingList<string> _savedPaths = new();
    private AppConfig _config = new();

    // 面包屑管理
    private readonly List<ToolStripItem> _breadcrumbItems = new();
    private int _breadcrumbInsertIndex;

    // 地址编辑状态
    private bool _isEditingAddress = false;

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
        _split.Panel1.Controls.Add(_savedGroup);

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

        Controls.Add(_split);

        Load += (_, __) => OnLoaded();
        FormClosing += (_, __) => SaveConfig();
    }

    private void OnLoaded()
    {
        // Hide the left saved-paths panel as per requirement
        _split.Panel1Collapsed = true;

        _config = ConfigService.Load();
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
        ConfigService.Save(_config);
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
}
