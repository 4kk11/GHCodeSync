using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GH_CodeSync
{
  public class GH_CodeSyncComponent : GH_Component
  {
    public GH_CodeSyncComponent()
      : base("GH_CodeSync Component", "Nickname",
        "Description of component",
        "Category", "Subcategory")
    {
    }

    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
    }

    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
    }

    protected override System.Drawing.Bitmap Icon => null;

    public override Guid ComponentGuid => new Guid("60469559-3e0d-4ffc-8de2-33064bcbda1b");
  }
}