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

        private BlockingCollection<Message> _messages;



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
            foreach (var m in _messages.GetConsumingEnumerable())
            {
                switch (m.MessageType)
                {
                    case MessageType.Debug:
                        base.WriteDebug(m.Data as string);
                        break;
                    case MessageType.Error:
                        base.WriteError(m.Data as ErrorRecord);
                        break;
                    case MessageType.Object:
                        base.WriteObject(m.Data);
                        break;
                    case MessageType.ObjectAsEnumerable:
                        var data = m.Data as Tuple<object, bool>;
                        base.WriteObject(data.Item1, data.Item2);
                        break;
                    case MessageType.Progress:
                        base.WriteProgress(m.Data as ProgressRecord);
                        break;
                    case MessageType.Verbose:
                        base.WriteVerbose(m.Data as string);
                        break;
                    case MessageType.Warning:
                        base.WriteWarning(m.Data as string);
                        break;
                }
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
            _messages.Add(new Message { MessageType = MessageType.Object, Data = obj });
        }

        public new void WriteObject(object sendToPipeline, bool enumerateCollection)
        {
            _messages.Add(new Message { MessageType = MessageType.ObjectAsEnumerable, Data = new Tuple<object, bool>(sendToPipeline, enumerateCollection) });
        }

        public new void WriteProgress(ProgressRecord progressRecord)
        {
            _messages.Add(new Message { MessageType = MessageType.Progress, Data = progressRecord });
        }

        public new void WriteWarning(string text)
        {
            _messages.Add(new Message { MessageType = MessageType.Warning, Data = text });
        }

        public new void WriteDebug(string text)
        {
            _messages.Add(new Message { MessageType = MessageType.Debug, Data = text });
        }

        public new void WriteError(ErrorRecord errorRecord)
        {
            _messages.Add(new Message { MessageType = MessageType.Error, Data = errorRecord });
        }

        public new void WriteVerbose(string text)
        {
            _messages.Add(new Message { MessageType = MessageType.Verbose, Data = text });
        }


        private void SetupMessages()
        {
            _messages = new BlockingCollection<Message>();
        }

        private void EndLoop()
        {
            _messages.CompleteAdding();
        }


        class Message
        {
            public MessageType MessageType { get; set; }
            public object Data { get; set; }
        }


        enum MessageType
        {
            Debug,
            Object,
            ObjectAsEnumerable,
            Verbose,
            Warning,
            Error,
            Progress
        }
    }
}
