using System;
using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino;

namespace GHCodeSync.Managers
{
    /// <summary>
    /// スクリプトコンポーネントの操作を管理するクラス
    /// </summary>
    public class ScriptComponentManager
    {
        public delegate void UpdateCallback(bool success, string error);

        /// <summary>
        /// スクリプトコンポーネントの更新
        /// </summary>
        public void UpdateScriptComponent(string targetGuid, string code, UpdateCallback callback)
        {
            // UIスレッドでの更新処理
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    var cleaned = IdeCodeTransformer.StripIdeHelpers(code);
                    var success = UpdateComponent(targetGuid, cleaned);
                    if (success)
                    {
                        callback(true, null);
                    }
                    else
                    {
                        callback(false, "No matching script component found");
                    }
                }
                catch (Exception ex)
                {
                    callback(false, ex.Message);
                }
            }));
        }

        /// <summary>
        /// コンポーネントの更新処理
        /// </summary>
        private bool UpdateComponent(string targetGuid, string code)
        {
            var ghCanvas = Instances.ActiveCanvas;
            if (ghCanvas?.Document == null)
            {
                throw new Exception("No active Grasshopper document");
            }

            var updated = false;
            foreach (var obj in ghCanvas.Document.Objects)
            {
                if (!string.IsNullOrEmpty(targetGuid) && obj.InstanceGuid.ToString() != targetGuid)
                    continue;

                var typeFullName = obj.GetType().FullName;
                if (typeFullName.Contains("ScriptComponent") || typeFullName.Contains("CSharpComponent"))
                {
                    try
                    {
                        var ctxField = obj.GetType()
                                        .GetField("Context",
                                                BindingFlags.Instance | BindingFlags.NonPublic);

                        object ctx = ctxField.GetValue(obj);
                        var prop = ctx.GetType().GetProperty("EnforceParamsOnCreate",
                                                        BindingFlags.Instance | BindingFlags.Public);

                        prop.SetValue(ctx, false);

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
                                continue;
                            }
                        }

                        var setParamsFromScript = obj.GetType().GetMethod("SetParametersFromScript");
                        setParamsFromScript.Invoke(obj, null);

                        obj.Attributes.ExpireLayout();
                        obj.ExpireSolution(true);
                        updated = true;
                        prop.SetValue(ctx, true);
                        break;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to update component {obj.InstanceGuid}: {ex.Message}");
                    }
                }
            }

            return updated;
        }

    }
}