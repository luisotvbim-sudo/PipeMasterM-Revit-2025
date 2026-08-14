using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace PipeMasterMEP;

public static class ImportadorAparelhos
{
    public class ResultadoImportacao
    {
        public int TotalImportados { get; set; }

        public int TotalIgnorados { get; set; }

        public List<string> Avisos { get; set; } = new List<string>();
    }

    private static XYZ ProjHoriz(XYZ v)
    {
        if (v == null)
        {
            return null;
        }
        XYZ h = new XYZ(v.X, v.Y, 0.0);
        return (h.GetLength() > 1E-06) ? h.Normalize() : null;
    }

    private static string Fmt(XYZ v)
    {
        return (v == null) ? "(null)" : string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2})", v.X, v.Y);
    }

    private static string FmtPt(XYZ p)
    {
        return (p == null) ? "(null)" : string.Format(CultureInfo.InvariantCulture, "({0:F2}m, {1:F2}m, z={2:F2}m)", p.X * 0.3048, p.Y * 0.3048, p.Z * 0.3048);
    }

    public static List<RevitLinkInstance> ResolverVinculos(Document doc, RevitLinkInstance link)
    {
        if (link != null)
        {
            return new List<RevitLinkInstance> { link };
        }
        return new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
    }

    public static ResultadoImportacao Importar(Document doc, SpatialElement ambiente, RevitLinkInstance link, MapeamentoAparelhosViewModel mapeamento, out List<PecaAguaDetectada> pecasImportadas)
    {
        pecasImportadas = new List<PecaAguaDetectada>();
        ResultadoImportacao resultado = new ResultadoImportacao();
        List<RevitLinkInstance> vinculos = ResolverVinculos(doc, link);
        if (vinculos.Count == 0)
        {
            resultado.Avisos.Add("Nenhum vínculo carregado no projeto — importação cancelada.");
            return resultado;
        }
        BoundingBoxXYZ bbAmb = ((Element)ambiente).get_BoundingBox((View)null);
        if (bbAmb == null)
        {
            resultado.Avisos.Add("Não foi possível obter o bounding box do ambiente.");
            return resultado;
        }
        Transform trfAmb = ((link != null) ? link.GetTotalTransform() : Transform.Identity);
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        double[] array = new double[2]
        {
            bbAmb.Min.X,
            bbAmb.Max.X
        };
        foreach (double cx in array)
        {
            double[] array2 = new double[2]
            {
                bbAmb.Min.Y,
                bbAmb.Max.Y
            };
            foreach (double cy in array2)
            {
                XYZ c = trfAmb.OfPoint(new XYZ(cx, cy, bbAmb.Min.Z));
                if (c.X < minX)
                {
                    minX = c.X;
                }
                if (c.X > maxX)
                {
                    maxX = c.X;
                }
                if (c.Y < minY)
                {
                    minY = c.Y;
                }
                if (c.Y > maxY)
                {
                    maxY = c.Y;
                }
            }
        }
        double folga = 1.0;
        XYZ bbMin = new XYZ(minX - folga, minY - folga, 0.0);
        XYZ bbMax = new XYZ(maxX + folga, maxY + folga, 0.0);
        Dictionary<string, FamilySymbol> mapa = new Dictionary<string, FamilySymbol>(StringComparer.OrdinalIgnoreCase);
        foreach (ItemMapeamento item in mapeamento.Itens)
        {
            if (item.Incluir && !string.IsNullOrEmpty(item.FamiliaSelecionada) && !(item.FamiliaSelecionada == "-- não importar --"))
            {
                FamilySymbol simbolo = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault((FamilySymbol s) => string.Equals(s.FamilyName, item.FamiliaSelecionada, StringComparison.OrdinalIgnoreCase));
                if (simbolo != null)
                {
                    mapa[item.NomeFamiliaVinculo] = simbolo;
                    continue;
                }
                resultado.Avisos.Add($"Família '{item.FamiliaSelecionada}' não encontrada no projeto — '{item.NomeFamiliaVinculo}' ignorada.");
            }
        }
        foreach (ItemMapeamento item2 in mapeamento.Itens)
        {
            DebugAgua.Log("MAPA: '" + item2.NomeFamiliaVinculo + "' (" + item2.TipoIdentificado + ") -> '" + item2.FamiliaSelecionada + "'");
        }
        if (mapa.Count == 0)
        {
            resultado.Avisos.Add("Nenhuma família de destino encontrada no projeto.");
            return resultado;
        }
        Level nivel = null;
        if (ambiente.LevelId != null && ambiente.LevelId != ElementId.InvalidElementId)
        {
            nivel = doc.GetElement(ambiente.LevelId) as Level;
        }
        if (nivel == null)
        {
            nivel = doc.ActiveView?.GenLevel;
        }
        if (nivel == null)
        {
            nivel = (from Level l in new FilteredElementCollector(doc).OfClass(typeof(Level))
                     orderby l.Elevation
                     select l).FirstOrDefault();
        }
        double zRef = nivel?.ProjectElevation ?? trfAmb.OfPoint(bbAmb.Min).Z;
        DebugAgua.Log("IMPORT: nível='" + (nivel?.Name ?? "?") + "' zRef=" + FmtPt(new XYZ(0.0, 0.0, zRef)) + " — coletando louças com Z em [" + Math.Round((zRef - 1.5) * 0.3048, 2) + "m .. " + Math.Round((zRef + 9.0) * 0.3048, 2) + "m]");
        ElementMulticategoryFilter catsVinculo = new ElementMulticategoryFilter(new List<BuiltInCategory>
        {
            BuiltInCategory.OST_PlumbingFixtures,
            BuiltInCategory.OST_SpecialityEquipment,
            BuiltInCategory.OST_GenericModel
        });
        List<Tuple<FamilyInstance, Transform>> instancias = new List<Tuple<FamilyInstance, Transform>>();
        foreach (RevitLinkInstance lk in vinculos)
        {
            Document linkDoc = lk.GetLinkDocument();
            if (linkDoc == null)
            {
                continue;
            }
            Transform trfLk = lk.GetTotalTransform();
            foreach (FamilyInstance fi in new FilteredElementCollector(linkDoc).WherePasses(catsVinculo).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
            {
                if (fi.Location is LocationPoint loc)
                {
                    XYZ pt = trfLk.OfPoint(loc.Point);
                    if (!(pt.Z < zRef - 1.5) && !(pt.Z > zRef + 9.0) && pt.X >= bbMin.X && pt.X <= bbMax.X && pt.Y >= bbMin.Y && pt.Y <= bbMax.Y)
                    {
                        instancias.Add(Tuple.Create(fi, trfLk));
                    }
                }
            }
        }
        if (instancias.Count == 0)
        {
            resultado.Avisos.Add("Nenhum aparelho encontrado nos vínculos dentro do ambiente.");
        }
        foreach (Tuple<FamilyInstance, Transform> par in instancias)
        {
            FamilyInstance fi2 = par.Item1;
            Transform trf = par.Item2;
            string nomeFam = fi2.Symbol.FamilyName;
            if (!mapa.TryGetValue(nomeFam, out var simboloDestino))
            {
                resultado.TotalIgnorados++;
                continue;
            }
            try
            {
                if (!(fi2.Location is LocationPoint locPt))
                {
                    resultado.TotalIgnorados++;
                    continue;
                }
                XYZ posicao = trf.OfPoint(locPt.Point);
                if (pecasImportadas.Any((PecaAguaDetectada pi) => pi.Posicao != null && Math.Abs(pi.Posicao.X - posicao.X) < 0.5 && Math.Abs(pi.Posicao.Y - posicao.Y) < 0.5))
                {
                    DebugAgua.Log("IMPORT ignorado (sobreposto por posição): '" + nomeFam + "' @ " + FmtPt(posicao));
                    resultado.TotalIgnorados++;
                    continue;
                }
                if (!simboloDestino.IsActive)
                {
                    simboloDestino.Activate();
                    doc.Regenerate();
                }
                FamilyInstance nova = ((nivel == null) ? doc.Create.NewFamilyInstance(posicao, simboloDestino, StructuralType.NonStructural) : doc.Create.NewFamilyInstance(posicao, simboloDestino, nivel, StructuralType.NonStructural));
                doc.Regenerate();
                XYZ lpNova = (nova.Location as LocationPoint)?.Point;
                if (lpNova != null && lpNova.DistanceTo(posicao) > 0.01)
                {
                    ElementTransformUtils.MoveElement(doc, nova.Id, posicao - lpNova);
                    doc.Regenerate();
                    DebugAgua.Log("   cota corrigida: nasceu em " + FmtPt(lpNova) + " -> movida p/ " + FmtPt(posicao));
                }
                XYZ fOrig = ProjHoriz(trf.OfVector(fi2.FacingOrientation));
                XYZ fDest = ProjHoriz(nova.FacingOrientation);
                XYZ hOrig = ProjHoriz(trf.OfVector(fi2.HandOrientation));
                XYZ hDest = ProjHoriz(nova.HandOrientation);
                DebugAgua.Log("IMPORT '" + nomeFam + "' -> '" + simboloDestino.FamilyName + "' @ " + FmtPt(posicao) + " Mirrored=" + fi2.Mirrored + ((fi2.Host != null) ? (" hostOrig=" + fi2.Host.Category?.Name) : ""));
                DebugAgua.Log("   facingOrig=" + Fmt(fOrig) + " handOrig=" + Fmt(hOrig) + " facingDest=" + Fmt(fDest) + " handDest=" + Fmt(hDest));
                XYZ refO = null;
                XYZ refD = null;
                string eixoUsado = null;
                if (fOrig != null && fDest != null)
                {
                    refO = fOrig;
                    refD = fDest;
                    eixoUsado = "facing";
                }
                else if (hOrig != null && hDest != null)
                {
                    refO = hOrig;
                    refD = hDest;
                    eixoUsado = "hand";
                }
                if (refO != null)
                {
                    double ang = Math.Atan2(refD.CrossProduct(refO).Z, refD.DotProduct(refO));
                    DebugAgua.Log("   alinhando pelo " + eixoUsado + " => giro=" + Math.Round(ang * 180.0 / Math.PI, 1) + "°");
                    if (Math.Abs(ang) > 0.0001)
                    {
                        ElementTransformUtils.RotateElement(doc, nova.Id, Line.CreateBound(posicao, posicao + XYZ.BasisZ), ang);
                        doc.Regenerate();
                    }
                }
                else
                {
                    XYZ eixoX = trf.OfVector(XYZ.BasisX);
                    double rotTotal = locPt.Rotation + Math.Atan2(eixoX.Y, eixoX.X);
                    if (Math.Abs(rotTotal) > 0.0001)
                    {
                        ElementTransformUtils.RotateElement(doc, nova.Id, Line.CreateBound(posicao, posicao + XYZ.BasisZ), rotTotal);
                    }
                    DebugAgua.Log("   SEM eixo horizontal nos dois lados — fallback ângulo cru=" + Math.Round(rotTotal * 180.0 / Math.PI, 1) + "°");
                }
                pecasImportadas.Add(new PecaAguaDetectada
                {
                    Instancia = nova,
                    Posicao = posicao,
                    Nome = simboloDestino.FamilyName
                });
                resultado.TotalImportados++;
            }
            catch (Exception ex)
            {
                resultado.Avisos.Add("Falha ao importar '" + nomeFam + "': " + ex.Message);
                resultado.TotalIgnorados++;
            }
        }
        mapeamento.SalvarMapeamento();
        return resultado;
    }
}
