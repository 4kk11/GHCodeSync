using System;
using System.IO;
using System.Xml.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;

namespace GHCodeSync.Managers
{
    /// <summary>
    /// 一時ファイルの管理を担当するクラス
    /// </summary>
    public class FileManager
    {
        private const string TEMP_DIR_NAME = "gh-codesync";

        /// <summary>
        /// コンポーネントの情報を保持する構造体
        /// </summary>
        public struct ComponentInfo
        {
            public string Guid { get; set; }
            public string SourceCode { get; set; }
            public string TempDirectory { get; set; }
        }

        /// <summary>
        /// コンポーネントに関連する一時ファイルを準備
        /// </summary>
        public ComponentInfo? PrepareComponentFiles(IGH_DocumentObject component)
        {
            try
            {
                // コンポーネントの情報を取得
                var guid = component.InstanceGuid.ToString();
                var sourceCode = GetComponentSource(component);
                if (string.IsNullOrEmpty(sourceCode))
                {
                    RhinoApp.WriteLine("GHCodeSync: Failed to get component source code");
                    return null;
                }

                // 一時ディレクトリを準備
                var tempDir = Path.Combine(Path.GetTempPath(), TEMP_DIR_NAME);
                Directory.CreateDirectory(tempDir);

                // プロジェクトファイルを作成（ソースコードからNuGet参照を抽出）
                CreateProjectFile(tempDir, sourceCode);

                // ソースコードファイルを作成
                var wrappedCode = IdeCodeTransformer.InjectForVSCode(sourceCode, guid);
                var sourceFile = Path.Combine(tempDir, $"{guid}.cs");
                File.WriteAllText(sourceFile, wrappedCode);

                // 接続用のコマンドファイルを作成
                var connectScript = $@"{{""command"": ""GHCodeSync.connect"", ""guid"": ""{guid}""}}";
                File.WriteAllText(Path.Combine(tempDir, "connect.cmd"), connectScript);

                // CLAUDE.mdファイルを作成
                CreateClaudeMarkdown(tempDir);

                return new ComponentInfo
                {
                    Guid = guid,
                    SourceCode = sourceCode,
                    TempDirectory = tempDir
                };
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"GHCodeSync: Error preparing component files: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// コンポーネントのソースコードを取得
        /// </summary>
        private string GetComponentSource(IGH_DocumentObject component)
        {
            var methodInfo = component.GetType().GetMethod("TryGetSource");
            if (methodInfo != null)
            {
                var parameters = new object[] { null };
                var result = (bool)methodInfo.Invoke(component, parameters);
                if (result)
                {
                    return (string)parameters[0];
                }
            }
            return null;
        }

        /// <summary>
        /// プロジェクトファイルを作成
        /// </summary>
        private void CreateProjectFile(string directory, string sourceCode)
        {
            // パッケージ参照を格納するリスト
            var packageReferences = new List<XElement>
            {
                // デフォルトのパッケージ参照
                new XElement("PackageReference",
                    new XAttribute("Include", "RhinoCommon"),
                    new XAttribute("Version", "8.18.25100.11001")
                ),
                new XElement("PackageReference",
                    new XAttribute("Include", "Grasshopper"),
                    new XAttribute("Version", "8.18.25100.11001")
                )
            };

            // ソースコードから#r directivesを抽出
            var nugetPattern = @"#r\s+""nuget:\s*([^,""]+)(?:\s*,\s*([^""]+))?""";
            var matches = Regex.Matches(sourceCode, nugetPattern);

            foreach (Match match in matches)
            {
                var packageName = match.Groups[1].Value.Trim();
                var packageVersion = match.Groups[2].Success ? match.Groups[2].Value.Trim() : "*";

                // 既存のパッケージでないか確認
                bool exists = false;
                foreach (var existingRef in packageReferences)
                {
                    if (existingRef.Attribute("Include")?.Value == packageName)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    var packageRef = new XElement("PackageReference",
                        new XAttribute("Include", packageName),
                        new XAttribute("Version", packageVersion)
                    );
                    packageReferences.Add(packageRef);
                }
            }

            var csproj = new XDocument(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", "net7.0"),
                        new XElement("LangVersion", "latest"),
                        new XElement("AllowUnsafeBlocks", "true")
                    ),
                    new XElement("ItemGroup", packageReferences),
                    new XElement("ItemGroup",
                        new XElement("Compile", new XAttribute("Include", "*.cs"))
                    )
                )
            );
            csproj.Save(Path.Combine(directory, "gh_component.csproj"));
        }

        /// <summary>
        /// CLAUDE.mdファイルを作成
        /// </summary>
        private void CreateClaudeMarkdown(string directory)
        {
            var claudeContent = @"# Grasshopper C# Script Component Development

## Overview
This temporary directory is for Grasshopper C# script component development. Code edited in VSCode is automatically sent to the Grasshopper C# script component and reflected in real-time.

## File Structure
- `*.cs` - C# script component source code
- `gh_component.csproj` - Visual Studio project file containing NuGet references
- `connect.cmd` - Connection command file
- `CLAUDE.md` - This file (development guide)

## How It Works
1. Edit and save C# files in VSCode
2. VSCode extension detects file changes
3. Code is sent to Grasshopper plugin via WebSocket
4. Code is reflected in Grasshopper's C# script component
5. Grasshopper document is automatically updated

## Allowed Edits
Only the following edits are permitted:
- **using section**: Namespace imports
- **NuGet statements**: Package references (see details below)
- **RunScript function arguments**: Input parameter definitions
- **RunScript function implementation**: Main logic implementation
- **New function implementations**: Helper functions and methods
- **New class implementations**: Supporting classes and structures

## Important Constraints
⚠️ **Do not make edits other than those allowed**
⚠️ **One .cs file corresponds to one C# script component**
⚠️ **All implementation must be contained within a single .cs file**
⚠️ **Defining classes or functions in separate files is not permitted**

## How to Use NuGet Packages
When using NuGet packages, describe them as comments in the following format:

```csharp
// #r ""nuget: RestSharp, 106.11.7""
// #r ""nuget: Newtonsoft.Json""  // Version can be omitted
```

**Important**: Always use WebSearch to find the appropriate version of NuGet packages before using them.

When you save the file:
1. PackageReference is automatically added to the .csproj file
2. Comments are removed and reflected in the Grasshopper C# script component

## Development Best Practices
- Utilize IntelliSense to improve coding efficiency
- Keep NuGet packages to a minimum
- Write code with emphasis on readability
- Appropriately divide complex logic into functions
- Add appropriate comments to clarify code intent
";
            File.WriteAllText(Path.Combine(directory, "CLAUDE.md"), claudeContent);
        }

        /// <summary>
        /// 一時ファイルを全て削除
        /// </summary>
        public void CleanupFiles()
        {
            Console.WriteLine("Cleaning up temporary files...");
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), TEMP_DIR_NAME);
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                    RhinoApp.WriteLine("GHCodeSync: Temporary files cleaned up successfully");
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"GHCodeSync: Error cleaning up temporary files: {ex.Message}");
            }
        }
    }
}