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

namespace VSCodeGH
{
    /// <summary>
    /// VSCode-Grasshopper連携プラグイン
    /// WebSocketを使用してVSCodeとGrasshopperのスクリプトコンポーネントを同期
    /// </summary>
    public class VSCodeGH : GH_AssemblyPriority
    {
        private static WebSocketServer _server;
        private const int PORT = 8080;

        public override GH_LoadingInstruction PriorityLoad()
        {
            StartWebSocketServer();
            RhinoApp.WriteLine($"VSCode-Grasshopper Integration Plugin loaded on port {PORT}");

            // アプリケーション終了時のクリーンアップ処理を登録
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                if (_server != null)
                {
                    _server.Stop();
                    RhinoApp.WriteLine("VSCode-Grasshopper Integration Plugin: WebSocket server stopped");
                }
            };

            return GH_LoadingInstruction.Proceed;
        }

        /// <summary>
        /// WebSocketサーバーを起動し、クライアントからの接続を待機
        /// </summary>
        private static void StartWebSocketServer()
        {
            try
            {
                _server = new WebSocketServer($"ws://localhost:{PORT}");
                _server.AddWebSocketService<VSCodeGHService>("/");
                _server.Start();
                RhinoApp.WriteLine($"WebSocket server started on ws://localhost:{PORT}");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting WebSocket server: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// WebSocketサービスの実装
    /// VSCodeとの通信を処理
    /// </summary>
    public class VSCodeGHService : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<JObject>(e.Data);
                var messageType = message["type"]?.ToString();

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
        /// </summary>
        private void HandleSetScript(JObject message)
        {
            var targetGuid = message["target"]?.ToString();
            var code = message["code"]?.ToString();
            var language = message["language"]?.ToString();

            if (string.IsNullOrEmpty(code))
            {
                SendError("Code content is empty");
                return;
            }

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
        /// </summary>
        private void UpdateScriptComponent(string targetGuid, string code)
        {
            var ghCanvas = Instances.ActiveCanvas;
            if (ghCanvas?.Document == null)
            {
                SendError("No active Grasshopper document");
                return;
            }

            var updated = false;
            foreach (var obj in ghCanvas.Document.Objects)
            {
                // GUIDが指定されている場合は一致するもののみ処理
                if (!string.IsNullOrEmpty(targetGuid) && obj.InstanceGuid.ToString() != targetGuid)
                    continue;

                var typeFullName = obj.GetType().FullName;
                if (typeFullName.Contains("ScriptComponent") || typeFullName.Contains("CSharpComponent"))
                {
                    try
                    {
                        // SetSourceメソッドを試行
                        var setSourceMethod = obj.GetType().GetMethod("SetSource");
                        if (setSourceMethod != null)
                        {
                            setSourceMethod.Invoke(obj, new object[] { code });
                        }
                        else
                        {
                            // Textプロパティを試行
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

                        // コンポーネントの再計算
                        obj.Attributes.ExpireLayout();
                        obj.ExpireSolution(true);
                        updated = true;

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

            if (!updated)
            {
                SendError($"No matching script component found{(string.IsNullOrEmpty(targetGuid) ? "" : $" for GUID: {targetGuid}")}");
            }
        }

        /// <summary>
        /// エラーメッセージの送信
        /// </summary>
        private void SendError(string message)
        {
            Send(JsonConvert.SerializeObject(new
            {
                type = "error",
                message = message
            }));
        }

        protected override void OnOpen()
        {
            RhinoApp.WriteLine("VSCode client connected");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            RhinoApp.WriteLine("VSCode client disconnected");
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            RhinoApp.WriteLine($"WebSocket error: {e.Message}");
        }
    }
}