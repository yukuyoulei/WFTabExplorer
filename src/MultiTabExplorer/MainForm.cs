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

    public MainForm()
    {
        Text = "多标签资源管理器";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;

        // Left: saved paths
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

        _btnNewTab.Click += (_, __) => AddNewTab(_txtAddress.Text.Trim());
        _btnCloseTab.Click += (_, __) => CloseActiveTab();
        _btnUp.Click += (_, __) => GetActiveExplorerTab()?.NavigateUp();
        _btnBrowse.Click += (_, __) =>
        {
            using var dlg = new FolderBrowserDialog();
            dlg.Description = "选择文件夹";
            if (Directory.Exists(_txtAddress.Text)) dlg.SelectedPath = _txtAddress.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _txtAddress.Text = dlg.SelectedPath;
                NavigateCurrentTo(dlg.SelectedPath);
            }
        };
        _txtAddress.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateCurrentTo(_txtAddress.Text.Trim());
            }
        };
        _btnGo.Click += (_, __) => NavigateCurrentTo(_txtAddress.Text.Trim());

        _tool.Items.AddRange(new ToolStripItem[]
        {
            _btnNewTab,
            _btnCloseTab,
            new ToolStripSeparator(),
            _btnUp,
            _btnBrowse,
            new ToolStripSeparator(),
            _lblAddress,
            _txtAddress,
            _btnGo
        });

        _tabs = new TabControl { Dock = DockStyle.Fill }; 
        _tabs.SelectedIndexChanged += (_, __) =>
        {
            var tab = GetActiveExplorerTab();
            if (tab != null)
            {
                _txtAddress.Text = tab.CurrentPath;
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
        _config = ConfigService.Load();
        foreach (var p in _config.SavedPaths.Distinct().Where(Directory.Exists))
            _savedPaths.Add(p);
        _savedList.DataSource = _savedPaths;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AddNewTab(Directory.Exists(home) ? home : Environment.CurrentDirectory);
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
                _txtAddress.Text = p;
                _tabs.SelectedTab.Text = Path.GetFileName(p).Length > 0 ? Path.GetFileName(p) : p;
            }
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

    private void SaveConfig()
    {
        _config.SavedPaths = _savedPaths.ToList();
        ConfigService.Save(_config);
    }
}
