using System;
using Rhino;
using Grasshopper.Kernel;
using GHCodeSync.Managers;
using System.Drawing;
using System.Reflection;
using System.Linq;
using System.IO;

namespace GHCodeSync
{
    public static class EmbeddedResourceHelpers
    {
        
        public static Bitmap GetEmbeddedImage(string resourceName)
        {
            // アセンブリの取得
            var assembly = Assembly.GetExecutingAssembly();
            // リソース名の取得
            var embeddedResourceName = assembly.GetManifestResourceNames().FirstOrDefault(x => x.Contains(resourceName));
            if (embeddedResourceName == null)
            {
                throw new ArgumentException($"Resource '{resourceName}' not found.");
            }

            // リソースのストリームを取得
            using (var stream = assembly.GetManifestResourceStream(embeddedResourceName))
            {
                if (stream == null)
                {
                    throw new ArgumentException($"Resource stream for '{resourceName}' not found.");
                }
                // Bitmapとして読み込む
                return new Bitmap(stream);
            }
        }
    }
}