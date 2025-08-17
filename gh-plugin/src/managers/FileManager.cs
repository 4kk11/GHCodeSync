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
                        new XElement("TargetFramework", "net48"),
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

## 概要
この一時ディレクトリはGrasshopperのC#スクリプトコンポーネント開発用です。VSCodeで編集したコードは自動的にGrasshopperのC#スクリプトコンポーネントに送信され、リアルタイムで反映されます。

## ファイル構成
- `*.cs` - C#スクリプトコンポーネントのソースコード
- `gh_component.csproj` - NuGet参照を含むVisual Studioプロジェクトファイル
- `connect.cmd` - 接続用コマンドファイル
- `CLAUDE.md` - このファイル（開発ガイド）

## 仕組み
1. VSCodeでC#ファイルを編集・保存
2. VSCode拡張がファイル変更を検知
3. コードがWebSocket経由でGrasshopperプラグインに送信
4. GrasshopperのC#スクリプトコンポーネントにコードが反映
5. Grasshopperのドキュメントが自動更新

## 許可された編集
以下の編集のみが許可されています：
- **usingセクション**: 名前空間のインポート
- **NuGetセンテンス**: パッケージ参照（詳細は下記参照）
- **RunScript関数の引数**: 入力パラメータの定義
- **RunScript関数の実装**: メインロジックの実装
- **新しい関数の実装**: ヘルパー関数やメソッド
- **新しいクラスの実装**: 補助クラスや構造体

## 重要な制約
⚠️ **許可された編集以外は行わないでください**
⚠️ **一つの.csファイルが一つのC#スクリプトコンポーネントに対応します**
⚠️ **すべての実装は一つの.csファイル内に収める必要があります**
⚠️ **クラスや関数を別ファイルに定義することは許可されていません**

## NuGetパッケージの使用方法
NuGetパッケージを使用する場合は、以下の形式でコメントアウトとして記述してください：

```csharp
// #r ""nuget: RestSharp, 106.11.7""
// #r ""nuget: Newtonsoft.Json""  // バージョン省略可能
```

ファイルを保存すると：
1. 自動的に.csprojファイルにPackageReferenceが追加されます
2. コメントアウトが削除されてGrasshopperのC#スクリプトコンポーネントに反映されます

## 開発のベストプラクティス
- IntelliSenseを活用してコーディング効率を向上させてください
- NuGetパッケージは必要最小限に留めてください
- コードは可読性を重視して記述してください
- 複雑なロジックは適切に関数分割してください
- コメントを適切に追加して、コードの意図を明確にしてください
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