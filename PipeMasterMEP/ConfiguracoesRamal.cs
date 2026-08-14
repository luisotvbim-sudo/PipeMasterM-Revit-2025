using System;

namespace PipeMasterMEP;

public static class ConfiguracoesRamal
{
    public static bool AlinharComPrimario
    {
        get
        {
            object obj = AppDomain.CurrentDomain.GetData("PipeMaster_AlinharPrimario");
            return obj == null || (bool)obj;
        }
        set
        {
            AppDomain.CurrentDomain.SetData("PipeMaster_AlinharPrimario", value);
        }
    }

    public static bool NivelarTampa
    {
        get
        {
            object obj = AppDomain.CurrentDomain.GetData("PipeMaster_NivelarTampa");
            return obj != null && (bool)obj;
        }
        set
        {
            AppDomain.CurrentDomain.SetData("PipeMaster_NivelarTampa", value);
        }
    }

    public static string Inclinacao
    {
        get
        {
            object obj = AppDomain.CurrentDomain.GetData("PipeMaster_Inclinacao");
            return (obj != null) ? ((string)obj) : "2.0";
        }
        set
        {
            AppDomain.CurrentDomain.SetData("PipeMaster_Inclinacao", value);
        }
    }
}
