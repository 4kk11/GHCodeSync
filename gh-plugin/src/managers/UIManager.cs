using System;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using System.Diagnostics;
using System.Drawing;

namespace GHCodeSync.Managers
{
    /// <summary>
    /// GrasshopperのUI要素を管理するクラス
    /// </summary>
    public class UIManager
    {
        private const string BUTTON_NAME = "VSCode Sync";
        private readonly FileManager _fileManager;
        public UIManager(FileManager fileManager)
        {
            _fileManager = fileManager;
            Instances.CanvasCreated += Instances_CanvasCreated;
        }

        /// <summary>
        /// キャンバス作成時のイベントハンドラ
        /// </summary>
        private void Instances_CanvasCreated(GH_Canvas canvas)
        {
            Instances.CanvasCreated -= Instances_CanvasCreated;
            this.AddToolStripSeparator();
            canvas.MouseDown += Canvas_MouseDown;
        }

        /// <summary>
        /// マウスクリックイベントの処理
        /// </summary>
        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var ghdoc = Instances.ActiveCanvas.Document;
                if (ghdoc == null) return;
                var selectedObjects = ghdoc.SelectedObjects();
                if (selectedObjects.Count() == 1 && selectedObjects.First().GetType().FullName.Contains("CSharpComponent"))
                {
                    TryAddVSCodeSyncButton();
                }
                else
                {
                    TryDeleteVSCodeSyncButton();
                }
            }
        }

        /// <summary>
        /// VSCode Syncボタンの追加を試行
        /// </summary>
        private bool TryAddVSCodeSyncButton()
        {
            if (FindToolStripButton(BUTTON_NAME) != null) return false;

            ToolStrip canvasToolbar = GetCanvasToolbar();
            if (canvasToolbar != null)
            {
                var button = new ToolStripButton();
                button.Image = EmbeddedResourceHelpers.GetEmbeddedImage("logo_24x24.png");
                button.Text = BUTTON_NAME;
                button.ToolTipText = "Open with VSCode";
                button.Click += HandleButtonClick;
                canvasToolbar.Items.Add(button);
                return true;
            }
            return false;
        }

        /// <summary>
        /// ToolStripSeparetorを追加
        /// 
        private void AddToolStripSeparator()
        {
            ToolStrip canvasToolbar = GetCanvasToolbar();
            if (canvasToolbar != null)
            {
                var separator = new ToolStripSeparator();
                canvasToolbar.Items.Add(separator);
            }
        }

        /// <summary>
        /// VSCode Syncボタンの削除を試行
        /// </summary>
        private bool TryDeleteVSCodeSyncButton()
        {
            var button = FindToolStripButton(BUTTON_NAME);
            if (button != null)
            {
                ToolStrip canvasToolbar = GetCanvasToolbar();
                canvasToolbar.Items.Remove(button);
                return true;
            }
            return false;
        }

        /// <summary>
        /// ツールバーボタンのクリックイベント処理
        /// </summary>
        private void HandleButtonClick(object sender, EventArgs e)
        {
            var ghdoc = Instances.ActiveCanvas.Document;
            var selectedObjects = ghdoc.SelectedObjects();
            if (selectedObjects.Count() != 1 || !selectedObjects.First().GetType().FullName.Contains("CSharpComponent"))
                return;

            var component = selectedObjects.First();
            var componentInfo = _fileManager.PrepareComponentFiles(component);
            if (componentInfo.HasValue)
            {
                var info = componentInfo.Value;
                if (!string.IsNullOrEmpty(info.TempDirectory))
                {
                    OpenVSCode(info.TempDirectory);
                }
            }
        }

        /// <summary>
        /// VSCodeを開く
        /// </summary>
        private void OpenVSCode(string directoryPath)
        {
            try
            {
                var url = $"vscode://file/{directoryPath.Replace(" ", "%20")}";
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine($"Error opening VSCode: {ex.Message}");
            }
        }

        /// <summary>
        /// キャンバスのツールバーを取得
        /// </summary>
        private ToolStrip GetCanvasToolbar()
        {
            return Instances.DocumentEditor.Controls[0].Controls[1] as ToolStrip;
        }

        /// <summary>
        /// 指定された名前のToolStripButtonを検索
        /// </summary>
        private static ToolStripButton FindToolStripButton(string name)
        {
            ToolStrip canvasToolbar = Instances.DocumentEditor.Controls[0].Controls[1] as ToolStrip;
            if (canvasToolbar != null)
            {
                return canvasToolbar.Items.OfType<ToolStripButton>()
                    .FirstOrDefault(b => b.Text == name);
            }
            return null;
        }
    }
}