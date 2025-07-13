# ToSingleFile

<div align="center">
    <img src="https://count.getloli.com/get/@ToSingleFileCSharp?theme=asoul&padding=4" alt="Visitor count"><br>
</div>

**ToSingleFile** is a powerful C# utility that combines multiple source code files from a directory structure into a single output file. This can be useful for getting the codebase into a single file for analysis using LLM. Also perfect for creating submissions, simplifying code reviews, or consolidating project artifacts.

## Features
* **Extension Filtering**: Combine specific file types (`.cs`, `.js`, etc.)
* **Exclusion Patterns**: Skip files containing specific keywords (e.g., `Designer`, `AssemblyInfo`)
* **Progress Visualization**: Real-time progress bar during processing
* **Directory Traversal**: Recursive file discovery
* **Relative Path Annotations**: Preserves original file locations as comments
* **Performance Metrics**: Reports processing time, line counts, and file sizes
* **Cross-Platform**: Works on Windows, Linux, and macOS

## Requirements
* .NET 5+ Runtime

## Installation
```bash
git clone https://github.com/2dameneko/ToSingleFileCSharp
cd ToSingleFile
dotnet build
```
or
```
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true /p:EnableCompressionInSingleFile=true
```

## Usage
```bash
ToSingleFile [options]
```

### Basic Examples
Combine all C# files in current directory:
```bash
ToSingleFile
```

Combine Python files in specific directory:
```bash
ToSingleFile -i ./src -e .py -o combined.py
```

Combine JavaScript files excluding tests:
```bash
ToSingleFile -e .js -x "test,spec"
```

## Command Line Options
| Argument | Alias | Description | Default |
|----------|-------|-------------|---------|
| `--input` | `-i` | Input directory | Current directory |
| `--output` | `-o` | Output filename | `Combined.cs` |
| `--extension` | `-e` | File extension to include | `.cs` |
| `--exclude` | `-x` | Comma-separated exclude patterns | `Designer,AssemblyInfo,Debug,Release` |
| `--help` | `-?` | Show help | N/A |

## Output Example
```cs
// File: Services\Logger.cs
public class Logger {
    public void Log(string message) { ... }
}

// File: Models\User.cs
public class User {
    public string Name { get; set; }
}
```

## Statistics Report
After processing, you'll see:
```
Successfully combined 42 .cs files into:
/home/projects/Combined.cs

Statistics:
  Resulting file size: 1.24 MB
  Files processed: 42
  Total lines: 15,328
  Directories processed: 8
  Processing time: 0.87 seconds
```

## Version History
* **0.1** (Initial Release):
  - Core combining functionality
  - Exclusion patterns
  - Performance metrics

## Important Notes
1. Always back up your code before combining
2. Output file is overwritten without warning
3. Large projects (>10,000 files) may require significant memory
4. Binary files are not supported

## License
[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
