using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace VideoFileRenamer;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mov",
        ".mkv",
        ".avi",
        ".wmv",
        ".flv",
        ".webm",
        ".m4v",
        ".mts",
        ".m2ts",
        ".ts"
    };

    private static readonly Regex NvidiaReplayRegex = new(
        @"^(?<game>[A-Za-z][A-Za-z0-9 '&._+\-:(),\[\]!]*?)\s+(?<year>(?:19|20)\d{2})[.\-_](?<month>0?[1-9]|1[0-2])[.\-_](?<day>0?[1-9]|[12]\d|3[01])\s*-\s*(?<hour>[01]?\d|2[0-3])[.\-_:](?<minute>[0-5]\d)[.\-_:](?<second>[0-5]\d)(?:[.\-_](?:\d+|DVR))*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DateRegex = new(
        @"(?<!\d)(?<year>(?:19|20)\d{2})\s*(?:[./_\-]\s*)?(?<month>0?[1-9]|1[0-2])\s*(?:[./_\-]\s*)?(?<day>0?[1-9]|[12]\d|3[01])(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex SeparatedTimeRegex = new(
        @"(?<![\dA-Za-z])(?<hour>[01]?\d|2[0-3])\s*(?:[:._\-hH])\s*(?<minute>[0-5]\d)(?:\s*(?:[:._\-mM])\s*(?<second>[0-5]\d))?\s*(?:s|S)?(?![\dA-Za-z])",
        RegexOptions.Compiled);

    private static readonly Regex CompactTimeRegex = new(
        @"(?<![\dA-Za-z])(?<hour>[01]\d|2[0-3])(?<minute>[0-5]\d)(?<second>[0-5]\d)?(?![\dA-Za-z])",
        RegexOptions.Compiled);

    public ObservableCollection<RenameItem> RenameItems { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Choose the folder that contains your video clips",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(FolderPathTextBox.Text) ? FolderPathTextBox.Text : string.Empty
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        FolderPathTextBox.Text = dialog.SelectedPath;
        BuildPreview();
    }

    private void ChooseOutputFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Choose the folder where renamed files should be moved",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(OutputFolderPathTextBox.Text)
                ? OutputFolderPathTextBox.Text
                : Directory.Exists(FolderPathTextBox.Text)
                    ? FolderPathTextBox.Text
                    : string.Empty
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        OutputFolderPathTextBox.Text = dialog.SelectedPath;
        BuildPreview();
    }

    private void ClearOutputFolderButton_Click(object sender, RoutedEventArgs e)
    {
        OutputFolderPathTextBox.Clear();
        BuildPreview();
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        BuildPreview();
    }

    private void OperationModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        BuildPreview();
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        var itemsToRename = RenameItems.Where(item => item.CanRename).ToList();
        if (itemsToRename.Count == 0)
        {
            StatusTextBlock.Text = "No files are ready to rename.";
            RenameButton.IsEnabled = false;
            return;
        }

        var operationMode = GetOperationMode();
        var actionText = operationMode == FileOperationMode.Copy
            ? "Copy renamed"
            : HasOutputFolder()
                ? "Rename and move"
                : "Rename";
        var confirmResult = System.Windows.MessageBox.Show(
            $"{actionText} {itemsToRename.Count} files? A CSV log will be written before processing.",
            $"Confirm {actionText.ToLowerInvariant()}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
        {
            return;
        }

        string logPath;
        StreamWriter logWriter;
        try
        {
            logPath = CreateLogPath();
            logWriter = new StreamWriter(logPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            WriteLogHeader(logWriter);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to create log file: {ex.Message}";
            return;
        }

        var successCount = 0;
        var failureCount = 0;

        using (logWriter)
        {
            foreach (var item in itemsToRename)
            {
                try
                {
                    if (operationMode == FileOperationMode.Copy)
                    {
                        File.Copy(item.FilePath, item.TargetPath);
                    }
                    else
                    {
                        File.Move(item.FilePath, item.TargetPath);
                    }

                    item.Status = operationMode == FileOperationMode.Copy ? "Copied" : "Done";
                    item.CanRename = false;
                    successCount++;
                    WriteLogRow(logWriter, operationMode, item, "Success", string.Empty);
                }
                catch (Exception ex)
                {
                    item.Status = $"Failed: {ex.Message}";
                    failureCount++;
                    WriteLogRow(logWriter, operationMode, item, "Failed", ex.Message);
                }
            }
        }

        CleanupOldLogs(logPath);
        RenameButton.IsEnabled = RenameItems.Any(item => item.CanRename);
        StatusTextBlock.Text = operationMode == FileOperationMode.Copy
            ? $"Finished. Copied: {successCount}. Failed: {failureCount}. Log: {logPath}"
            : HasOutputFolder()
                ? $"Finished. Renamed and moved: {successCount}. Failed: {failureCount}. Log: {logPath}"
                : $"Finished. Renamed: {successCount}. Failed: {failureCount}. Log: {logPath}";
    }

    private void BuildPreview()
    {
        RenameItems.Clear();
        RenameButton.IsEnabled = false;

        var folderPath = FolderPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusTextBlock.Text = "Choose a folder first.";
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            StatusTextBlock.Text = "The selected folder does not exist.";
            return;
        }

        var outputFolderPath = OutputFolderPathTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(outputFolderPath) && !Directory.Exists(outputFolderPath))
        {
            StatusTextBlock.Text = "The output folder does not exist.";
            return;
        }

        var searchOption = IncludeSubfoldersCheckBox.IsChecked == true
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        List<string> files;
        try
        {
            files = Directory.EnumerateFiles(folderPath, "*", searchOption)
                .Where(ShouldIncludeFile)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to read files: {ex.Message}";
            return;
        }

        var reservedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            RenameItems.Add(CreateRenameItem(file, outputFolderPath, reservedTargets));
        }

        var readyCount = RenameItems.Count(item => item.CanRename);
        var skippedCount = RenameItems.Count - readyCount;
        RenameButton.IsEnabled = readyCount > 0;
        StatusTextBlock.Text = HasOutputFolder()
            ? $"Files: {files.Count}. Ready to rename and {GetOperationVerb().ToLowerInvariant()}: {readyCount}. Skipped: {skippedCount}."
            : GetOperationMode() == FileOperationMode.Copy
                ? $"Files: {files.Count}. Ready to copy with new names: {readyCount}. Skipped: {skippedCount}."
                : $"Files: {files.Count}. Ready: {readyCount}. Skipped: {skippedCount}.";
    }

    private bool ShouldIncludeFile(string file)
    {
        return VideoOnlyCheckBox.IsChecked != true || VideoExtensions.Contains(Path.GetExtension(file));
    }

    private bool HasOutputFolder()
    {
        return !string.IsNullOrWhiteSpace(OutputFolderPathTextBox.Text);
    }

    private FileOperationMode GetOperationMode()
    {
        return CopyModeRadioButton.IsChecked == true ? FileOperationMode.Copy : FileOperationMode.Move;
    }

    private string GetOperationVerb()
    {
        return GetOperationMode() == FileOperationMode.Copy ? "Copy" : "Move";
    }

    private string CreateLogPath()
    {
        var logDirectoryPath = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectoryPath);
        var fileName = $"rename-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        return Path.Combine(logDirectoryPath, fileName);
    }

    private static void CleanupOldLogs(string logPath)
    {
        var logDirectoryPath = Path.GetDirectoryName(logPath);
        if (string.IsNullOrWhiteSpace(logDirectoryPath) || !Directory.Exists(logDirectoryPath))
        {
            return;
        }

        var logs = Directory.EnumerateFiles(logDirectoryPath, "rename-log-*.csv", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(10);

        foreach (var log in logs)
        {
            try
            {
                log.Delete();
            }
            catch
            {
                // Log cleanup should not fail the completed file operation.
            }
        }
    }

    private static void WriteLogHeader(TextWriter writer)
    {
        writer.WriteLine("Timestamp,Operation,OriginalPath,TargetPath,OriginalName,NewName,SourceDirectory,TargetDirectory,Result,Message");
    }

    private static void WriteLogRow(
        TextWriter writer,
        FileOperationMode operationMode,
        RenameItem item,
        string result,
        string message)
    {
        var values = new[]
        {
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            operationMode.ToString(),
            item.FilePath,
            item.TargetPath,
            item.OriginalName,
            item.NewName,
            item.DirectoryPath,
            item.TargetDirectoryPath,
            result,
            message
        };

        writer.WriteLine(string.Join(",", values.Select(EscapeCsvValue)));
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static RenameItem CreateRenameItem(string filePath, string outputFolderPath, HashSet<string> reservedTargets)
    {
        var directoryPath = Path.GetDirectoryName(filePath) ?? string.Empty;
        var originalName = Path.GetFileName(filePath);
        var originalStem = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var targetDirectoryPath = string.IsNullOrWhiteSpace(outputFolderPath)
            ? directoryPath
            : outputFolderPath;

        if (!TryBuildNewStem(originalStem, out var parsedName))
        {
            return RenameItem.Skipped(filePath, originalName, directoryPath, targetDirectoryPath, "No date found");
        }

        var targetName = parsedName.Stem + extension;
        var targetPath = Path.Combine(targetDirectoryPath, targetName);
        if (PathsEqual(filePath, targetPath))
        {
            return RenameItem.Skipped(filePath, originalName, directoryPath, targetDirectoryPath, "Already clean");
        }

        var uniqueTargetPath = MakeUniqueTargetPath(targetDirectoryPath, parsedName.Stem, extension, filePath, reservedTargets);
        var uniqueTargetName = Path.GetFileName(uniqueTargetPath);
        var status = uniqueTargetName.Equals(targetName, StringComparison.OrdinalIgnoreCase)
            ? "Ready"
            : "Ready, numbered";

        return RenameItem.Ready(
            filePath,
            originalName,
            uniqueTargetName,
            uniqueTargetPath,
            directoryPath,
            Path.GetDirectoryName(uniqueTargetPath) ?? targetDirectoryPath,
            status);
    }

    private static bool TryBuildNewStem(string originalStem, out ParsedName parsedName)
    {
        if (TryBuildNvidiaReplayStem(originalStem, out parsedName))
        {
            return true;
        }

        parsedName = default;
        var dateMatch = DateRegex.Match(originalStem);
        if (!dateMatch.Success || !TryGetDate(dateMatch, out var date))
        {
            return false;
        }

        var tail = originalStem[(dateMatch.Index + dateMatch.Length)..];
        if (TryFindTime(tail, out var hour, out var minute, out var second))
        {
            parsedName = new ParsedName($"{date:yyyy-MM-dd}_{hour:00}-{minute:00}-{second:00}");
            return true;
        }

        parsedName = new ParsedName($"{date:yyyy-MM-dd}");
        return true;
    }

    private static bool TryBuildNvidiaReplayStem(string originalStem, out ParsedName parsedName)
    {
        parsedName = default;

        var match = NvidiaReplayRegex.Match(originalStem);
        if (!match.Success || !TryGetDate(match, out var date))
        {
            return false;
        }

        var hour = int.Parse(match.Groups["hour"].Value);
        var minute = int.Parse(match.Groups["minute"].Value);
        var second = int.Parse(match.Groups["second"].Value);
        parsedName = new ParsedName($"{date:yyyy-MM-dd}_{hour:00}-{minute:00}-{second:00}");
        return true;
    }

    private static bool TryGetDate(Match match, out DateTime date)
    {
        var year = int.Parse(match.Groups["year"].Value);
        var month = int.Parse(match.Groups["month"].Value);
        var day = int.Parse(match.Groups["day"].Value);

        try
        {
            date = new DateTime(year, month, day);
            return true;
        }
        catch
        {
            date = default;
            return false;
        }
    }

    private static bool TryFindTime(string text, out int hour, out int minute, out int second)
    {
        var match = SeparatedTimeRegex.Match(text);
        if (!match.Success)
        {
            match = CompactTimeRegex.Match(text);
        }

        if (!match.Success)
        {
            hour = 0;
            minute = 0;
            second = 0;
            return false;
        }

        hour = int.Parse(match.Groups["hour"].Value);
        minute = int.Parse(match.Groups["minute"].Value);
        second = match.Groups["second"].Success ? int.Parse(match.Groups["second"].Value) : 0;
        return true;
    }

    private static string MakeUniqueTargetPath(
        string directoryPath,
        string stem,
        string extension,
        string sourcePath,
        HashSet<string> reservedTargets)
    {
        var index = 1;
        while (true)
        {
            var candidateStem = index == 1 ? stem : $"{stem}_{index:00}";
            var candidatePath = Path.Combine(directoryPath, candidateStem + extension);

            if ((PathsEqual(sourcePath, candidatePath) || !File.Exists(candidatePath)) && reservedTargets.Add(candidatePath))
            {
                return candidatePath;
            }

            index++;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ParsedName(string Stem);

    private enum FileOperationMode
    {
        Move,
        Copy
    }
}

public sealed class RenameItem : INotifyPropertyChanged
{
    private string status;
    private bool canRename;

    private RenameItem(
        string filePath,
        string originalName,
        string newName,
        string targetPath,
        string directoryPath,
        string targetDirectoryPath,
        string status,
        bool canRename)
    {
        FilePath = filePath;
        OriginalName = originalName;
        NewName = newName;
        TargetPath = targetPath;
        DirectoryPath = directoryPath;
        TargetDirectoryPath = targetDirectoryPath;
        this.status = status;
        this.canRename = canRename;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath { get; }

    public string OriginalName { get; }

    public string NewName { get; }

    public string TargetPath { get; }

    public string DirectoryPath { get; }

    public string TargetDirectoryPath { get; }

    public string Status
    {
        get => status;
        set => SetField(ref status, value);
    }

    public bool CanRename
    {
        get => canRename;
        set => SetField(ref canRename, value);
    }

    public static RenameItem Ready(
        string filePath,
        string originalName,
        string newName,
        string targetPath,
        string directoryPath,
        string targetDirectoryPath,
        string status)
    {
        return new RenameItem(
            filePath,
            originalName,
            newName,
            targetPath,
            directoryPath,
            targetDirectoryPath,
            status,
            true);
    }

    public static RenameItem Skipped(
        string filePath,
        string originalName,
        string directoryPath,
        string targetDirectoryPath,
        string status)
    {
        return new RenameItem(
            filePath,
            originalName,
            originalName,
            filePath,
            directoryPath,
            targetDirectoryPath,
            status,
            false);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
