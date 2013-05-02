using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScratchCmdlet
{
    using System.Management.Automation;
    using System.Threading;
    using ClrPlus.Powershell.Core;
    [Cmdlet(AllVerbs.Get, "Scratch")]
    public class GetScratchCmdlet : AsyncCmdlet
    {
        public override void BeginProcessingAsync()
        {
            for (int i = 0; i < 100; i++) {
               WriteDebug("DEBUG for "+ i);
               WriteVerbose("VERBOSE for " + i);
               WriteObject(i);
               WriteWarning("WARNING for " + i);
               WriteError(new ErrorRecord(new Exception(), "Error for " + i, ErrorCategory.ConnectionError, null));
               WriteVerbose("VERBOSE for " + i);
               WriteProgress(new ProgressRecord(0, "Progress", "Progress for Test " + i) { PercentComplete =  (int)(((i) / (double)100) * 100)});

               Thread.Sleep(500);
            }
        }
    }
}
