using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MultiTabExplorer;

public class DriveStatusControl : UserControl
{
    private readonly Label _lblDriveName;
    private readonly Label _lblDriveType;
    private readonly ProgressBar _progressBar;
    private readonly Label _lblSpaceInfo;
    private readonly Panel _contentPanel;
    private readonly ToolTip _toolTip;

    public event Action<string>? DriveClicked;

    private string _drivePath = string.Empty;

    public DriveStatusControl()
    {
        Height = 80;
        MinimumSize = new Size(0, 80);
        BorderStyle = BorderStyle.FixedSingle;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(8);
        BackColor = SystemColors.Control;

        _toolTip = new ToolTip();

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0)
        };

        _lblDriveName = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 20,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _lblDriveType = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 16,
            Font = new Font(Font.FontFamily, 8),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 16,
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100
        };

        _lblSpaceInfo = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 16,
            Font = new Font(Font.FontFamily, 8),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _contentPanel.Controls.Add(_lblSpaceInfo);
        _contentPanel.Controls.Add(_progressBar);
        _contentPanel.Controls.Add(_lblDriveType);
        _contentPanel.Controls.Add(_lblDriveName);
        Controls.Add(_contentPanel);

        Click += OnControlClick;
        DoubleClick += OnControlClick;
        _contentPanel.Click += OnControlClick;
        _contentPanel.DoubleClick += OnControlClick;
        _lblDriveName.Click += OnControlClick;
        _lblDriveName.DoubleClick += OnControlClick;
        _lblDriveType.Click += OnControlClick;
        _lblDriveType.DoubleClick += OnControlClick;
        _progressBar.Click += OnControlClick;
        _progressBar.DoubleClick += OnControlClick;
        _lblSpaceInfo.Click += OnControlClick;
        _lblSpaceInfo.DoubleClick += OnControlClick;

        MouseEnter += OnControlMouseEnter;
        MouseLeave += OnControlMouseLeave;
        _contentPanel.MouseEnter += OnControlMouseEnter;
        _contentPanel.MouseLeave += OnControlMouseLeave;
        _lblDriveName.MouseEnter += OnControlMouseEnter;
        _lblDriveName.MouseLeave += OnControlMouseLeave;
        _lblDriveType.MouseEnter += OnControlMouseEnter;
        _lblDriveType.MouseLeave += OnControlMouseLeave;
        _progressBar.MouseEnter += OnControlMouseEnter;
        _progressBar.MouseLeave += OnControlMouseLeave;
        _lblSpaceInfo.MouseEnter += OnControlMouseEnter;
        _lblSpaceInfo.MouseLeave += OnControlMouseLeave;
    }

    public void UpdateFromDrive(DriveInfo drive)
    {
        _drivePath = drive.Name;

        var driveLetter = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string driveLabel;
        try
        {
            driveLabel = drive.VolumeLabel;
        }
        catch
        {
            driveLabel = string.Empty;
        }
        driveLabel = string.IsNullOrWhiteSpace(driveLabel) ? "本地磁盘" : driveLabel;
        _lblDriveName.Text = $"{driveLetter} ({driveLabel})";

        string driveTypeText = drive.DriveType switch
        {
            DriveType.Fixed => "本地磁盘",
            DriveType.CDRom => "光盘驱动器",
            DriveType.Removable => "可移动磁盘",
            DriveType.Network => "网络驱动器",
            DriveType.Ram => "RAM磁盘",
            _ => drive.DriveType.ToString()
        };
        _lblDriveType.Text = driveTypeText;

        try
        {
            if (drive.IsReady)
            {
                var totalBytes = drive.TotalSize;
                var freeBytes = drive.AvailableFreeSpace;
                var usedBytes = Math.Max(0, totalBytes - freeBytes);

                var percent = totalBytes > 0 ? usedBytes * 100d / totalBytes : 0d;
                var usedPercentage = (int)Math.Round(Math.Max(0, Math.Min(100, percent)));
                _progressBar.Value = usedPercentage;

                var percentText = percent >= 100
                    ? "100"
                    : percent >= 10
                        ? percent.ToString("0.#")
                        : percent.ToString("0.##");

                _lblSpaceInfo.Text = $"{FormatSize(freeBytes)} 可用 / 共 {FormatSize(totalBytes)} ({percentText}%)";

                if (usedPercentage >= 90)
                    _progressBar.ForeColor = Color.Red;
                else if (usedPercentage >= 70)
                    _progressBar.ForeColor = Color.Orange;
                else
                    _progressBar.ForeColor = Color.Green;
            }
            else
            {
                _progressBar.Value = 0;
                _lblSpaceInfo.Text = "驱动器未就绪";
            }
        }
        catch
        {
            _progressBar.Value = 0;
            _lblSpaceInfo.Text = "无法读取驱动器信息";
        }

        UpdateToolTip();
    }

    private void OnControlClick(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_drivePath))
        {
            DriveClicked?.Invoke(_drivePath);
        }
    }

    private void OnControlMouseEnter(object? sender, EventArgs e)
    {
        BackColor = SystemColors.ControlLight;
    }

    private void OnControlMouseLeave(object? sender, EventArgs e)
    {
        BackColor = SystemColors.Control;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024L * 1024)
            return $"{bytes / 1024.0:0.##} KB";
        else if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):0.##} MB";
        else if (bytes < 1024L * 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):0.##} GB";
        else
            return $"{bytes / (1024.0 * 1024 * 1024 * 1024):0.##} TB";
    }

    private void UpdateToolTip()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(_lblDriveName.Text)) lines.Add(_lblDriveName.Text);
        if (!string.IsNullOrWhiteSpace(_lblDriveType.Text)) lines.Add(_lblDriveType.Text);
        if (!string.IsNullOrWhiteSpace(_lblSpaceInfo.Text)) lines.Add(_lblSpaceInfo.Text);
        if (!string.IsNullOrWhiteSpace(_drivePath)) lines.Add(_drivePath);

        var text = string.Join("\n", lines);

        _toolTip.SetToolTip(this, text);
        _toolTip.SetToolTip(_contentPanel, text);
        _toolTip.SetToolTip(_lblDriveName, text);
        _toolTip.SetToolTip(_lblDriveType, text);
        _toolTip.SetToolTip(_progressBar, text);
        _toolTip.SetToolTip(_lblSpaceInfo, text);
    }
}
