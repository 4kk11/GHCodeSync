using System;
using System.IO;
using System.Xml.Linq;
using System.Reflection;
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

                // プロジェクトファイルを作成
                CreateProjectFile(tempDir);

                // ソースコードファイルを作成
                var wrappedCode = IdeCodeTransformer.InjectForVSCode(sourceCode, guid);
                var sourceFile = Path.Combine(tempDir, $"{guid}.cs");
                File.WriteAllText(sourceFile, wrappedCode);

                // 接続用のコマンドファイルを作成
                var connectScript = $@"{{""command"": ""GHCodeSync.connect"", ""guid"": ""{guid}""}}";
                File.WriteAllText(Path.Combine(tempDir, "connect.cmd"), connectScript);

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
        private void CreateProjectFile(string directory)
        {
            var csproj = new XDocument(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", "net48"),
                        new XElement("LangVersion", "latest"),
                        new XElement("AllowUnsafeBlocks", "true")
                    ),
                    new XElement("ItemGroup",
                        new XElement("PackageReference",
                            new XAttribute("Include", "RhinoCommon"),
                            new XAttribute("Version", "8.18.25100.11001")
                        ),
                        new XElement("PackageReference",
                            new XAttribute("Include", "Grasshopper"),
                            new XAttribute("Version", "8.18.25100.11001")
                        )
                    ),
                    new XElement("ItemGroup",
                        new XElement("Compile", new XAttribute("Include", "*.cs"))
                    )
                )
            );
            csproj.Save(Path.Combine(directory, "gh_component.csproj"));
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