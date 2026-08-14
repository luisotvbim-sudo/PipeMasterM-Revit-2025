using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoAlignBranchPerp : IExternalCommand
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
            FiltroTuboPerp filtro = new FiltroTuboPerp();
            Reference refPrincipal = uidoc.Selection.PickObject(ObjectType.Element, filtro, "PipeMaster [M]: Selecione o Tubo PRINCIPAL (Coletor Mestre)...");
            MEPCurve tuboPrincipal = doc.GetElement(refPrincipal) as MEPCurve;
            Reference refRamal = uidoc.Selection.PickObject(ObjectType.Element, filtro, "PipeMaster [M]: Selecione o Tubo RAMAL que será alinhado a 90º...");
            MEPCurve tuboRamal = doc.GetElement(refRamal) as MEPCurve;
            if (tuboPrincipal == null || tuboRamal == null || tuboPrincipal.Id == tuboRamal.Id)
            {
                return Result.Cancelled;
            }
            LocationCurve locPrincipal = tuboPrincipal.Location as LocationCurve;
            LocationCurve locRamal = tuboRamal.Location as LocationCurve;
            if (locPrincipal == null || locRamal == null || !(locPrincipal.Curve is Line) || !(locRamal.Curve is Line))
            {
                TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f Os elementos precisam ser segmentos retos.");
                return Result.Failed;
            }
            Line linhaPrin = locPrincipal.Curve as Line;
            Line linhaRamal = locRamal.Curve as Line;
            XYZ p0Prin = linhaPrin.GetEndPoint(0);
            XYZ p1Prin = linhaPrin.GetEndPoint(1);
            XYZ vM = linhaPrin.Direction;
            Line eixoPrincipal = Line.CreateUnbound(p0Prin, vM);
            XYZ pt0Ramal = linhaRamal.GetEndPoint(0);
            XYZ pt1Ramal = linhaRamal.GetEndPoint(1);
            double dist0 = eixoPrincipal.Distance(pt0Ramal);
            double dist1 = eixoPrincipal.Distance(pt1Ramal);
            XYZ pAnchor;
            XYZ pPontaGira;
            if (dist0 < dist1)
            {
                pAnchor = pt0Ramal;
                pPontaGira = pt1Ramal;
            }
            else
            {
                pAnchor = pt1Ramal;
                pPontaGira = pt0Ramal;
            }
            XYZ p0Prin2D = new XYZ(p0Prin.X, p0Prin.Y, 0.0);
            XYZ p1Prin2D = new XYZ(p1Prin.X, p1Prin.Y, 0.0);
            if (p0Prin2D.DistanceTo(p1Prin2D) < 1E-06)
            {
                TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f O coletor principal é vertical. O comando funciona para redes horizontais/inclinadas.");
                return Result.Failed;
            }
            XYZ vM2D = (p1Prin2D - p0Prin2D).Normalize();
            XYZ pAnchor2D = new XYZ(pAnchor.X, pAnchor.Y, 0.0);
            double t = (pAnchor2D - p0Prin2D).DotProduct(vM2D);
            XYZ pIntersecao2D = p0Prin2D + vM2D * t;
            XYZ dirAfastamento2D = pAnchor2D - pIntersecao2D;
            XYZ dirRamal2D;
            if (dirAfastamento2D.GetLength() < 1E-05)
            {
                XYZ perp1 = new XYZ(0.0 - vM2D.Y, vM2D.X, 0.0).Normalize();
                XYZ perp2 = new XYZ(vM2D.Y, 0.0 - vM2D.X, 0.0).Normalize();
                XYZ vOrig = (pPontaGira - pAnchor).Normalize();
                dirRamal2D = ((vOrig.DotProduct(perp1) > vOrig.DotProduct(perp2)) ? perp1 : perp2);
            }
            else
            {
                dirRamal2D = dirAfastamento2D.Normalize();
            }
            XYZ vOriginal = pPontaGira - pAnchor;
            double len2D = new XYZ(vOriginal.X, vOriginal.Y, 0.0).GetLength();
            double deltaZ = pPontaGira.Z - pAnchor.Z;
            if (len2D < 1E-06)
            {
                TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f O ramal é muito curto ou perfeitamente vertical.");
                return Result.Failed;
            }
            XYZ novaPontaGira = pAnchor + new XYZ(dirRamal2D.X * len2D, dirRamal2D.Y * len2D, deltaZ);
            Line novaLinha = ((!pAnchor.IsAlmostEqualTo(pt0Ramal)) ? Line.CreateBound(novaPontaGira, pAnchor) : Line.CreateBound(pAnchor, novaPontaGira));
            double distCruzamento2D = dirAfastamento2D.GetLength();
            double slopeZ = deltaZ / len2D;
            double zRamalNoCruzamento = pAnchor.Z - slopeZ * distCruzamento2D;
            double lenPrin2D = p0Prin2D.DistanceTo(p1Prin2D);
            double proporcaoT = t / lenPrin2D;
            double zPrinNoCruzamento = p0Prin.Z + proporcaoT * (p1Prin.Z - p0Prin.Z);
            double diferencaZ = zPrinNoCruzamento - zRamalNoCruzamento;
            XYZ vetorDeslocamentoVertical = new XYZ(0.0, 0.0, diferencaZ);
            using (Transaction trans = new Transaction(doc, "PipeMaster: Align Perpendicular"))
            {
                FailureHandlingOptions options = trans.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new SupressorAvisoDesconectar());
                trans.SetFailureHandlingOptions(options);
                trans.Start();
                locRamal.Curve = novaLinha;
                ElementTransformUtils.MoveElement(doc, tuboRamal.Id, vetorDeslocamentoVertical);
                trans.Commit();
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
