using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;

namespace PipeMasterMEP;

public class RoomHoverServer : IDirectContext3DServer, IExternalServer
{
    private Guid _serverGuid;

    private Mesh _roomMesh;

    private Transform _roomTransform = Transform.Identity;

    private readonly Color _corAmbiente = new Color(138, 43, 226);

    private readonly Color _corX = new Color(148, 0, 211);

    private readonly Color _corMarcador = new Color(186, 85, 211);

    private XYZ _x1a;

    private XYZ _x1b;

    private XYZ _x2a;

    private XYZ _x2b;

    private double _espX = 0.1;

    private List<double[]> _tris;

    private XYZ _marcadorPt;

    public RoomHoverServer()
    {
        _serverGuid = Guid.NewGuid();
    }

    public void UpdateRoomMesh(Mesh mesh, Transform t)
    {
        _roomMesh = mesh;
        _roomTransform = t;
        _x1a = null;
        _tris = null;
        if (mesh == null || mesh.NumTriangles == 0)
        {
            return;
        }
        List<double[]> tris = new List<double[]>(mesh.NumTriangles);
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        double maxZ = double.MinValue;
        for (int i = 0; i < mesh.NumTriangles; i++)
        {
            MeshTriangle tri = mesh.get_Triangle(i);
            double[] t2 = new double[6];
            for (int j = 0; j < 3; j++)
            {
                XYZ p = t.OfPoint(tri.get_Vertex(j));
                t2[j * 2] = p.X;
                t2[j * 2 + 1] = p.Y;
                if (p.X < minX)
                {
                    minX = p.X;
                }
                if (p.X > maxX)
                {
                    maxX = p.X;
                }
                if (p.Y < minY)
                {
                    minY = p.Y;
                }
                if (p.Y > maxY)
                {
                    maxY = p.Y;
                }
                if (p.Z > maxZ)
                {
                    maxZ = p.Z;
                }
            }
            tris.Add(t2);
        }
        _tris = tris;
        double dx = maxX - minX;
        double dy = maxY - minY;
        if (dx < 0.5 || dy < 0.5)
        {
            return;
        }
        double cx = (minX + maxX) / 2.0;
        double cy = (minY + maxY) / 2.0;
        if (!PontoInterno(cx, cy))
        {
            double maiorArea = -1.0;
            foreach (double[] t3 in tris)
            {
                double area = Math.Abs((t3[2] - t3[0]) * (t3[5] - t3[1]) - (t3[4] - t3[0]) * (t3[3] - t3[1]));
                if (area > maiorArea)
                {
                    maiorArea = area;
                    cx = (t3[0] + t3[2] + t3[4]) / 3.0;
                    cy = (t3[1] + t3[3] + t3[5]) / 3.0;
                }
            }
        }
        double diag = Math.Sqrt(dx * dx + dy * dy);
        double s = Math.Sqrt(2.0) / 2.0;
        double braco = double.MaxValue;
        braco = Math.Min(braco, DistanciaAteBorda(cx, cy, s, s, diag));
        braco = Math.Min(braco, DistanciaAteBorda(cx, cy, 0.0 - s, s, diag));
        braco = Math.Min(braco, DistanciaAteBorda(cx, cy, s, 0.0 - s, diag));
        braco = Math.Min(braco, DistanciaAteBorda(cx, cy, 0.0 - s, 0.0 - s, diag));
        braco -= 0.4;
        if (!(braco < 0.35))
        {
            braco = Math.Min(braco, 4.0);
            _espX = Math.Max(0.04, Math.Min(0.15, braco * 0.12));
            double z = maxZ + 0.05;
            _x1a = new XYZ(cx - braco * s, cy - braco * s, z);
            _x1b = new XYZ(cx + braco * s, cy + braco * s, z);
            _x2a = new XYZ(cx - braco * s, cy + braco * s, z);
            _x2b = new XYZ(cx + braco * s, cy - braco * s, z);
        }
    }

    private double DistanciaAteBorda(double cx, double cy, double dx, double dy, double maxDist)
    {
        double t;
        for (t = 0.2; t < maxDist && PontoInterno(cx + dx * t, cy + dy * t); t += 0.2)
        {
        }
        return t - 0.2;
    }

    private bool PontoInterno(double px, double py)
    {
        if (_tris == null)
        {
            return false;
        }
        foreach (double[] t in _tris)
        {
            double d1 = Sinal(px, py, t[0], t[1], t[2], t[3]);
            double d2 = Sinal(px, py, t[2], t[3], t[4], t[5]);
            double d3 = Sinal(px, py, t[4], t[5], t[0], t[1]);
            bool temNeg = d1 < 0.0 || d2 < 0.0 || d3 < 0.0;
            bool temPos = d1 > 0.0 || d2 > 0.0 || d3 > 0.0;
            if (!(temNeg && temPos))
            {
                return true;
            }
        }
        return false;
    }

    private static double Sinal(double px, double py, double ax, double ay, double bx, double by)
    {
        return (px - bx) * (ay - by) - (ax - bx) * (py - by);
    }

