using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MultiTabExplorer;

public class ExplorerTab : UserControl
{
    private readonly WebBrowser _browser;
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private bool _isNavigatingFromHistory = false;

    public string CurrentPath { get; private set; } = string.Empty;

    public event Action<string>? PathChanged;
    // Kept for compatibility with MainForm wiring; not used with WebBrowser-based rendering.
    public event Action<string, bool>? ItemActivated; // (fullPath, isDirectory)
    
    public event Action? HistoryChanged;
    
    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;

    public ExplorerTab()
    {
        _browser = new WebBrowser
        {
            Dock = DockStyle.Fill,
            AllowWebBrowserDrop = true,
            IsWebBrowserContextMenuEnabled = true,
            WebBrowserShortcutsEnabled = true,
            ScriptErrorsSuppressed = true
        };

        _browser.Navigated += (_, e) => OnBrowserNavigated(e.Url);
        _browser.DocumentCompleted += (_, e) =>
        {
            // DocumentCompleted can fire multiple times (frames); ensure final URL
            if (_browser.Url != null && e.Url == _browser.Url)
            {
                OnBrowserNavigated(_browser.Url);
            }
        };

        Controls.Add(_browser);
    }

    public void NavigateTo(string path, bool recordHistory = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        var fullPath = Path.GetFullPath(path);
        if (string.Equals(CurrentPath, fullPath, StringComparison.OrdinalIgnoreCase)) return;

        if (recordHistory && !string.IsNullOrEmpty(CurrentPath))
        {
            _backStack.Push(CurrentPath);
            _forwardStack.Clear();
            HistoryChanged?.Invoke();
        }

        _isNavigatingFromHistory = !recordHistory;

        try
        {
            _browser.Navigate(fullPath);
        }
        catch
        {
            _isNavigatingFromHistory = false;
        }
    }

    public void NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        var parent = Directory.GetParent(CurrentPath);
        if (parent != null) NavigateTo(parent.FullName);
    }

    public void GoBack()
    {
        if (!CanGoBack) return;

        var target = _backStack.Pop();
        if (!Directory.Exists(target))
        {
            HistoryChanged?.Invoke();
            return;
        }

        if (!string.IsNullOrEmpty(CurrentPath))
        {
            _forwardStack.Push(CurrentPath);
        }

        HistoryChanged?.Invoke();
        NavigateTo(target, recordHistory: false);
    }

    public void GoForward()
    {
        if (!CanGoForward) return;

        var target = _forwardStack.Pop();
        if (!Directory.Exists(target))
        {
            HistoryChanged?.Invoke();
            return;
        }

        if (!string.IsNullOrEmpty(CurrentPath))
        {
            _backStack.Push(CurrentPath);
        }

        HistoryChanged?.Invoke();
        NavigateTo(target, recordHistory: false);
    }

    public void RefreshView()
    {
        try
        {
            _browser.Refresh(WebBrowserRefreshOption.IfExpired);
        }
        catch
        {
            // ignore refresh errors
        }
    }

    private void OnBrowserNavigated(Uri? uri)
    {
        if (uri == null) return;
        string? newPath = null;

        if (uri.IsFile)
        {
            var local = uri.LocalPath;
            if (Directory.Exists(local))
            {
                newPath = Path.GetFullPath(local);
            }
        }
        else
        {
            // Some shell views may expose non-file URIs; best-effort mapping
            var asString = uri.ToString();
            if (!string.IsNullOrWhiteSpace(asString) && Directory.Exists(asString))
            {
                newPath = Path.GetFullPath(asString);
            }
        }

        if (!string.IsNullOrEmpty(newPath))
        {
            if (!string.Equals(CurrentPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                CurrentPath = newPath;
                PathChanged?.Invoke(CurrentPath);
            }
        }

        _isNavigatingFromHistory = false;
        HistoryChanged?.Invoke();
    }

    public void CreateNewFolder()
    {
        var baseDir = CurrentPath;
        if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir)) return;
        try
        {
            string baseName = "新建文件夹";
            string candidate = Path.Combine(baseDir, baseName);
            if (Directory.Exists(candidate))
            {
                int i = 2;
                while (Directory.Exists(candidate) && i < 10000)
                {
                    candidate = Path.Combine(baseDir, $"{baseName} ({i})");
                    i++;
                }
            }
            Directory.CreateDirectory(candidate);
            RefreshView();
        }
        catch (Exception ex)
        {
            try
            {
                MessageBox.Show(FindForm(), $"新建文件夹失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // ignore UI errors
            }
        }
    }
}
