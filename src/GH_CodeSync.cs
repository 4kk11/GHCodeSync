using System;
using System.IO;
using System.Linq;
using Rhino;
using Grasshopper;
using Grasshopper.Kernel;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI;
using System.Windows.Forms;

namespace GH_CodeSync
{
    /// <summary>
    /// VSCode-Grasshopper連携プラグイン
    /// - WebSocketを使用してVSCodeとGrasshopperのスクリプトコンポーネントを同期
    /// - ポート8080でVSCodeクライアントからの接続を待機
    /// - スクリプトの双方向同期を実現
    /// </summary>
    public class GH_CodeSync : GH_AssemblyPriority
    {
        // WebSocketサーバーのインスタンス
        private static WebSocketServer _server;
        // WebSocketサーバーが使用するポート番号
        private const int PORT = 8080;

        private const string BUTTON_NAME = "VSCode Sync";

        /// <summary>
        /// プラグインのロード時に呼び出されるメソッド
        /// - WebSocketサーバーの起動
        /// - シャットダウン時のクリーンアップ処理の登録
        /// </summary>
        public override GH_LoadingInstruction PriorityLoad()
        {
            StartWebSocketServer();
            RhinoApp.WriteLine($"VSCode-Grasshopper Integration Plugin loaded on port {PORT}");

            // アプリケーション終了時のクリーンアップ処理を登録
            // WebSocketサーバーの適切な終了を保証
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                if (_server != null)
                {
                    _server.Stop();
                    RhinoApp.WriteLine("VSCode-Grasshopper Integration Plugin: WebSocket server stopped");
                }
            };

            // Grasshopperのキャンバス作成イベントにハンドラを登録
            Instances.CanvasCreated += Instances_CanvasCreated;

            return GH_LoadingInstruction.Proceed;
        }

        /// <summary>
        /// WebSocketサーバーを起動し、クライアントからの接続を待機
        /// - localhost:8080でリッスン
        /// - GH_CodeSyncServiceをWebSocketハンドラとして登録
        /// </summary>
        private static void StartWebSocketServer()
        {
            try
            {
                _server = new WebSocketServer($"ws://localhost:{PORT}");
                _server.AddWebSocketService<GH_CodeSyncService>("/");
                _server.Start();
                RhinoApp.WriteLine($"WebSocket server started on ws://localhost:{PORT}");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting WebSocket server: {ex.Message}");
            }
        }

        private void Instances_CanvasCreated(GH_Canvas canvas)
        {
            Instances.CanvasCreated -= Instances_CanvasCreated;
            canvas.MouseDown += Canvas_MouseDown;
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            // マウスクリックイベントの処理
            if (e.Button == MouseButtons.Left)
            {
                var ghdoc = Instances.ActiveCanvas.Document;
                if (ghdoc == null) return;
                var selectedObjects = ghdoc.SelectedObjects();
                if (selectedObjects.Count() == 1 && selectedObjects.First().GetType().FullName.Contains("CSharpComponent"))
                {
                    this.TryAddVSCodeSyncButton();
                }
                else
                {
                    this.TryDeleteVSCodeSyncButton();
                }
            }
        }

