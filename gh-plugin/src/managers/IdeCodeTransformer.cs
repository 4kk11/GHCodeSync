using System;

namespace GHCodeSync.Managers
{
    /// <summary>
    /// IDE用のコード変換を管理するクラス
    /// InjectForVSCode と StripIdeHelpers は対になっている関数です
    /// </summary>
    public static class IdeCodeTransformer
    {
        private const string DummyMembersRegion = @"
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
        /// VSCode用のコード修正（コードを IDE で扱いやすくする）
        /// </summary>
        public static string InjectForVSCode(string rawCode, string guid)
        {
            // 名前空間を注入
            string code = InjectNamespace(rawCode, guid);

            // DummyMembersの注入
            code = InjectDummyMembers(code);
            
            return code;
        }

        /// <summary>
        /// IDE用のヘルパーコードを除去（InjectForVSCodeの逆操作）
        /// </summary>
        public static string StripIdeHelpers(string code)
        {
            // 名前空間の除去
            code = RemoveGuidNamespace(code);
            
            // DummyMembersリージョンの除去
            code = RemoveRegion(code, "DummyMembers");

            return code;
        }

        /// <summary>
        /// DummyMembersをクラスに注入
        /// </summary>
        private static string InjectDummyMembers(string code)
        {
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
        /// 名前空間を注入
        /// </summary>
        private static string InjectNamespace(string code, string guid)
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

        /// <summary>
        /// 特定のリージョンを除去
        /// </summary>
        private static string RemoveRegion(string src, string regionName)
        {
            if (string.IsNullOrEmpty(src)) return src;

            var lines = src.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var result = new System.Text.StringBuilder();
            bool inRegion = false;

            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith($"#region {regionName}"))
                    inRegion = true;
                else if (line.TrimStart().StartsWith("#endregion") && inRegion)
                    inRegion = false;
                else if (!inRegion)
                    result.AppendLine(line);
            }

            return result.ToString();
        }

        /// <summary>
        /// GUID名前空間を除去
        /// </summary>
        private static string RemoveGuidNamespace(string src)
        {
            if (string.IsNullOrEmpty(src)) return src;

            var lines = src.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var result = new System.Text.StringBuilder();
            bool skipNext = false;

            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("namespace GH_Scripts_"))
                    skipNext = true;
                else if (skipNext && string.IsNullOrWhiteSpace(line))
                    skipNext = false;
                else if (!skipNext)
                    result.AppendLine(line);
            }

            return result.ToString();
        }
    }
}