using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;

namespace PipeMasterMEP;

public class PreviewRotasServer : IDirectContext3DServer, IExternalServer
{
    private Guid _serverId;

    private XYZ _pt1;

    private XYZ _intA;

    private XYZ _intB;

    private XYZ _pt2;

    private bool _temDados = false;

    private Outline _boundingBox;

    private int _rotaAtiva = 0;

    private List<XYZ> _rotaLivre = null;

    private List<List<XYZ>> _todasRotasLivres = null;

    private XYZ _bolinhasCentro = null;

    private XYZ _cavaletePreview = null;

    public int RotaAtiva => _rotaAtiva;

    public PreviewRotasServer()
    {
        _serverId = Guid.NewGuid();
    }

    public void SetRotas(XYZ pt1, XYZ intA, XYZ intB, XYZ pt2)
    {
        _pt1 = pt1;
        _intA = intA;
        _intB = intB;
        _pt2 = pt2;
        _rotaLivre = null;
        _temDados = true;
        _rotaAtiva = 0;
        double minX = Math.Min(Math.Min(pt1.X, pt2.X), Math.Min(intA.X, intB.X)) - 1.0;
        double minY = Math.Min(Math.Min(pt1.Y, pt2.Y), Math.Min(intA.Y, intB.Y)) - 1.0;
        double minZ = pt1.Z - 1.0;
        double maxX = Math.Max(Math.Max(pt1.X, pt2.X), Math.Max(intA.X, intB.X)) + 1.0;
        double maxY = Math.Max(Math.Max(pt1.Y, pt2.Y), Math.Max(intA.Y, intB.Y)) + 1.0;
        double maxZ = pt1.Z + 1.0;
        _boundingBox = new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
    }

    public void SetRotaLivre(List<XYZ> pts)
    {
        _rotaLivre = pts;
        _temDados = true;
        _rotaAtiva = 3;
        if (pts != null && pts.Count > 0)
        {
            double minX = pts.Min((XYZ p) => p.X) - 1.0;
            double minY = pts.Min((XYZ p) => p.Y) - 1.0;
            double minZ = pts.Min((XYZ p) => p.Z) - 1.0;
            double maxX = pts.Max((XYZ p) => p.X) + 1.0;
            double maxY = pts.Max((XYZ p) => p.Y) + 1.0;
            double maxZ = pts.Max((XYZ p) => p.Z) + 1.0;
            _boundingBox = new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
        }
    }

    public void SetRotasLivres(List<List<XYZ>> rotas)
    {
        _todasRotasLivres = rotas;
        _temDados = true;
        _rotaAtiva = -1;
        List<XYZ> allPts = new List<XYZ>();
        if (rotas != null)
        {
            foreach (List<XYZ> r in rotas)
            {
                if (r != null)
                {
                    allPts.AddRange(r);
                }
            }
        }
        if (allPts.Count > 0)
        {
            double minX = allPts.Min((XYZ p) => p.X) - 1.0;
            double minY = allPts.Min((XYZ p) => p.Y) - 1.0;
            double minZ = allPts.Min((XYZ p) => p.Z) - 1.0;
            double maxX = allPts.Max((XYZ p) => p.X) + 1.0;
            double maxY = allPts.Max((XYZ p) => p.Y) + 1.0;
            double maxZ = allPts.Max((XYZ p) => p.Z) + 1.0;
            _boundingBox = new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
        }
    }

    public void SetCavaletePreview(XYZ centroColuna, XYZ posCavalete)
    {
        _bolinhasCentro = centroColuna;
        _cavaletePreview = posCavalete;
        _rotaAtiva = 5;
        _temDados = true;
    }

    public void SetRotaAtiva(int rota)
    {
        _rotaAtiva = rota;
    }

    public void Clear()
    {
        _temDados = false;
    }

    public bool CanExecute(View view)
    {
        return _temDados && (view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.ThreeD || view.ViewType == ViewType.EngineeringPlan);
    }

    public string GetApplicationId()
    {
        return "PipeMaster";
    }

    public string GetSourceId()
    {
        return "PipeMasterPreview";
    }

    public string GetName()
    {
        return "PipeMaster Rota Preview";
    }

    public string GetDescription()
    {
        return "Renderiza prévias de tubulação em tempo real";
    }

    public string GetVendorId()
    {
        return "FA_PROJETOS";
    }

    public Guid GetServerId()
    {
        return _serverId;
    }

    public ExternalServiceId GetServiceId()
    {
        return ExternalServices.BuiltInExternalServices.DirectContext3DService;
    }

    public Outline GetBoundingBox(View view)
    {
        return _boundingBox;
    }

    public bool UseInTransparentPass(View view)
    {
        return true;
    }

    public bool UsesHandles()
    {
        return false;
    }