        private bool TryAddVSCodeSyncButton()
        {
            // すでにボタンが存在する場合は何もしない
            if (FindToolStripButton(BUTTON_NAME) != null) return false;

            // GrasshopperのUIにVSCode Syncボタンを追加
            ToolStrip canvasToolbar = Instances.DocumentEditor.Controls[0].Controls[1] as ToolStrip;
            if (canvasToolbar != null)
            {
                ToolStripButton button = new ToolStripButton(BUTTON_NAME);
                button.Click += (s, args) =>
                {
                    // VSCodeを起動してC#スクリプトコンポーネントと接続
                    var ghdoc = Instances.ActiveCanvas.Document;
                    var selectedObjects = ghdoc.SelectedObjects();
                    if (selectedObjects.Count() == 1 && selectedObjects.First().GetType().FullName.Contains("CSharpComponent"))
                    {
                        // コンポーネントの情報を取得
                        var component = selectedObjects.First();
                        var guid = component.InstanceGuid.ToString();
                        var language = "csharp";

                        // TryGetSourceメソッドをリフレクションで呼び出し
                        var methodInfo = component.GetType().GetMethod("TryGetSource");
                        string sourceCode = null;
                        if (methodInfo != null)
                        {
                            var parameters = new object[] { null };
                            var result = (bool)methodInfo.Invoke(component, parameters);
                            if (result)
                            {
                                sourceCode = (string)parameters[0];
                            }
                        }
                        
                        // 一時ディレクトリ準備
                        var tempDir = Path.Combine(Path.GetTempPath(), "gh-codesync");
                        Directory.CreateDirectory(tempDir);
                        
                        // ソースコードを一時ファイルに保存
                        var tempFile = Path.Combine(tempDir, $"{guid}.cs");
                        File.WriteAllText(tempFile, sourceCode ?? "// Empty component");

                        // WebSocket接続用のconnect.cmdファイルを作成
                        var connectScript = $@"{{""command"": ""vscode-grasshopper.connect"", ""guid"": ""{guid}""}}";
                        File.WriteAllText(Path.Combine(tempDir, "connect.cmd"), connectScript);
                        
                        // OSに依存しないURLスキーム起動方法
                        var url = $"vscode://file/{tempFile.Replace(" ", "%20")}?target={guid}&language={language}";
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                };
                canvasToolbar.Items.Add(button);
                return true;
            }
            return false;
        }

        private bool TryDeleteVSCodeSyncButton()
        {
            // GrasshopperのUIからVSCode Syncボタンを削除
            var button = FindToolStripButton(BUTTON_NAME);
            if (button != null)
            {
                ToolStrip canvasToolbar = Instances.DocumentEditor.Controls[0].Controls[1] as ToolStrip;
                canvasToolbar.Items.Remove(button);
                return true;
            }
            
            return false;
        }

        private static ToolStripButton FindToolStripButton(string name)
        {
            // GrasshopperのUIから指定された名前のToolStripButtonを検索
            ToolStrip canvasToolbar = Instances.DocumentEditor.Controls[0].Controls[1] as ToolStrip;
            if (canvasToolbar != null)
            {
                return canvasToolbar.Items.OfType<ToolStripButton>().FirstOrDefault(b => b.Text == name);
            }
            return null;
        }
    }

    /// <summary>
    /// WebSocketサービスの実装
    /// - VSCodeクライアントとの通信を処理
    /// - メッセージの受信と適切なハンドリング
    /// - スクリプトコンポーネントの更新処理
    /// - エラー通知の管理
    /// </summary>
    public class GH_CodeSyncService : WebSocketBehavior
    {
        /// <summary>
        /// WebSocketメッセージ受信時の処理
        /// メッセージフォーマット：
        /// {
        ///   "type": "setScript",
        ///   "target": "コンポーネントのGUID",
        ///   "code": "更新するスクリプト内容",
        ///   "language": "csharp"
        /// }
        /// </summary>
        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                // 受信したJSONメッセージをパース
                var message = JsonConvert.DeserializeObject<JObject>(e.Data);
                var messageType = message["type"]?.ToString();
                RhinoApp.WriteLine($"Received message: {e.Data}");
                // メッセージタイプに応じた処理を実行
                switch (messageType)
                {
                    case "setScript":
                        HandleSetScript(message);
                        break;
                    default:
                        RhinoApp.WriteLine($"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error handling message: {ex.Message}");
                SendError($"Failed to process message: {ex.Message}");
            }
        }

