using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class SupressorAvisoTombo : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
        foreach (FailureMessageAccessor f in failures)
        {
            if (f.GetSeverity() == FailureSeverity.Warning || f.GetSeverity() == FailureSeverity.Error)
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
