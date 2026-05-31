# VideoFileRenamer

[中文 README](README_zh.md)

`VideoFileRenamer` is a Windows desktop utility for batch-cleaning video file names. It detects dates and times from existing file names, shows a rename preview, and only applies the changes after confirmation.

## ✨ Features

- Select a source folder and scan files.
- Optionally choose an output folder for renamed files.
- Optionally process only common video formats.
- Optionally include subfolders.
- Choose whether to move files or copy files after renaming.
- Normalize detected dates and times into consistent file names.
- Supports NVIDIA Replay-style file names.
- Preview original name, new name, status, and folder before renaming.
- Move or copy renamed files to the selected output folder.
- Generate a CSV operation log for each batch.
- Automatically adds numeric suffixes when target names conflict.

## 🏷️ Rename Format

When both date and time are detected:

```text
yyyy-MM-dd_HH-mm-ss.ext
```

When only a date is detected:

```text
yyyy-MM-dd.ext
```

If the target file name already exists in the rename location, a numeric suffix is added:

```text
2026-05-31_13-20-05_02.mp4
```

## 🎬 Supported Video Extensions

When "process only common video formats" is enabled, these extensions are included by default:

```text
.mp4 .mov .mkv .avi .wmv .flv .webm .m4v .mts .m2ts .ts
```

## 🧰 Requirements

- Windows
- .NET 9 SDK

## 🚀 Run

Run from the project root:

```powershell
dotnet restore
dotnet run
```

## 🔨 Build

```powershell
dotnet build
```

Publish a Release build:

```powershell
dotnet publish -c Release
```

## 🖱️ Usage

1. Start the application.
2. Choose the source folder.
3. Optionally choose an output folder if renamed files should be moved there.
4. Choose move or copy mode.
5. Enable or disable video-only filtering and subfolder scanning as needed.
6. Click preview and review the rename results.
7. Confirm and run the rename operation.

## ⚠️ Notes

- The app previews changes before renaming, but the final rename operation directly changes file names on disk.
- Move mode changes the original files.
- Copy mode keeps the original files and writes renamed copies.
- If an output folder is selected, renamed files are moved or copied into that folder.
- If no output folder is selected, renamed files are written in their original folders.
- A CSV log is written to the `logs` folder next to the executable file.
- Only the latest 10 log files are kept.
- Test with a small set of files before processing important folders.
- Files without a recognizable date are skipped.

## 📄 License

This project is licensed under the [MIT License](LICENSE).
