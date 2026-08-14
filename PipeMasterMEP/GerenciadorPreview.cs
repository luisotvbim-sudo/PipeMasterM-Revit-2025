using System;
using System.Collections.Generic;
using Autodesk.Revit.DB.ExternalService;

namespace PipeMasterMEP;

public static class GerenciadorPreview
{
    private static bool _registrado;

    public static PreviewRotasServer Server { get; private set; }

    public static void Iniciar()
    {
        if (Server == null)
        {
            Server = new PreviewRotasServer();
        }
        if (ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService) is MultiServerService mss)
        {
            if (!_registrado)
            {
                mss.AddServer(Server);
                _registrado = true;
            }
            IList<Guid> ativos = mss.GetActiveServerIds();
            if (!ativos.Contains(Server.GetServerId()))
            {
                ativos.Add(Server.GetServerId());
                mss.SetActiveServers(ativos);
            }
        }
    }
}