    public void MostrarMarcador(XYZ pt)
    {
        _marcadorPt = pt;
    }

    public void LimparMarcador()
    {
        _marcadorPt = null;
    }

    public void LimparAmbiente()
    {
        _roomMesh = null;
        _x1a = null;
        _tris = null;
    }

    public void Clear()
    {
        _roomMesh = null;
        _x1a = null;
        _tris = null;
        _marcadorPt = null;
    }

    public bool CanExecute(View view)
    {
        return (_roomMesh != null || _marcadorPt != null) && (view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.ThreeD || view.ViewType == ViewType.EngineeringPlan);
    }

    public void RenderScene(View view, DisplayStyle displayStyle)
    {
        try
        {
            bool passeTransparente = DrawContext.IsTransparentPass();
            if (_roomMesh != null && passeTransparente)
            {
                DesenharMalha(_roomMesh, _roomTransform, _corAmbiente);
            }
            if (!passeTransparente)
            {
                if (_roomMesh != null && _x1a != null)
                {
                    DesenharLinhaGrossa(_x1a, _x1b, _corX, _espX);
                    DesenharLinhaGrossa(_x2a, _x2b, _corX, _espX);
                }
                if (_marcadorPt != null)
                {
                    double h = 0.6;
                    double e = 0.09;
                    XYZ c = new XYZ(_marcadorPt.X, _marcadorPt.Y, _marcadorPt.Z + 0.3);
                    DesenharLinhaGrossa(c + new XYZ(0.0 - h, 0.0 - h, 0.0), c + new XYZ(h, h, 0.0), _corMarcador, e);
                    DesenharLinhaGrossa(c + new XYZ(0.0 - h, h, 0.0), c + new XYZ(h, 0.0 - h, 0.0), _corMarcador, e);
                }
            }
        }
        catch
        {
        }
    }

    private void DesenharMalha(Mesh mesh, Transform t, Color cor)
    {
        ColorWithTransparency colorT = new ColorWithTransparency(cor.Red, cor.Green, cor.Blue, 102u);
        VertexFormatBits formatBits = VertexFormatBits.PositionColored;
        using VertexFormat vertexFormat = new VertexFormat(formatBits);
        using EffectInstance effect = new EffectInstance(formatBits);
        int vertexCount = mesh.NumTriangles * 3;
        int indexCount = vertexCount;
        int vertexSize = VertexPositionColored.GetSizeInFloats();
        using VertexBuffer vBuffer = new VertexBuffer(vertexCount * vertexSize);
        using IndexBuffer iBuffer = new IndexBuffer(indexCount);
        vBuffer.Map(vertexCount * vertexSize);
        iBuffer.Map(indexCount);
        VertexStreamPositionColored streamPosCol = vBuffer.GetVertexStreamPositionColored();
        IndexStreamTriangle streamTriangle = iBuffer.GetIndexStreamTriangle();
        for (int i = 0; i < mesh.NumTriangles; i++)
        {
            MeshTriangle tri = mesh.get_Triangle(i);
            for (int j = 0; j < 3; j++)
            {
                XYZ pt = t.OfPoint(tri.get_Vertex(j));
                streamPosCol.AddVertex(new VertexPositionColored(pt, colorT));
            }
            streamTriangle.AddTriangle(new IndexTriangle(i * 3, i * 3 + 1, i * 3 + 2));
        }
        vBuffer.Unmap();
        iBuffer.Unmap();
        DrawContext.FlushBuffer(vBuffer, vertexCount, iBuffer, indexCount, vertexFormat, effect, PrimitiveType.TriangleList, 0, mesh.NumTriangles);
    }

    private void DesenharLinhaGrossa(XYZ inicio, XYZ fim, Color cor, double espessura)
    {
        XYZ dir = fim - inicio;
        if (dir.GetLength() < 1E-09)
        {
            return;
        }
        dir = dir.Normalize();
        XYZ perp = new XYZ(0.0 - dir.Y, dir.X, 0.0);
        if (perp.GetLength() < 1E-09)
        {
            perp = XYZ.BasisX;
        }
        perp = perp.Normalize() * (espessura / 2.0);
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

    public bool UseInTransparentPass(View view)
    {
        return true;
    }

    public string GetApplicationId()
    {
        return "";
    }

    public string GetSourceId()
    {
        return "";
    }

    public string GetName()
    {
        return "PipeMasterRoomHover";
    }

    public Guid GetServerId()
    {
        return _serverGuid;
    }

    public string GetDescription()
    {
        return "Destaque de ambientes e aparelhos do Lançamento de Água.";
    }

    public string GetVendorId()
    {
        return "PipeMaster";
    }

    public ExternalServiceId GetServiceId()
    {
        return ExternalServices.BuiltInExternalServices.DirectContext3DService;
    }

    public Outline GetBoundingBox(View view)
    {
        if (_roomMesh == null && _marcadorPt == null)
        {
            return null;
        }
        return new Outline(new XYZ(-1000.0, -1000.0, -1000.0), new XYZ(1000.0, 1000.0, 1000.0));
    }

    public bool UsesHandles()
    {
        return false;
    }
}
