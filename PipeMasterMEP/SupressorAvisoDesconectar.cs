using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class SupressorAvisoDesconectar : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
        foreach (FailureMessageAccessor f in failures)
        {
            FailureSeverity severity = f.GetSeverity();
            if (severity == FailureSeverity.Warning || severity == FailureSeverity.Error)
            {
                if (f.HasResolutions())
                {
                    failuresAccessor.ResolveFailure(f);
                    return FailureProcessingResult.ProceedWithCommit;
                }
                failuresAccessor.DeleteWarning(f);
            }
        }
        return FailureProcessingResult.Continue;
    }
}