        /// <summary>
        /// スクリプト更新メッセージの処理
        /// - メッセージからスクリプト情報を抽出
        /// - UIスレッドでスクリプトコンポーネントを更新
        /// </summary>
        /// <param name="message">受信したJSONメッセージ</param>
        private void HandleSetScript(JObject message)
        {
            // メッセージからパラメータを抽出
            var targetGuid = message["target"]?.ToString();
            var code = message["code"]?.ToString();
            var language = message["language"]?.ToString();

            // スクリプト内容の検証
            if (string.IsNullOrEmpty(code))
            {
                SendError("Code content is empty");
                return;
            }

            // UIスレッドでの更新処理
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    UpdateScriptComponent(targetGuid, code);
                }
                catch (Exception ex)
                {
                    SendError($"Failed to update script: {ex.Message}");
                }
            }));
        }

        /// <summary>
        /// スクリプトコンポーネントの更新
        /// - アクティブなGrasshopperドキュメントからコンポーネントを検索
        /// - 該当コンポーネントのスクリプトを更新
        /// - コンポーネントの再計算を実行
        /// </summary>
        /// <param name="targetGuid">更新対象のコンポーネントGUID（空の場合は全コンポーネントが対象）</param>
        /// <param name="code">更新するスクリプト内容</param>
        private void UpdateScriptComponent(string targetGuid, string code)
        {
            // アクティブなGrasshopperドキュメントの取得
            var ghCanvas = Instances.ActiveCanvas;
            if (ghCanvas?.Document == null)
            {
                SendError("No active Grasshopper document");
                return;
            }

            var updated = false;
            // ドキュメント内のすべてのコンポーネントを走査
            foreach (var obj in ghCanvas.Document.Objects)
            {
                // GUIDによるフィルタリング
                if (!string.IsNullOrEmpty(targetGuid) && obj.InstanceGuid.ToString() != targetGuid)
                    continue;

                // スクリプトコンポーネントの種類を判定
                var typeFullName = obj.GetType().FullName;
                if (typeFullName.Contains("ScriptComponent") || typeFullName.Contains("CSharpComponent"))
                {
                    try
                    {
                        // SetSourceメソッドによる更新を試行
                        var setSourceMethod = obj.GetType().GetMethod("SetSource");
                        if (setSourceMethod != null)
                        {
                            setSourceMethod.Invoke(obj, new object[] { code });
                        }
                        else
                        {
                            // Textプロパティによる更新を試行
                            var textProperty = obj.GetType().GetProperty("Text");
                            if (textProperty?.CanWrite == true)
                            {
                                textProperty.SetValue(obj, code, null);
                            }
                            else
                            {
                                continue; // このコンポーネントは更新できない
                            }
                        }

                        // コンポーネントの再計算とレイアウト更新
                        obj.Attributes.ExpireLayout();
                        obj.ExpireSolution(true);
                        updated = true;

                        // 更新成功の通知
                        Send(JsonConvert.SerializeObject(new
                        {
                            type = "scriptUpdated",
                            target = obj.InstanceGuid.ToString(),
                            status = "success"
                        }));
                    }
                    catch (Exception ex)
                    {
                        SendError($"Failed to update component {obj.InstanceGuid}: {ex.Message}");
                    }
                }
            }

            // 更新対象が見つからなかった場合
            if (!updated)
            {
                SendError($"No matching script component found{(string.IsNullOrEmpty(targetGuid) ? "" : $" for GUID: {targetGuid}")}");
            }
        }

        /// <summary>
        /// エラーメッセージの送信
        /// - クライアントへのエラー通知
        /// </summary>
        private void SendError(string message)
        {
            Send(JsonConvert.SerializeObject(new
            {
                type = "error",
                message = message
            }));
        }

        /// <summary>
        /// WebSocket接続確立時の処理
        /// </summary>
        protected override void OnOpen()
        {
            RhinoApp.WriteLine("VSCode client connected");
        }

        /// <summary>
        /// WebSocket接続切断時の処理
        /// </summary>
        protected override void OnClose(CloseEventArgs e)
        {
            RhinoApp.WriteLine("VSCode client disconnected");
        }

        /// <summary>
        /// WebSocketエラー発生時の処理
        /// </summary>
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            RhinoApp.WriteLine($"WebSocket error: {e.Message}");
        }
    }
}