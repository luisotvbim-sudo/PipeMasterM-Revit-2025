using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoAlignBranch : IExternalCommand
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
            MepLinearFilter filtroLinear = new MepLinearFilter();
            Reference refPrincipal = uidoc.Selection.PickObject(ObjectType.Element, filtroLinear, "PipeMaster [M]: Selecione o Tubo PRINCIPAL (Tronco). Pressione ESC para sair.");
            MEPCurve tuboPrincipal = doc.GetElement(refPrincipal) as MEPCurve;
            Reference refRamal = uidoc.Selection.PickObject(ObjectType.Element, filtroLinear, "PipeMaster [M]: Selecione o RAMAL (Branch) que será alinhado ao Tronco.");
            MEPCurve tuboRamal = doc.GetElement(refRamal) as MEPCurve;
            if (tuboPrincipal == null || tuboRamal == null)
            {
                TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f Você precisa selecionar tubulações válidas.");
                return Result.Failed;
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
            XYZ p1 = linhaPrin.GetEndPoint(0);
            XYZ p2 = linhaPrin.GetEndPoint(1);
            XYZ p3 = linhaRamal.GetEndPoint(0);
            XYZ p4 = linhaRamal.GetEndPoint(1);
            XYZ vetorDeslocamentoFinal = null;
            XYZ intersecao2D = EncontrarIntersecao2D(p1, p2, p3, p4);
            if (intersecao2D != null)
            {
                XYZ vetorPrin2D = new XYZ(p2.X - p1.X, p2.Y - p1.Y, 0.0);
                XYZ vetorAteCruzamento = intersecao2D - new XYZ(p1.X, p1.Y, 0.0);
                double tPrin = vetorAteCruzamento.DotProduct(vetorPrin2D) / vetorPrin2D.DotProduct(vetorPrin2D);
                double zPrincipalNoCruzamento = p1.Z + tPrin * (p2.Z - p1.Z);
                XYZ vetorRamal2D = new XYZ(p4.X - p3.X, p4.Y - p3.Y, 0.0);
                XYZ vetorAteCruzamentoRamal = intersecao2D - new XYZ(p3.X, p3.Y, 0.0);
                double tRamal = vetorAteCruzamentoRamal.DotProduct(vetorRamal2D) / vetorRamal2D.DotProduct(vetorRamal2D);
                double zRamalNoCruzamento = p3.Z + tRamal * (p4.Z - p3.Z);
                double diferencaZ = zPrincipalNoCruzamento - zRamalNoCruzamento;
                vetorDeslocamentoFinal = new XYZ(0.0, 0.0, diferencaZ);
            }
            else
            {
                XYZ v1 = (p2 - p1).Normalize();
                XYZ v2 = (p4 - p3).Normalize();
                if (!ObterVetorAlinhamento3D(p1, v1, p3, v2, out XYZ shift3D))
                {
                    TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f As tubulações são perfeitamente colineares em 3D. Não é possível alinhá-las para um cruzamento.");
                    return Result.Failed;
                }
                vetorDeslocamentoFinal = shift3D;
            }
            using (Transaction t = new Transaction(doc, "PipeMaster: Align Branch"))
            {
                t.Start();
                ElementTransformUtils.MoveElement(doc, tuboRamal.Id, vetorDeslocamentoFinal);
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
            TaskDialog.Show("PipeMaster [M] - Erro", "Ops! Algo deu errado: " + ex2.Message);
            return Result.Failed;
        }
    }

    private XYZ EncontrarIntersecao2D(XYZ p1, XYZ p2, XYZ p3, XYZ p4)
    {
        double A1 = p2.Y - p1.Y;
        double B1 = p1.X - p2.X;
        double C1 = A1 * p1.X + B1 * p1.Y;
        double A2 = p4.Y - p3.Y;
        double B2 = p3.X - p4.X;
        double C2 = A2 * p3.X + B2 * p3.Y;
        double determinante = A1 * B2 - A2 * B1;
        if (Math.Abs(determinante) < 1E-05)
        {
            return null;
        }
        double x = (B2 * C1 - B1 * C2) / determinante;
        double y = (A1 * C2 - A2 * C1) / determinante;
        return new XYZ(x, y, 0.0);
    }

    private bool ObterVetorAlinhamento3D(XYZ p1, XYZ v1, XYZ p2, XYZ v2, out XYZ vetorDeslocamento)
    {
        _ = XYZ.Zero;
        XYZ w0 = p1 - p2;
        double a = v1.DotProduct(v1);
        double b = v1.DotProduct(v2);
        double c = v2.DotProduct(v2);
        double d = w0.DotProduct(v1);
        double e = w0.DotProduct(v2);
        double denominador = a * c - b * b;
        if (Math.Abs(denominador) < 1E-05)
        {
            double tParallel = w0.DotProduct(v1) / a;
            XYZ projPrincipal = p1 - v1 * tParallel;
            vetorDeslocamento = projPrincipal - p2;
            if (vetorDeslocamento.GetLength() < 1E-05)
            {
                return false;
            }
            return true;
        }
        double s = (b * e - c * d) / denominador;
        double tParam = (a * e - b * d) / denominador;
        XYZ pontoMaisProximoPrincipal = p1 + v1 * s;
        XYZ pontoMaisProximoRamal = p2 + v2 * tParam;
        vetorDeslocamento = pontoMaisProximoPrincipal - pontoMaisProximoRamal;
        return true;
    }
}
