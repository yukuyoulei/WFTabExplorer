using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MultiTabExplorer;

public class ExplorerTab : UserControl
{
    private readonly ListView _list;
    private readonly ImageList _images;

    public string CurrentPath { get; private set; } = string.Empty;

    public event Action<string>? PathChanged;
    public event Action<string, bool>? ItemActivated; // (fullPath, isDirectory)

    public ExplorerTab()
    {
        _images = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(16, 16)
        };

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            SmallImageList = _images,
        };
        _list.Columns.Add("名称", 320, HorizontalAlignment.Left);
        _list.Columns.Add("类型", 120, HorizontalAlignment.Left);
        _list.Columns.Add("大小", 100, HorizontalAlignment.Right);
        _list.Columns.Add("修改日期", 160, HorizontalAlignment.Left);

        _list.ItemActivate += (_, __) =>
        {
            if (_list.SelectedItems.Count == 0) return;
            var item = _list.SelectedItems[0];
            var fullPath = item.Tag as string ?? string.Empty;
            bool isDir = item.SubItems[1].Text == "文件夹";
            ItemActivated?.Invoke(fullPath, isDir);
        };

        Controls.Add(_list);
    }

    public void NavigateTo(string path)
    {
        if (!Directory.Exists(path)) return;
        CurrentPath = path;
        PathChanged?.Invoke(CurrentPath);
        PopulateList();
    }

    public void NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        var parent = Directory.GetParent(CurrentPath);
        if (parent != null) NavigateTo(parent.FullName);
    }

    private void PopulateList()
    {
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            _images.Images.Clear();

            // Parent directory item
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                var upItem = new ListViewItem("..", GetIconIndexForFolder())
                {
                    Tag = parent.FullName
                };
                upItem.SubItems.Add("文件夹");
                upItem.SubItems.Add("");
                upItem.SubItems.Add("");
                _list.Items.Add(upItem);
            }

            IEnumerable<DirectoryInfo> dirs = Enumerable.Empty<DirectoryInfo>();
            IEnumerable<FileInfo> files = Enumerable.Empty<FileInfo>();
            try
            {
                var di = new DirectoryInfo(CurrentPath);
                dirs = di.EnumerateDirectories();
                files = di.EnumerateFiles();
            }
            catch
            {
                // ignore access issues
            }

            foreach (var d in dirs.OrderBy(d => d.Name))
            {
                var lvi = new ListViewItem(d.Name, GetIconIndexForFolder())
                {
                    Tag = d.FullName
                };
                lvi.SubItems.Add("文件夹");
                lvi.SubItems.Add("");
                lvi.SubItems.Add(d.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                _list.Items.Add(lvi);
            }

            foreach (var f in files.OrderBy(f => f.Name))
            {
                var sizeText = FormatFileSize(f.Length);
                int iconIndex = GetIconIndexForFile(f.FullName);
                var lvi = new ListViewItem(f.Name, iconIndex)
                {
                    Tag = f.FullName
                };
                lvi.SubItems.Add(f.Extension.TrimStart('.').ToUpperInvariant() + " 文件");
                lvi.SubItems.Add(sizeText);
                lvi.SubItems.Add(f.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                _list.Items.Add(lvi);
            }

            AutoResizeColumns();
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    private void AutoResizeColumns()
    {
        for (int i = 0; i < _list.Columns.Count; i++)
        {
            _list.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.HeaderSize);
            _list.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.ColumnContent);
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.#} {1}", len, sizes[order]);
    }

    private int GetIconIndexForFolder()
    {
        const string key = "__folder";
        if (!_images.Images.ContainsKey(key))
        {
            using var icon = NativeIcons.GetSmallIconForFolder();
            if (icon != null)
            {
                _images.Images.Add(key, icon);
            }
            else
            {
                using var bmp = SystemIcons.Application.ToBitmap();
                _images.Images.Add(key, bmp);
            }
        }
        return _images.Images.IndexOfKey(key);
    }

    private int GetIconIndexForFile(string fullPath)
    {
        string key = Path.GetExtension(fullPath).ToLowerInvariant();
        if (string.IsNullOrEmpty(key)) key = "__noext";

        int idx = _images.Images.IndexOfKey(key);
        if (idx >= 0) return idx;

        try
        {
            using var icon = NativeIcons.GetSmallIconForFile(fullPath);
            if (icon != null)
            {
                _images.Images.Add(key, icon);
                return _images.Images.Count - 1;
            }
        }
        catch { }

        // fallback
        using var bmp = SystemIcons.Application.ToBitmap();
        _images.Images.Add(key, bmp);
        return _images.Images.Count - 1;
    }
}
