using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoCriarTubo : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        ElementId defaultPipeTypeId = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).FirstElementId();
        if (defaultPipeTypeId == ElementId.InvalidElementId)
        {
            TaskDialog.Show("PipeMaster [M]", "Não foi encontrado nenhum Tipo de Tubo carregado neste projeto.");
            return Result.Failed;
        }
        PipeMasterOptionsViewModel viewModel = new PipeMasterOptionsViewModel();
        viewModel.Initialize(doc);
        if (viewModel.GetType().GetMethod("AjustarTema") != null)
        {
            viewModel.AjustarTema(commandData.Application.Application.BackgroundColor);
        }
        PipeMasterOptionsUI optionsControl = new PipeMasterOptionsUI
        {
            DataContext = viewModel
        };
        using (TomboOptionsBarSession.Begin(optionsControl))
        {
            while (true)
            {
                try
                {
                    Reference refPeca = uidoc.Selection.PickObject(ObjectType.Element, new FiltroPecasPipeMaster(), "PipeMaster [M]: Selecione a Conexão para criar tubo... (ESC para sair)");
                    if (!(doc.GetElement(refPeca) is FamilyInstance { MEPModel: not null } peca) || peca.MEPModel.ConnectorManager == null)
                    {
                        continue;
                    }
                    bool temConectorLivre = false;
                    foreach (Connector c in peca.MEPModel.ConnectorManager.Connectors)
                    {
                        if (c.ConnectorType != ConnectorType.Logical && !c.IsConnected)
                        {
                            temConectorLivre = true;
                            break;
                        }
                    }
                    if (!temConectorLivre)
                    {
                        continue;
                    }
                    ElementId nivelId = peca.LevelId;
                    if (nivelId == ElementId.InvalidElementId)
                    {
                        Level nivelSalvaVidas = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();
                        if (nivelSalvaVidas == null)
                        {
                            TaskDialog.Show("PipeMaster [M]", "Erro: Projeto sem Nível.");
                            return Result.Failed;
                        }
                        nivelId = nivelSalvaVidas.Id;
                    }
                    using Transaction t = new Transaction(doc, "PipeMaster: Criar Tubos");
                    t.Start();
                    foreach (Connector conectorPeca in peca.MEPModel.ConnectorManager.Connectors)
                    {
                        if (conectorPeca.ConnectorType == ConnectorType.Logical || conectorPeca.IsConnected)
                        {
                            continue;
                        }
                        double comprimentoTuboMetros = 0.5;
                        string textoComprimento = viewModel.Comprimento.Replace(",", ".");
                        if (double.TryParse(textoComprimento, NumberStyles.Any, CultureInfo.InvariantCulture, out var compUsuario))
                        {
                            comprimentoTuboMetros = compUsuario;
                        }
                        ElementId sysId = ElementId.InvalidElementId;
                        ElementId pipeTypeId = defaultPipeTypeId;
                        double diametroFinalInternal = conectorPeca.Radius * 2.0;
                        if (viewModel.IsPersonalizado)
                        {
                            if (viewModel.SistemaSelecionado != null)
                            {
                                sysId = viewModel.SistemaSelecionado.Id;
                            }
                            if (viewModel.TipoSelecionado != null)
                            {
                                pipeTypeId = viewModel.TipoSelecionado.Id;
                            }
                            if (double.TryParse(viewModel.DiametroSelecionado, out var diametroRibbonMm))
                            {
                                diametroFinalInternal = UnitUtils.ConvertToInternalUnits(diametroRibbonMm, UnitTypeId.Millimeters);
                            }
                        }
                        else
                        {
                            if (conectorPeca.MEPSystem != null)
                            {
                                sysId = conectorPeca.MEPSystem.GetTypeId();
                            }
                            if (sysId == ElementId.InvalidElementId)
                            {
                                List<PipingSystemType> sysTypes = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().ToList();
                                PipingSystemType matchingSys = sysTypes.FirstOrDefault((PipingSystemType s) => s.SystemClassification.ToString() == conectorPeca.PipeSystemType.ToString());
                                sysId = ((matchingSys != null) ? matchingSys.Id : sysTypes.First().Id);
                            }
                        }
                        XYZ direcaoSaida = conectorPeca.CoordinateSystem.BasisZ.Normalize();
                        XYZ pontoInicial = conectorPeca.Origin;
                        double comprimentoInternal = UnitUtils.ConvertToInternalUnits(comprimentoTuboMetros, UnitTypeId.Meters);
                        XYZ pontoFinal = pontoInicial + direcaoSaida * comprimentoInternal;
                        Pipe novoTubo = Pipe.Create(doc, sysId, pipeTypeId, nivelId, pontoInicial, pontoFinal);
                        ((Element)novoTubo).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametroFinalInternal);
                        foreach (Connector conectorTubo in novoTubo.ConnectorManager.Connectors)
                        {
                            if (conectorTubo.Origin.DistanceTo(pontoInicial) < 0.01)
                            {
                                try
                                {
                                    conectorPeca.ConnectTo(conectorTubo);
                                }
                                catch
                                {
                                }
                                break;
                            }
                        }
                    }
                    t.Commit();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex2)
                {
                    TaskDialog.Show("PipeMaster [M] - Erro", ex2.Message);
                    return Result.Failed;
                }
            }
        }
        return Result.Succeeded;
    }
}
