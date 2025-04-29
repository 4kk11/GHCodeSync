using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace GHCodeSync
{
    public class GHCodeSyncInfo : GH_AssemblyInfo
    {
        public override string Name => "VSCode Grasshopper Integration";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "A plugin for connecting VSCode and Grasshopper C# script components";

        public override Guid Id => new Guid("810aa6cf-cbff-4d78-bfed-5f899334ef72");

        //Return a string identifying you or your company.
        public override string AuthorName => "GHCodeSync Authors";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "https://github.com/yourusername/GHCodeSync";

        public override string Version => "0.0.1";

        //Return a string representing the version. This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}