using System;
using Rhino;
using Grasshopper.Kernel;
using GHCodeSync.Managers;

namespace GHCodeSync
{
    /// <summary>
    /// VSCode-Grasshopper連携プラグイン
    /// - WebSocket通信を使用してVSCodeとGrasshopperのスクリプトコンポーネントを同期
    /// - スクリプトの双方向同期を実現
    /// </summary>
    public class GHCodeSync : GH_AssemblyPriority
    {
        private WebSocketManager _webSocketManager;
        private UIManager _uiManager;
        private ScriptComponentManager _scriptManager;
        private FileManager _fileManager;

        /// <summary>
        /// プラグインのロード時に呼び出されるメソッド
        /// - 各マネージャーの初期化
        /// - WebSocketサーバーの起動
        /// - シャットダウン時のクリーンアップ処理の登録
        /// </summary>
        public override GH_LoadingInstruction PriorityLoad()
        {
            InitializeManagers();
            RhinoApp.WriteLine("VSCode-Grasshopper Integration Plugin loaded");

            // アプリケーション終了時のクリーンアップ処理を登録
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                _webSocketManager?.StopServer();
                _fileManager?.CleanupFiles();
                RhinoApp.WriteLine("VSCode-Grasshopper Integration Plugin: Cleanup completed");
            };

            return GH_LoadingInstruction.Proceed;
        }

        /// <summary>
        /// 各マネージャーの初期化
        /// </summary>
        private void InitializeManagers()
        {
            try
            {
                _fileManager = new FileManager();
                _scriptManager = new ScriptComponentManager();
                _uiManager = new UIManager(_fileManager);
                _webSocketManager = new WebSocketManager(_scriptManager);
                
                // WebSocketサーバーを起動
                _webSocketManager.StartServer();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error initializing managers: {ex.Message}");
            }
        }
    }
}