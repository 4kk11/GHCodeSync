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

        private static readonly string DummyMembersRegion = @"
    #region DummyMembers
    // Dummy implementation for VSCode IntelliSense (not touch this region)
    RhinoDoc RhinoDocument;
    GH_Document GrasshopperDocument;
    IGH_Component Component;
    int Iteration;
    public override void InvokeRunScript(IGH_Component owner,
                                        object rhinoDocument,
                                        int iteration,
                                        List<object> inputs,
                                        IGH_DataAccess DA)
    {
        throw new NotImplementedException();
    }
    private void Print(string text) { throw new NotImplementedException(); }
    private void Print(string format, params object[] args) { throw new NotImplementedException(); }
    private void Reflect(object obj) { throw new NotImplementedException(); }
    private void Reflect(object obj, string method_name) { throw new NotImplementedException(); }
    #endregion
        ";

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
                    RhinoApp.WriteLine("Failed to get component source code");
                    return null;
                }

                // 一時ディレクトリを準備
                var tempDir = Path.Combine(Path.GetTempPath(), TEMP_DIR_NAME);
                Directory.CreateDirectory(tempDir);

                // プロジェクトファイルを作成
                CreateProjectFile(tempDir);

                // ソースコードファイルを作成
                var wrappedCode = InjectForVSCode(sourceCode, guid);
                var sourceFile = Path.Combine(tempDir, $"{guid}.cs");
                File.WriteAllText(sourceFile, wrappedCode);

                // 接続用のコマンドファイルを作成
                var connectScript = $@"{{""command"": ""vscode-grasshopper.connect"", ""guid"": ""{guid}""}}";
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
                RhinoApp.WriteLine($"Error preparing component files: {ex.Message}");
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
        /// VSCode用のコード修正
        /// </summary>
        private string InjectForVSCode(string rawCode, string guid)
        {
            // 名前空間でラップ
            string code = WrapWithNamespace(rawCode, guid);

            // DummyMembersの注入
            const string classMarker = "public class Script_Instance";
            int idx = code.IndexOf(classMarker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                int brace = code.IndexOf('{', idx);
                if (brace > 0)
                {
                    code = code.Insert(brace + 1, DummyMembersRegion);
                }
            }
            return code;
        }

        /// <summary>
        /// 名前空間でコードをラップ
        /// </summary>
        private string WrapWithNamespace(string code, string guid)
        {
            var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int insertAt = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].TrimStart().StartsWith("using "))
                    insertAt = i;

            string ns = $"GH_Scripts_{guid.Replace('-', '_')}";

            var sb = new System.Text.StringBuilder();

            for (int i = 0; i <= insertAt; i++)
                sb.AppendLine(lines[i]);

            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();

            for (int i = insertAt + 1; i < lines.Length; i++)
                sb.AppendLine(lines[i]);

            return sb.ToString();
        }
    }
}