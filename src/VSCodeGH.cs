using System;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.PlugIns;

namespace VSCodeGH
{
    public class VSCodeGH : GH_AssemblyPriority
    {

        private static FileSystemWatcher _watcher;

        // 編集したいファイル（VSCode側で編集しているパスを指定）
        // private static string _scriptFilePath = @"C:\YOURFOLDER\myscript.py"; // Pyの場合
        private static string _scriptFilePath = @"/Users/akihito/Desktop/test/test.cs"; // C#の場合

        // ScriptComponent の NickNameや GUID、あるいはインデックス（複数の場合は工夫してください）
        private static string _targetComponentName = "Hoge";
        public override GH_LoadingInstruction PriorityLoad()
        {
            SetWatcher();
            RhinoApp.WriteLine("VSCode Script Sync Plugin loaded and watcher started.");

            return GH_LoadingInstruction.Proceed;
        }

        // protected override void OnShutdown()
        // {
        //     if (_watcher != null)
        //     {
        //         _watcher.EnableRaisingEvents = false;
        //         _watcher.Dispose();
        //     }
        // }

        private static void SetWatcher()
        {
            if (!File.Exists(_scriptFilePath))
            {
                File.WriteAllText(_scriptFilePath, "# Start typing your GH script here.");
            }

            var dir = Path.GetDirectoryName(_scriptFilePath);
            var filename = Path.GetFileName(_scriptFilePath);

            _watcher = new FileSystemWatcher(dir, filename)
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (s, e) =>
            {
                // ファイル更新直後は読み込みロック等でタイミングが合わないこともあるので、少しリトライ
                RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    TryUpdateScriptComponentFromFile();
                }));
            };
        }

        // ScriptComponent or CSharpComponent にスクリプトをセット
        private static void TryUpdateScriptComponentFromFile()
        {
            try
            {
                string code = null;

                // ファイルロック問題への簡易対応
                int retry = 0;
                while (retry < 3)
                {
                    try
                    {
                        code = File.ReadAllText(_scriptFilePath);
                        break;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(50);
                        retry++;
                    }
                }

                if (code == null)
                    return;

                // GHが開いているか
                var ghCanvas = Instances.ActiveCanvas;
                if (ghCanvas == null || ghCanvas.Document == null)
                {
                    RhinoApp.WriteLine("[VSCodeSync] No active GH document.");
                    return;
                }

                // スクリプトコンポーネントをすべて取得（特定のNickNameまたはインデックスで識別も可）
                int updated = 0;
                foreach (var obj in ghCanvas.Document.Objects)
                {
                    var typeFullName = obj.GetType().FullName;
                    // ScriptComponentの判定。
                    if (typeFullName == "RhinoCodePluginGH.Components.ScriptComponent"
                     || typeFullName == "RhinoCodePluginGH.Components.CSharpComponent"
                     // 必要なら他にも
                     )
                    {
                        // NickNameで限定したい場合
                        if (!string.IsNullOrEmpty(_targetComponentName) && obj.NickName != _targetComponentName) continue;
                        var mi = obj.GetType().GetMethod("SetSource");
                        if (mi != null)
                        {
                            mi.Invoke(obj, new object[] { code });
                            obj.Attributes.ExpireLayout(); // レイアウト再描画
                            obj.ExpireSolution(true);
                            updated++;
                            RhinoApp.WriteLine($"[VSCodeSync] Updated GH ScriptComponent: {obj.NickName}");
                        }
                        else
                        {
                            // .Text プロパティを使う場合（CSharpComponentなど）
                            var pi = obj.GetType().GetProperty("Text");
                            if (pi != null && pi.CanWrite)
                            {
                                pi.SetValue(obj, code, null);
                                obj.Attributes.ExpireLayout();
                                obj.ExpireSolution(true);
                                updated++;
                                RhinoApp.WriteLine($"[VSCodeSync] Updated GH ScriptComponent (Text): {obj.NickName}");
                            }
                        }
                    }
                }
                if (updated == 0)
                {
                    RhinoApp.WriteLine("[VSCodeSync] No GH ScriptComponent found to update.");
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("[VSCodeSync] Error: " + ex.Message);
            }
        }

    }

}