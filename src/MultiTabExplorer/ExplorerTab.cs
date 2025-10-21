using System;
using System.IO;
using System.Windows.Forms;

namespace MultiTabExplorer;

public class ExplorerTab : UserControl
{
    private readonly WebBrowser _browser;

    public string CurrentPath { get; private set; } = string.Empty;

    public event Action<string>? PathChanged;
    // Kept for compatibility with MainForm wiring; not used with WebBrowser-based rendering.
    public event Action<string, bool>? ItemActivated; // (fullPath, isDirectory)

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

    public void NavigateTo(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            try
            {
                // WebBrowser accepts file system paths directly
                _browser.Navigate(path);
            }
            catch
            {
                // ignore navigation errors
            }
        }
    }

    public void NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        var parent = Directory.GetParent(CurrentPath);
        if (parent != null) NavigateTo(parent.FullName);
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

        if (!string.IsNullOrEmpty(newPath) && !string.Equals(CurrentPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            CurrentPath = newPath;
            PathChanged?.Invoke(CurrentPath);
        }
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
