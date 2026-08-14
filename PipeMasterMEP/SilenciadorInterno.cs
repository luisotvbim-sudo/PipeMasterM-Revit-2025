using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class SilenciadorInterno : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        failuresAccessor.DeleteAllWarnings();
        return FailureProcessingResult.Continue;
    }
}
