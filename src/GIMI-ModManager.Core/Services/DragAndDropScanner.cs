﻿using System.Diagnostics;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace GIMI_ModManager.Core.Services;

// Extract process if archive file:
// 1. Copy archive to work folder in windows temp folder
// 2. Extract archive to windows temp folder
// 3. Move extracted files to work folder
// 4. Delete copied archive

public sealed class DragAndDropScanner : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<DragAndDropScanner>();
    private readonly string _tmpFolder = Path.Combine(Path.GetTempPath(), "JASM_TMP");
    private readonly string _workFolder = Path.Combine(Path.GetTempPath(), "JASM_TMP", Guid.NewGuid().ToString("N"));
    private string? tmpModFolder;

    private ExtractTool _extractTool;

    public DragAndDropScanner()
    {
        _extractTool = GetExtractTool();
    }

    public DragAndDropScanResult ScanAndGetContents(string path)
    {
        PrepareWorkFolder();

        if (IsArchive(path))
        {
            var copiedPath = Path.Combine(_workFolder, Path.GetFileName(path));
            File.Copy(path, copiedPath);
            var result = Extractor(copiedPath);
            result?.Invoke(copiedPath);
        }
        else if (Directory.Exists(path)) // ModDragAndDropService handles loose folders, but this added just in case 
        {
            var modFolder = new Mod(new DirectoryInfo(path));
            modFolder.CopyTo(_workFolder);
            tmpModFolder = modFolder.FullPath;
        }
        else
        {
            throw new Exception("No valid mod folder or archive found");
        }


        var extractedDirs = new DirectoryInfo(_workFolder).EnumerateDirectories().ToArray();
        if (extractedDirs is null || !extractedDirs.Any())
            throw new DirectoryNotFoundException("No valid mod folder found in archive. Loose files are ignored");

        var ignoredDirs = new List<DirectoryInfo>();
        if (extractedDirs.Length > 1)
            ignoredDirs.AddRange(extractedDirs.Skip(1));

        var newMod = new Mod(extractedDirs.First());

        //newMod.MoveTo(_tmpFolder);

        return new DragAndDropScanResult()
        {
            ExtractedMod = newMod,
            IgnoredMods = ignoredDirs.Select(dir => dir.Name).ToArray()
        };
    }

    private void PrepareWorkFolder()
    {
        Directory.CreateDirectory(_tmpFolder);
        Directory.CreateDirectory(_workFolder);
    }

    private bool IsArchive(string path)
    {
        return Path.GetExtension(path) switch
        {
            ".zip" => true,
            ".rar" => true,
            ".7z" => true,
            _ => false
        };
    }

    private Action<string>? Extractor(string path)
    {
        Action<string>? action = null;

        if (_extractTool == ExtractTool.Bundled7Zip)
            action = Extract7Z;
        else if (_extractTool == ExtractTool.SharpCompress)
            action = Path.GetExtension(path) switch
            {
                ".zip" => SharpExtractZip,
                ".rar" => SharpExtractRar,
                ".7z" => SharpExtract7z,
                _ => null
            };
        else if (_extractTool == ExtractTool.System7Zip) throw new NotImplementedException();

        return action;
    }

    private void ExtractEntries(IArchive archive)
    {
        _logger.Information("Extracting {ArchiveType} archive", archive.Type);
        foreach (var entry in archive.Entries)
        {
            _logger.Debug("Extracting {EntryName}", entry.Key);
            entry.WriteToDirectory(_workFolder, new ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = true,
                PreserveFileTime = false
            });
        }
    }

    private void SharpExtractZip(string path)
    {
        using var archive = ZipArchive.Open(path);
        ExtractEntries(archive);
    }


    private void SharpExtractRar(string path)
    {
        using var archive = RarArchive.Open(path);
        ExtractEntries(archive);
    }

    // ReSharper disable once InconsistentNaming
    private void SharpExtract7z(string path)
    {
        using var archive = ArchiveFactory.Open(path);
        ExtractEntries(archive);
    }

    private bool IsRootModFolder(DirectoryInfo folder)
    {
        foreach (var fileSystemInfo in folder.GetFileSystemInfos())
        {
            var extension = Path.GetExtension(fileSystemInfo.Name);
            if (extension.Equals(".ini")) return true;
        }

        return false;
    }

    public void Dispose()
    {
        Directory.Delete(_workFolder, true);
        if (tmpModFolder is not null && Path.Exists(tmpModFolder))
            Directory.Delete(tmpModFolder, true);
    }

    private enum ExtractTool
    {
        Bundled7Zip, // 7zip bundled with JASM
        SharpCompress, // SharpCompress library
        System7Zip // 7zip installed on the system
    }

    private ExtractTool GetExtractTool()
    {
        var bundled7ZFolder = Path.Combine(AppContext.BaseDirectory, @"Assets\7z\");
        if (File.Exists(Path.Combine(bundled7ZFolder, "7z.exe")) &&
            File.Exists(Path.Combine(bundled7ZFolder, "7-zip.dll")) &&
            File.Exists(Path.Combine(bundled7ZFolder, "7z.dll")))
        {
            _logger.Debug("Using bundled 7zip");
            return ExtractTool.Bundled7Zip;
        }

        _logger.Information("Bundled 7zip not found, using SharpCompress library");
        return ExtractTool.SharpCompress;
    }


    private void Extract7Z(string path)
    {
        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, @"Assets\7z\7z.exe");
        var process = new Process
        {
            StartInfo =
            {
                FileName = sevenZipPath,
                Arguments = $"x \"{path}\" -o\"{_workFolder}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        _logger.Information("Extracting 7z archive with command: {Command}", process.StartInfo.Arguments);
        process.Start();
        process.WaitForExit();
        _logger.Information("7z extraction finished with exit code {ExitCode}", process.ExitCode);
    }
}

public class DragAndDropScanResult
{
    public IMod ExtractedMod { get; init; } = null!;
    public string[] IgnoredMods { get; init; } = Array.Empty<string>();
    public string[] IgnoredFiles { get; init; } = Array.Empty<string>();
    public string? ThumbnailPath { get; set; }
}