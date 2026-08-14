using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoInclinarRede : IExternalCommand
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
            MemoriaInclinacao.Carregar();
            ICollection<ElementId> idsSelecionados = uidoc.Selection.GetElementIds();
            if (idsSelecionados.Count == 0)
            {
                TaskDialog.Show("PipeMaster", "⚠\ufe0f Selecione a rede primeiro com o TAB.");
                return Result.Cancelled;
            }
            List<Element> selecionados = new List<Element>();
            foreach (ElementId id in idsSelecionados)
            {
                Element el = doc.GetElement(id);
                if (el is Pipe)
                {
                    selecionados.Add(el);
                }
                else if (el is FamilyInstance fi)
                {
                    long cat = ((fi.Category != null) ? fi.Category.Id.Value : 0);
                    if (cat == -2008049 || cat == -2008055 || cat == -2001160)
                    {
                        selecionados.Add(el);
                    }
                }
            }
            if (selecionados.Count == 0)
            {
                return Result.Cancelled;
            }
            List<Connector> conectoresAbertos = new List<Connector>();
            List<Connector> conectoresTerminais = new List<Connector>();
            foreach (Element el2 in selecionados)
            {
                foreach (Connector c in ObterConectores(el2))
                {
                    bool isExtremity = true;
                    bool isTerminal = false;
                    if (!c.IsConnected)
                    {
                        isTerminal = true;
                    }
                    else
                    {
                        foreach (Connector refC in c.AllRefs)
                        {
                            if (refC.Owner.Id != el2.Id && selecionados.Any((Element s) => s.Id == refC.Owner.Id))
                            {
                                isExtremity = false;
                            }
                            if (refC.Owner.Id != el2.Id && !selecionados.Any((Element s) => s.Id == refC.Owner.Id))
                            {
                                isTerminal = true;
                            }
                            if (refC.Owner.Category != null && refC.Owner.Category.Id.Value == -2001160)
                            {
                                isTerminal = true;
                            }
                        }
                    }
                    if (isExtremity)
                    {
                        conectoresAbertos.Add(c);
                    }
                    if (isTerminal)
                    {
                        conectoresTerminais.Add(c);
                    }
                }
            }
            if (conectoresAbertos.Count == 0)
            {
                return Result.Failed;
            }
            Connector pivotConn = EncontrarPivoAutomatico(conectoresAbertos, selecionados, doc);
            if (pivotConn == null)
            {
                pivotConn = conectoresAbertos.First();
            }
            Dictionary<string, double> targetZ = new Dictionary<string, double>();
            Dictionary<string, double> originalZ = new Dictionary<string, double>();
            Dictionary<string, bool> isFlexBranch = new Dictionary<string, bool>();
            Dictionary<string, double> distFromRalo = new Dictionary<string, double>();
            Queue<Connector> fila = new Queue<Connector>();
            string pivotKey = GetKey(pivotConn);
            targetZ[pivotKey] = pivotConn.Origin.Z;
            originalZ[pivotKey] = pivotConn.Origin.Z;
            isFlexBranch[pivotKey] = false;
            distFromRalo[pivotKey] = 0.0;
            fila.Enqueue(pivotConn);
            while (fila.Count > 0)
            {
                Connector curr = fila.Dequeue();
                string currKey = GetKey(curr);
                double cZ = targetZ[currKey];
                bool currFlex = isFlexBranch[currKey];
                double currentDist = distFromRalo[currKey];
                if (curr.IsConnected)
                {
                    foreach (Connector refC2 in curr.AllRefs)
                    {
                        if (refC2.Owner.Id != curr.Owner.Id && selecionados.Any((Element s) => s.Id == refC2.Owner.Id))
                        {
                            string refKey = GetKey(refC2);
                            if (!targetZ.ContainsKey(refKey))
                            {
                                targetZ[refKey] = cZ;
                                originalZ[refKey] = refC2.Origin.Z;
                                isFlexBranch[refKey] = currFlex;
                                distFromRalo[refKey] = currentDist;
                                fila.Enqueue(refC2);
                            }
                        }
                    }
                }
                foreach (Connector other in ObterConectores(curr.Owner))
                {
                    if (other.Id == curr.Id)
                    {
                        continue;
                    }
                    string otherKey = GetKey(other);
                    if (targetZ.ContainsKey(otherKey))
                    {
                        continue;
                    }
                    double otherZ = cZ;
                    bool nextFlex = currFlex;
                    double distXY = Math.Sqrt(Math.Pow(other.Origin.X - curr.Origin.X, 2.0) + Math.Pow(other.Origin.Y - curr.Origin.Y, 2.0));
                    if (curr.Owner is Pipe p)
                    {
                        double dzOrig = Math.Abs(other.Origin.Z - curr.Origin.Z);
                        if (distXY < 0.01)
                        {
                            otherZ = other.Origin.Z;
                            nextFlex = true;
                        }
                        else if (dzOrig / distXY > 0.1)
                        {
                            otherZ = cZ + dzOrig;
                        }
                        else
                        {
                            int dn = (int)Math.Round(UnitUtils.ConvertFromInternalUnits(curr.Radius * 2.0, UnitTypeId.Millimeters));
                            string sys = "Padrão";
                            Parameter pSysType = ((Element)p).get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
                            if (pSysType != null && pSysType.HasValue)
                            {
                                sys = pSysType.AsValueString();
                            }
                            else if (curr.MEPSystem != null)
                            {
                                ElementId typeId = curr.MEPSystem.GetTypeId();
                                if (typeId != ElementId.InvalidElementId)
                                {
                                    Element sysType = doc.GetElement(typeId);
                                    if (sysType != null)
                                    {
                                        sys = sysType.Name;
                                    }
                                }
                            }
                            double? incl = MemoriaInclinacao.ObterInclinacao(sys, dn);
                            otherZ = ((!incl.HasValue) ? (cZ + (other.Origin.Z - curr.Origin.Z)) : (cZ + distXY * incl.Value));
                        }
                    }
                    else if (curr.Owner is FamilyInstance)
                    {
                        double dzOrig2 = other.Origin.Z - curr.Origin.Z;
                        otherZ = cZ + dzOrig2;
                    }
                    targetZ[otherKey] = otherZ;
                    originalZ[otherKey] = other.Origin.Z;
                    isFlexBranch[otherKey] = nextFlex;
                    distFromRalo[otherKey] = currentDist + distXY;
                    fila.Enqueue(other);
                }
            }
            double maxDist = -1.0;
            string anchorKey = null;
            foreach (string key in targetZ.Keys)
            {
                if (!(key == pivotKey) && distFromRalo.ContainsKey(key) && !isFlexBranch[key] && distFromRalo[key] > maxDist)
                {
                    maxDist = distFromRalo[key];
                    anchorKey = key;
                }
            }
            if (anchorKey != null)
            {
                double shiftNeeded = originalZ[anchorKey] - targetZ[anchorKey];
                List<string> chaves = targetZ.Keys.ToList();
                foreach (string key2 in chaves)
                {
                    if (!isFlexBranch[key2])
                    {
                        targetZ[key2] += shiftNeeded;
                    }
                }
            }
            using (Transaction t = new Transaction(doc, "PipeMaster: Inclinação Ultimate"))
            {
                t.Start();
                FailureHandlingOptions fho = t.GetFailureHandlingOptions();
                fho.SetFailuresPreprocessor(new SilenciadorInterno());
                t.SetFailureHandlingOptions(fho);
                List<(string, string)> conexoesOriginais = new List<(string, string)>();
                List<Tuple<Connector, Connector>> conectoresParaDesconectar = new List<Tuple<Connector, Connector>>();
                foreach (Element el3 in selecionados)
                {
                    foreach (Connector c2 in ObterConectores(el3))
                    {
                        if (!c2.IsConnected)
                        {
                            continue;
                        }
                        foreach (Connector refC3 in c2.AllRefs)
                        {
                            if (refC3.Owner.Id != el3.Id && selecionados.Any((Element s) => s.Id == refC3.Owner.Id))
                            {
                                conexoesOriginais.Add((GetKey(c2), GetKey(refC3)));
                                conectoresParaDesconectar.Add(new Tuple<Connector, Connector>(c2, refC3));
                            }
                        }
                    }
                }
                foreach (Tuple<Connector, Connector> par in conectoresParaDesconectar)
                {
                    try
                    {
                        par.Item1.DisconnectFrom(par.Item2);
                    }
                    catch
                    {
                    }
                }
                doc.Regenerate();
                foreach (Element el4 in selecionados)
                {
                    if (!(el4 is Pipe p2))
                    {
                        continue;
                    }
                    LocationCurve lc = p2.Location as LocationCurve;
                    XYZ p3 = lc.Curve.GetEndPoint(0);
                    XYZ p4 = lc.Curve.GetEndPoint(1);
                    double z0 = p3.Z;
                    double z1 = p4.Z;
                    foreach (Connector c3 in ObterConectores(p2))
                    {
                        string key3 = GetKey(c3);
                        if (targetZ.ContainsKey(key3))
                        {
                            if (c3.Origin.DistanceTo(p3) < 0.05)
                            {
                                z0 = targetZ[key3];
                            }
                            if (c3.Origin.DistanceTo(p4) < 0.05)
                            {
                                z1 = targetZ[key3];
                            }
                        }
                    }
                    lc.Curve = Line.CreateBound(new XYZ(p3.X, p3.Y, z0), new XYZ(p4.X, p4.Y, z1));
                }
                foreach (Element el5 in selecionados)
                {
                    if (!(el5 is FamilyInstance fi2))
                    {
                        continue;
                    }
                    Connector c4 = ObterConectores(fi2).FirstOrDefault();
                    if (c4 != null && targetZ.ContainsKey(GetKey(c4)))
                    {
                        double dz = targetZ[GetKey(c4)] - c4.Origin.Z;
                        if (Math.Abs(dz) > 0.001)
                        {
                            ElementTransformUtils.MoveElement(doc, fi2.Id, new XYZ(0.0, 0.0, dz));
                        }
                    }
                }
                doc.Regenerate();
                Dictionary<string, Connector> todosConectores = new Dictionary<string, Connector>();
                foreach (Element el6 in selecionados)
                {
                    foreach (Connector c5 in ObterConectores(el6))
                    {
                        todosConectores[GetKey(c5)] = c5;
                    }
                }
                foreach (var par2 in conexoesOriginais)
                {
                    if (!todosConectores.TryGetValue(par2.Item1, out var c6) || !todosConectores.TryGetValue(par2.Item2, out var c7))
                    {
                        continue;
                    }
                    bool isConnected = false;
                    if (c6.IsConnected)
                    {
                        foreach (Connector r in c6.AllRefs)
                        {
                            if (r.Id == c7.Id && r.Owner.Id == c7.Owner.Id)
                            {
                                isConnected = true;
                                break;
                            }
                        }
                    }
                    if (!isConnected)
                    {
                        try
                        {
                            c6.ConnectTo(c7);
                        }
                        catch
                        {
                        }
                    }
                }
                if (MemoriaInclinacao.NivelarTampaCaixas)
                {
                    foreach (Element el7 in selecionados)
                    {
                        if (!(el7 is FamilyInstance { Category: not null } fi3) || fi3.Category.Id.Value != -2001160)
                        {
                            continue;
                        }
                        double elevacaoBase = 0.0;
                        bool achouElevacao = false;
                        Parameter pOffset = ((Element)fi3).get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                        Parameter pElev = ((Element)fi3).get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);
                        if (pOffset != null && pOffset.HasValue)
                        {
                            elevacaoBase = pOffset.AsDouble();
                            achouElevacao = true;
                        }
                        else if (pElev != null && pElev.HasValue)
                        {
                            elevacaoBase = pElev.AsDouble();
                            achouElevacao = true;
                        }
                        if (achouElevacao)
                        {
                            Parameter paramProlongador = fi3.LookupParameter("Prolongador") ?? BuscarParametroFlexivel(fi3, "prolongador");
                            Parameter paramElevacaoRalo = fi3.LookupParameter("Elevação do Ralo") ?? fi3.LookupParameter("Elevacao do Ralo") ?? BuscarParametroFlexivel(fi3, "elevacao do ralo");
                            if (paramProlongador != null && !paramProlongador.IsReadOnly)
                            {
                                paramProlongador.Set(1);
                            }
                            if (paramElevacaoRalo != null && !paramElevacaoRalo.IsReadOnly)
                            {
                                double dezCentimetrosEmPes = UnitUtils.ConvertToInternalUnits(100.0, UnitTypeId.Millimeters);
                                double valorFinal = Math.Abs(elevacaoBase) + dezCentimetrosEmPes;
                                paramElevacaoRalo.Set(valorFinal);
                            }
                        }
                    }
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
            TaskDialog.Show("Erro PipeMaster", "Falha ao inclinar: " + ex2.Message);
            return Result.Failed;
        }
    }

    private Connector EncontrarPivoAutomatico(List<Connector> conectoresAbertos, List<Element> selecionados, Document doc)
    {
        if (conectoresAbertos.Count == 1)
        {
            return conectoresAbertos.First();
        }
        List<Connector> pipesAbertos = conectoresAbertos.Where((Connector c) => c.Owner is Pipe).ToList();
        if (pipesAbertos.Count == 0)
        {
            return conectoresAbertos.OrderBy((Connector c) => c.Origin.Z).First();
        }
        List<Connector> horizontaisAbertos = pipesAbertos.Where((Connector c) => Math.Abs(c.CoordinateSystem.BasisZ.Z) < 0.5).ToList();
        if (horizontaisAbertos.Count > 0)
        {
            double maxDiam = horizontaisAbertos.Max((Connector c) => c.Radius);
            List<Connector> largestHorizontals = horizontaisAbertos.Where((Connector c) => c.Radius >= maxDiam - 0.001).ToList();
            return largestHorizontals.OrderBy((Connector c) => c.Origin.Z).First();
        }
        return pipesAbertos.OrderBy((Connector c) => c.Origin.Z).First();
    }

    private IEnumerable<Connector> ObterConectores(Element el)
    {
        ConnectorManager cm = null;
        if (el is Pipe p)
        {
            cm = p.ConnectorManager;
        }
        else if (el is FamilyInstance fi)
        {
            cm = fi.MEPModel?.ConnectorManager;
        }
        if (cm == null)
        {
            return Enumerable.Empty<Connector>();
        }
        return from Connector c in cm.Connectors
               where c.ConnectorType != ConnectorType.Logical
               select c;
    }

    private string GetKey(Connector c)
    {
        return $"{c.Owner.Id.ToString()}_{c.Id}";
    }

    private Parameter BuscarParametroFlexivel(FamilyInstance fi, string nomeParaSearch)
    {
        string busca = RemoverAcentos(nomeParaSearch);
        foreach (Parameter p in fi.Parameters)
        {
            if (p.Definition != null && !string.IsNullOrEmpty(p.Definition.Name))
            {
                string nome = RemoverAcentos(p.Definition.Name);
                if (nome.Contains(busca))
                {
                    return p;
                }
            }
        }
        return null;
    }

    private string RemoverAcentos(string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return texto;
        }
        texto = texto.ToLowerInvariant();
        StringBuilder sb = new StringBuilder();
        string text = texto.Normalize(NormalizationForm.FormD);
        foreach (char c in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
