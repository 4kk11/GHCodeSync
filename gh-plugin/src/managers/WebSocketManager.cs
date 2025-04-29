using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using Rhino;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GHCodeSync.Managers
{
    /// <summary>
    /// WebSocketサーバーとクライアントとの通信を管理するクラス
    /// </summary>
    public class WebSocketManager
    {
        private WebSocketServer _server;
        private const int PORT = 51234;
        private readonly ScriptComponentManager _scriptManager;

        public WebSocketManager(ScriptComponentManager scriptManager)
        {
            _scriptManager = scriptManager;
        }

        /// <summary>
        /// WebSocketサーバーを起動
        /// </summary>
        public void StartServer()
        {
            try
            {
                _server = new WebSocketServer($"ws://localhost:{PORT}");
                _server.AddWebSocketService<WebSocketMessageHandler>("/", () => new WebSocketMessageHandler(_scriptManager));
                _server.Start();
                RhinoApp.WriteLine($"GHCodeSync: WebSocket server started on ws://localhost:{PORT}");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"GHCodeSync: Error starting WebSocket server: {ex.Message}");
            }
        }

        /// <summary>
        /// WebSocketサーバーを停止
        /// </summary>
        public void StopServer()
        {
            if (_server != null)
            {
                _server.Stop();
                RhinoApp.WriteLine("GHCodeSync: WebSocket server stopped");
            }
        }
    }

    /// <summary>
    /// WebSocketメッセージを処理するハンドラクラス
    /// </summary>
    public class WebSocketMessageHandler : WebSocketBehavior
    {
        private readonly ScriptComponentManager _scriptManager;

        public WebSocketMessageHandler(ScriptComponentManager scriptManager)
        {
            _scriptManager = scriptManager;
        }

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
                    case "healthCheck":
                        Send(JsonConvert.SerializeObject(new
                        {
                            type = "healthCheck",
                            status = "ok"
                        }));
                        break;
                    default:
                        RhinoApp.WriteLine($"GHCodeSync: Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"GHCodeSync: Error handling message: {ex.Message}");
                SendError($"Failed to process message: {ex.Message}");
            }
        }

        private void HandleSetScript(JObject message)
        {
            var targetGuid = message["target"]?.ToString();
            var code = message["code"]?.ToString();

            if (string.IsNullOrEmpty(code))
            {
                SendError("Code content is empty");
                return;
            }

            try
            {
                _scriptManager.UpdateScriptComponent(targetGuid, code, (success, error) =>
                {
                    if (success)
                    {
                        Send(JsonConvert.SerializeObject(new
                        {
                            type = "scriptUpdated",
                            target = targetGuid,
                            status = "success"
                        }));
                    }
                    else
                    {
                        SendError(error);
                    }
                });
            }
            catch (Exception ex)
            {
                SendError($"Failed to update script: {ex.Message}");
            }
        }

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
            RhinoApp.WriteLine("GHCodeSync: VSCode client connected");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            RhinoApp.WriteLine("GHCodeSync: VSCode client disconnected");
        }

        protected override void OnError(ErrorEventArgs e)
        {
            RhinoApp.WriteLine($"GHCodeSync: WebSocket error: {e.Message}");
        }
    }
}