    public void RenderScene(View view, DisplayStyle displayStyle)
    {
        if (!_temDados)
        {
            return;
        }
        try
        {
            double espessuraLinha = 0.025;
            Color roxoPrincipal = new Color(148, 0, 211);
            Color roxoSecundario = new Color(186, 85, 211);
            if (_todasRotasLivres != null)
            {
                for (int i = 0; i < _todasRotasLivres.Count; i++)
                {
                    if (i == _rotaAtiva)
                    {
                        DrawRota(_todasRotasLivres[i], roxoPrincipal);
                    }
                }
            }
            if (_rotaAtiva == 3 && _rotaLivre != null)
            {
                for (int j = 0; j < _rotaLivre.Count - 1; j++)
                {
                    if (_rotaLivre[j].DistanceTo(_rotaLivre[j + 1]) > 0.05)
                    {
                        DesenharLinhaGrossa(_rotaLivre[j], _rotaLivre[j + 1], roxoPrincipal, espessuraLinha);
                    }
                }
            }
            if ((_rotaAtiva == 5 || _rotaAtiva == 3) && _cavaletePreview != null)
            {
                double b = 0.05;
                XYZ n2 = _cavaletePreview + new XYZ(0.0, b, 0.0);
                XYZ s2 = _cavaletePreview + new XYZ(0.0, 0.0 - b, 0.0);
                XYZ e2 = _cavaletePreview + new XYZ(b, 0.0, 0.0);
                XYZ w2 = _cavaletePreview + new XYZ(0.0 - b, 0.0, 0.0);
                DesenharLinhaGrossa(n2, e2, roxoPrincipal, 0.02);
                DesenharLinhaGrossa(e2, s2, roxoPrincipal, 0.02);
                DesenharLinhaGrossa(s2, w2, roxoPrincipal, 0.02);
                DesenharLinhaGrossa(w2, n2, roxoPrincipal, 0.02);
                if (_bolinhasCentro != null)
                {
                    DesenharLinhaGrossa(_cavaletePreview, _bolinhasCentro, roxoSecundario, 0.02);
                }
                return;
            }
            bool mostrarA = _rotaAtiva == 0 || _rotaAtiva == 1;
            bool mostrarB = _rotaAtiva == 0 || _rotaAtiva == 2;
            if (mostrarA)
            {
                if (_pt1.DistanceTo(_intA) > 0.05)
                {
                    DesenharLinhaGrossa(_pt1, _intA, roxoPrincipal, espessuraLinha);
                }
                if (_intA.DistanceTo(_pt2) > 0.05)
                {
                    DesenharLinhaGrossa(_intA, _pt2, roxoPrincipal, espessuraLinha);
                }
            }
            if (mostrarB)
            {
                if (_pt1.DistanceTo(_intB) > 0.05)
                {
                    DesenharLinhaGrossa(_pt1, _intB, roxoSecundario, espessuraLinha);
                }
                if (_intB.DistanceTo(_pt2) > 0.05)
                {
                    DesenharLinhaGrossa(_intB, _pt2, roxoSecundario, espessuraLinha);
                }
            }
            void DrawRota(List<XYZ> r, Color c)
            {
                if (r != null)
                {
                    for (int k = 0; k < r.Count - 1; k++)
                    {
                        if (r[k].DistanceTo(r[k + 1]) > 0.05)
                        {
                            DesenharLinhaGrossa(r[k], r[k + 1], c, espessuraLinha);
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void DesenharLinhaGrossa(XYZ inicio, XYZ fim, Color cor, double espessura)
    {
        XYZ dir = (fim - inicio).Normalize();
        XYZ perp = new XYZ(0.0 - dir.Y, dir.X, 0.0).Normalize() * (espessura / 2.0);
        XYZ p1 = inicio + perp;
        XYZ p2 = inicio - perp;
        XYZ p3 = fim + perp;
        XYZ p4 = fim - perp;
        ColorWithTransparency c = new ColorWithTransparency(cor.Red, cor.Green, cor.Blue, 0u);
        int numVertices = 4;
        int vertexBufferSizeInFloats = VertexPositionColored.GetSizeInFloats() * numVertices;
        int numTriangulos = 2;
        int indexBufferSizeInShorts = IndexTriangle.GetSizeInShortInts() * numTriangulos;
        using VertexBuffer vb = new VertexBuffer(vertexBufferSizeInFloats);
        using IndexBuffer ib = new IndexBuffer(indexBufferSizeInShorts);
        using VertexFormat format = new VertexFormat(VertexFormatBits.PositionColored);
        using EffectInstance effect = new EffectInstance(VertexFormatBits.PositionColored);
        vb.Map(vertexBufferSizeInFloats);
        VertexStreamPositionColored vs = vb.GetVertexStreamPositionColored();
        vs.AddVertex(new VertexPositionColored(p1, c));
        vs.AddVertex(new VertexPositionColored(p2, c));
        vs.AddVertex(new VertexPositionColored(p3, c));
        vs.AddVertex(new VertexPositionColored(p4, c));
        vb.Unmap();
        ib.Map(indexBufferSizeInShorts);
        IndexStreamTriangle isTri = ib.GetIndexStreamTriangle();
        isTri.AddTriangle(new IndexTriangle(0, 1, 2));
        isTri.AddTriangle(new IndexTriangle(1, 3, 2));
        ib.Unmap();
        effect.SetColor(new Color(cor.Red, cor.Green, cor.Blue));
        effect.SetEmissiveColor(new Color(cor.Red, cor.Green, cor.Blue));
        DrawContext.FlushBuffer(vb, numVertices, ib, numTriangulos * 3, format, effect, PrimitiveType.TriangleList, 0, numTriangulos);
    }
}
