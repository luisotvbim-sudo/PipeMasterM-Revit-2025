using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoVirarDireita : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        try
        {
            Reference refTubo = uidoc.Selection.PickObject(ObjectType.PointOnElement, new FiltroTuboPipeMaster(), "PipeMaster [M]: Clique na PONTA do tubo de onde deseja virar à DIREITA...");
            if (!(doc.GetElement(refTubo.ElementId) is Pipe tuboExistente))
            {
                return Result.Cancelled;
            }
            XYZ pontoClique = refTubo.GlobalPoint;
            Connector conectorLivre = null;
            double menorDistancia = double.MaxValue;
            foreach (Connector c in tuboExistente.ConnectorManager.Connectors)
            {
                if (!c.IsConnected)
                {
                    double dist = c.Origin.DistanceTo(pontoClique);
                    if (dist < menorDistancia)
                    {
                        menorDistancia = dist;
                        conectorLivre = c;
                    }
                }
            }
            if (conectorLivre == null)
            {
                TaskDialog.Show("PipeMaster [M]", "Não foi encontrada uma ponta solta neste tubo.");
                return Result.Failed;
            }
            XYZ direcaoAvanco = conectorLivre.CoordinateSystem.BasisZ.Normalize();
            if (Math.Abs(direcaoAvanco.Z) > 0.99)
            {
                TaskDialog.Show("PipeMaster [M]", "O tubo está na vertical. Use comandos de subida/descida para tubos verticais.");
                return Result.Failed;
            }
            XYZ vetorDireita = new XYZ(direcaoAvanco.Y, 0.0 - direcaoAvanco.X, direcaoAvanco.Z).Normalize();
            double comprimentoNovoTubo = UnitUtils.ConvertToInternalUnits(0.3, UnitTypeId.Meters);
            XYZ pontoInicialNovo = conectorLivre.Origin;
            XYZ pontoFinalNovo = pontoInicialNovo + vetorDireita * comprimentoNovoTubo;
            using (Transaction t = new Transaction(doc, "PipeMaster: Virar à Direita"))
            {
                t.Start();
                ElementId sysId = tuboExistente.MEPSystem?.GetTypeId() ?? tuboExistente.MEPSystem?.Id;
                if (sysId == null || sysId == ElementId.InvalidElementId)
                {
                    sysId = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).FirstElementId();
                }
                Pipe novoTubo = Pipe.Create(doc, sysId, tuboExistente.PipeType.Id, tuboExistente.LevelId, pontoInicialNovo, pontoFinalNovo);
                double diametro = ((Element)tuboExistente).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                ((Element)novoTubo).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                bool sucessoAoCriarJoelho = false;
                foreach (Connector conectorNovo in novoTubo.ConnectorManager.Connectors)
                {
                    if (conectorNovo.Origin.DistanceTo(pontoInicialNovo) < 0.01)
                    {
                        try
                        {
                            doc.Create.NewElbowFitting(conectorLivre, conectorNovo);
                            sucessoAoCriarJoelho = true;
                        }
                        catch
                        {
                        }
                        break;
                    }
                }
                if (!sucessoAoCriarJoelho)
                {
                    t.RollBack();
                    return Result.Cancelled;
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex2)
        {
            TaskDialog.Show("PipeMaster [M] - Erro", ex2.Message);
            return Result.Failed;
        }
    }
}
