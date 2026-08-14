using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

public class EventoPintarLinhas : IExternalEventHandler
{
    public List<ElementId> IdsParaPintar { get; set; } = new List<ElementId>();

    public List<ElementId> IdsParaRestaurar { get; set; } = new List<ElementId>();

    public Color CorOverride { get; set; }

    public void Execute(UIApplication app)
    {
        UIDocument uidoc = app.ActiveUIDocument;
        if (uidoc == null)
        {
            return;
        }
        Document doc = uidoc.Document;
        using Transaction t = new Transaction(doc, "PipeMaster: Destacar Linhas");
        t.Start();
        OverrideGraphicSettings ogsReset = new OverrideGraphicSettings();
        foreach (ElementId id in IdsParaRestaurar)
        {
            try
            {
                doc.ActiveView.SetElementOverrides(id, ogsReset);
            }
            catch
            {
            }
        }
        if (IdsParaPintar.Count > 0 && CorOverride != null)
        {
            OverrideGraphicSettings ogsPaint = new OverrideGraphicSettings();
            ogsPaint.SetProjectionLineColor(CorOverride);
            ogsPaint.SetProjectionLineWeight(5);
            foreach (ElementId id2 in IdsParaPintar)
            {
                try
                {
                    doc.ActiveView.SetElementOverrides(id2, ogsPaint);
                }
                catch
                {
                }
            }
        }
        t.Commit();
    }

    public string GetName()
    {
        return "Pintar Linhas UI";
    }
}
