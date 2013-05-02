using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Powershell.Core
{
    using System.Collections.Concurrent;
    using System.Management.Automation;
    using System.Threading.Tasks;

    public abstract class AsyncCmdlet : PSCmdlet
    {
        private BlockingCollection<Action> _messages;

        public virtual void BeginProcessingAsync()
        {

        }

        public virtual void EndProcessingAsync()
        {

        }

        public virtual void ProcessRecordAsync()
        {

        }

        private void ProcessMessages()
        {
            foreach (var m in _messages.GetConsumingEnumerable()) {
                m();
            }

        }

        protected override void BeginProcessing()
        {
            SetupMessages();
            Task.Factory.StartNew(() =>
            {
                BeginProcessingAsync();
                EndLoop();
            });
            ProcessMessages();
        }


        protected override void EndProcessing()
        {
            SetupMessages();
            Task.Factory.StartNew(() =>
            {
                EndProcessingAsync();
                EndLoop();
            });
            ProcessMessages();
        }

        protected override void ProcessRecord()
        {
            SetupMessages();
            Task.Factory.StartNew(() =>
            {
                ProcessRecordAsync();
                EndLoop();
            });
            ProcessMessages();
        }


        public new void WriteObject(object obj)
        {

            _messages.Add(() => base.WriteObject(obj));
        }

        public new void WriteObject(object sendToPipeline, bool enumerateCollection)
        {
            _messages.Add(() => base.WriteObject(sendToPipeline, enumerateCollection));
        }

        public new void WriteProgress(ProgressRecord progressRecord)
        {
            _messages.Add(() => base.WriteProgress(progressRecord));
        }

        public new void WriteWarning(string text)
        {
            _messages.Add(() => base.WriteWarning(text));
        }

        public new void WriteDebug(string text)
        {
            _messages.Add(() => base.WriteDebug(text));
        }

        public new void WriteError(ErrorRecord errorRecord)
        {
            _messages.Add(() => base.WriteError(errorRecord));
        }

        public new void WriteVerbose(string text)
        {
            _messages.Add(() => base.WriteDebug(text));
        }


        private void SetupMessages()
        {
            _messages = new BlockingCollection<Action>();
        }

        private void EndLoop()
        {
            _messages.CompleteAdding();
        }
       
    }
}
