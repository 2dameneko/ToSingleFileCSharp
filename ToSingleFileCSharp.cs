using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ToSingleFile
{
    public sealed class CombinerConfiguration
    {
        public string InputFolder { get; }
        public string OutputFile { get; }
        public string FileExtension { get; }
        public string[] ExcludeMasks { get; }

        public CombinerConfiguration(
            string inputFolder,
            string outputFile = "Combined.cs",
            string fileExtension = ".cs",
            string[] excludeMasks = null)
        {
            InputFolder = inputFolder;
            OutputFile = outputFile;
            FileExtension = NormalizeExtension(fileExtension);
            ExcludeMasks = excludeMasks ?? new[] { "Designer", "AssemblyInfo", "Debug", "Release" };
        }

        private static string NormalizeExtension(string extension)
            => string.IsNullOrEmpty(extension) ? ".cs" :
               extension.StartsWith(".") ? extension : $".{extension}";
    }

    public sealed class FileCombiner
    {
        private readonly IFileSystem _fileSystem;
        private readonly ProgressReporter _progressReporter;

        public FileCombiner(IFileSystem fileSystem, ProgressReporter progressReporter)
        {
            _fileSystem = fileSystem;
            _progressReporter = progressReporter;
        }

        public CombineResult ProcessFiles(CombinerConfiguration config)
        {
            ValidateInputFolder(config.InputFolder);
            var outputPath = Path.GetFullPath(config.OutputFile);

            var files = GetSourceFiles(config, outputPath);
            if (files.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No {config.FileExtension} files found (after filters) in: {config.InputFolder}");
            }

            _fileSystem.DeleteIfExists(outputPath);
            return CombineFiles(files, outputPath, config);
        }

        private void ValidateInputFolder(string folderPath)
        {
            if (!_fileSystem.DirectoryExists(folderPath))
                throw new DirectoryNotFoundException($"Directory '{folderPath}' does not exist.");
        }

        private List<SourceFile> GetSourceFiles(CombinerConfiguration config, string outputPath)
        {
            var searchPattern = $"*{config.FileExtension}";
            var files = _fileSystem.GetFiles(config.InputFolder, searchPattern, SearchOption.AllDirectories);

            return files
                .Select(f => new SourceFile(f, Path.GetFullPath(f)))
                .Where(f => f.FullPath != outputPath)
                .Where(f => !config.ExcludeMasks.Any(m => f.FullPath.Contains(m)))
                .ToList();
        }

        private CombineResult CombineFiles(
            List<SourceFile> files,
            string outputPath,
            CombinerConfiguration config)
        {
            var result = new CombineResult();
            var stopwatch = Stopwatch.StartNew();

            using (var writer = _fileSystem.CreateStreamWriter(outputPath, Encoding.UTF8))
            {
                _progressReporter.Reset();
                Console.WriteLine("Combining files:");

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var content = _fileSystem.ReadAllText(file.Path, Encoding.UTF8);

                    writer.WriteLine($"// File: {GetRelativePath(file.Path, config.InputFolder)}");
                    writer.Write(content);
                    if (!content.EndsWith("\n")) writer.Write('\n');

                    result.TotalLines += CountLines(content);
                    _progressReporter.Update(i + 1, files.Count);
                }
            }

            stopwatch.Stop();
            FinalizeResult(result, files, outputPath, stopwatch.Elapsed);
            return result;
        }

        private void FinalizeResult(
            CombineResult result,
            List<SourceFile> files,
            string outputPath,
            TimeSpan elapsed)
        {
            result.OutputPath = outputPath;
            result.FileSize = _fileSystem.GetFileSize(outputPath);
            result.OriginalFileCount = files.Count;
            result.DirectoriesProcessed = files
                .Select(f => Path.GetDirectoryName(f.Path))
                .Distinct()
                .Count();
            result.ProcessingTime = elapsed;
        }

        private static int CountLines(string content)
        {
            if (string.IsNullOrEmpty(content)) return 0;

            int count = 1;
            int position = 0;
            while ((position = content.IndexOfAny(new[] { '\r', '\n' }, position)) != -1)
            {
                count++;
                if (content[position] == '\r' && position < content.Length - 1 && content[position + 1] == '\n')
                    position += 2;
                else
                    position += 1;
            }
            return count;
        }

        private static string GetRelativePath(string fullPath, string basePath)
        {
            var uri = new Uri(fullPath);
            var baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? basePath
                : basePath + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(uri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }

    public sealed class ProgressReporter
    {
        private int _lastFilledLength = -1;
        private const int ProgressWidth = 50;

        public void Update(int current, int total)
        {
            var progress = (double)current / total;
            var filledLength = (int)(progress * ProgressWidth);

            if (filledLength == _lastFilledLength) return;

            _lastFilledLength = filledLength;
            var filled = new string('=', filledLength);
            var empty = new string(' ', ProgressWidth - filledLength);
            Console.Write($"\r[{filled}{empty}] {progress:P0}");
        }

        public void Reset() => _lastFilledLength = -1;
        public void Complete() => Console.WriteLine();
    }

    public interface IFileSystem
    {
        string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
        string ReadAllText(string path, Encoding encoding);
        StreamWriter CreateStreamWriter(string path, Encoding encoding, int bufferSize = 32768);
        void DeleteIfExists(string path);
        long GetFileSize(string path);
        bool DirectoryExists(string path);
    }

    public sealed class PhysicalFileSystem : IFileSystem
    {
        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
            => Directory.GetFiles(path, searchPattern, searchOption);

        public string ReadAllText(string path, Encoding encoding)
            => File.ReadAllText(path, encoding);

        public StreamWriter CreateStreamWriter(string path, Encoding encoding, int bufferSize = 32768)
            => new StreamWriter(path, false, encoding, bufferSize);

        public void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        public long GetFileSize(string path)
            => new FileInfo(path).Length;

        public bool DirectoryExists(string path)
            => Directory.Exists(path);
    }

    public sealed class SourceFile
    {
        public string Path { get; }
        public string FullPath { get; }

        public SourceFile(string path, string fullPath)
        {
            Path = path;
            FullPath = fullPath;
        }
    }

    public sealed class CombineResult
    {
        public int TotalLines { get; set; }
        public string OutputPath { get; set; }
        public long FileSize { get; set; }
        public int OriginalFileCount { get; set; }
        public int DirectoriesProcessed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public static class ArgumentParser
    {
        public static CombinerConfiguration Parse(string[] args)
        {
            if (args.Any(IsHelpArgument))
            {
                ShowHelp();
                return null;
            }

            var folderPath = Directory.GetCurrentDirectory();
            var outputPath = "Combined.cs";
            var extension = ".cs";
            string[] excludeMasks = null;

            var argDict = ParseArguments(args);
            bool hasArguments = args.Length > 0;

            if (TryGetArgumentValue(argDict, "i", "input", out var inputValue))
                folderPath = inputValue ?? folderPath;
            else if (argDict.TryGetValue("positional", out var positionalValue))
                folderPath = positionalValue ?? folderPath;

            if (TryGetArgumentValue(argDict, "o", "output", out var outputValue))
                outputPath = outputValue ?? outputPath;

            if (TryGetArgumentValue(argDict, "e", "extension", out var extValue))
                extension = extValue ?? extension;

            if (TryGetArgumentValue(argDict, "x", "exclude", out var excludeValue))
                excludeMasks = ParseExcludeMasks(excludeValue);

            if (!hasArguments)
            {
                Console.WriteLine($"Using current directory: {folderPath}");
                Console.WriteLine($"Default output: {outputPath}");
                Console.WriteLine($"Default extension: {extension}");
                Console.WriteLine($"Default exclude-mask: {string.Join(", ", excludeMasks ?? new[]{string.Empty})}");
            }

            return new CombinerConfiguration(folderPath, outputPath, extension, excludeMasks);
        }

        private static string[] ParseExcludeMasks(string excludeValue)
            => excludeValue?
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrEmpty(m))
                .ToArray();

        private static bool TryGetArgumentValue(
            Dictionary<string, string> dict,
            string shortKey,
            string longKey,
            out string value)
        {
            if (dict.TryGetValue(shortKey, out value)) return true;
            if (dict.TryGetValue(longKey, out value)) return true;
            value = null;
            return false;
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            var argDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string currentKey = null;

            foreach (var arg in args)
            {
                if (arg.StartsWith("-") || arg.StartsWith("/"))
                {
                    currentKey = arg.TrimStart('-', '/').ToLower();
                    argDict[currentKey] = null;
                }
                else if (currentKey != null)
                {
                    argDict[currentKey] = arg;
                    currentKey = null;
                }
                else
                {
                    argDict["positional"] = arg;
                }
            }
            return argDict;
        }

        private static bool IsHelpArgument(string arg)
        {
            return arg == "--help" || arg == "/?" || arg == "?" || arg == "-?" ||
                   arg.Equals("-help", StringComparison.OrdinalIgnoreCase) ||
                   arg.Equals("--h", StringComparison.OrdinalIgnoreCase) ||
                   arg.Equals("-h", StringComparison.OrdinalIgnoreCase);
        }

        private static void ShowHelp()
        {
            Console.WriteLine("ToSingleFile - Combines source files into a single file");
            Console.WriteLine("Usage: ToSingleFile [folder-path] or ToSingleFile [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -i, --input <folder>    Input folder to process (default: current directory)");
            Console.WriteLine("  -o, --output <file>     Output file (default: Combined.cs)");
            Console.WriteLine("  -e, --extension <ext>   File extension to include (default: .cs)");
            Console.WriteLine("  -x, --exclude <masks>    Comma-separated exclude masks (default Designer, AssemblyInfo, Debug, Release)");
            Console.WriteLine("  --help, /?, -?          Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  ToSingleFile                            # Uses current directory with defaults");
            Console.WriteLine("  ToSingleFile C:\\Project                 # Positional input folder");
            Console.WriteLine("  ToSingleFile -i C:\\Project -o All.cs   # Named arguments");
            Console.WriteLine("  ToSingleFile -e .txt -x \"temp,backup\" # Custom extension and exclude masks");
            Console.WriteLine("  ToSingleFile -e .py                    # Current dir with Python files");
        }
    }

    public static class FormatUtilities
    {
        public static string FormatBytes(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblBytes = bytes;

            while (i < suffix.Length - 1 && dblBytes >= 1024)
            {
                dblBytes /= 1024;
                i++;
            }

            return $"{dblBytes:0.##} {suffix[i]}";
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var config = ArgumentParser.Parse(args);
                if (config == null) return 0; // Help shown

                var fileSystem = new PhysicalFileSystem();
                var progressReporter = new ProgressReporter();
                var combiner = new FileCombiner(fileSystem, progressReporter);

                var result = combiner.ProcessFiles(config);
                DisplayResults(result, config);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static void DisplayResults(CombineResult result, CombinerConfiguration config)
        {
            Console.WriteLine($"\nSuccessfully combined {result.OriginalFileCount} {config.FileExtension} files into:");
            Console.WriteLine(Path.GetFullPath(config.OutputFile));
            Console.WriteLine("\nStatistics:");
            Console.WriteLine($"  Resulting file size: {FormatUtilities.FormatBytes(result.FileSize)}");
            Console.WriteLine($"  Output file: {result.OutputPath}");
            Console.WriteLine($"  Files processed: {result.OriginalFileCount}");
            Console.WriteLine($"  Total lines: {result.TotalLines:N0}");
            Console.WriteLine($"  Directories processed: {result.DirectoriesProcessed}");
            Console.WriteLine($"  Processing time: {result.ProcessingTime.TotalSeconds:F2} seconds");
        }
    }
}
