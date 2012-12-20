//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Powershell.Core {
    using System;
    using System.Collections.Generic;
    using System.Management.Automation.Runspaces;
    using System.Threading;
    using System.Threading.Tasks;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Core.Tasks;

#if DISCARD_UNUSED
    public class OnDisposable<T> : IDisposable where T : IDisposable {
        private T _disposable;
        private readonly Action<T> _finalizer;

        public OnDisposable(T instance, Action<T> finalizer = null) {
            _disposable = instance;
            _finalizer = finalizer;
        }

        ~OnDisposable() {
            Dispose();
        }

        public T Value {
            get {
                return _disposable;
            }
        }

        public void Dispose() {
            lock (this) {
                if (!_disposable.Equals(default(T))) {
                    if (_finalizer != null) {
                        _finalizer(_disposable);
                    }

                    _disposable.Dispose();
                    _disposable = default(T);
                }
            }
        }

        public static implicit operator T(OnDisposable<T> obj) {
            return obj._disposable;
        }
    }
#endif 

    internal class DynamicPowershellCommand : IDisposable {
        internal Command Command;
        internal AsynchronouslyEnumerableList<object> Result = new AsynchronouslyEnumerableList<object>();

        internal Pipeline CommandPipeline;

        internal DynamicPowershellCommand(Pipeline pipeline) {
            CommandPipeline = pipeline;
        }

        public void Dispose() {
            lock (this) {
                if(CommandPipeline != null) {
                    CommandPipeline.Dispose();
                    CommandPipeline = null;
                }
            }
        }

        internal void Wait() {
            lock (this) {
                if (Result != null) {
                    Result.Wait();
                    Result = null;
                }
            }
        }
     
        internal void SetParameters(IEnumerable<object> unnamedArguments, IEnumerable<KeyValuePair<string, object>> namedArguments) {
            foreach(var arg in unnamedArguments) {
                Command.Parameters.Add(null, arg);
            }
            foreach(var arg in namedArguments) {
                Command.Parameters.Add(arg.Key, arg.Value );
            }
        }

        internal void SetParameters(IEnumerable<PersistablePropertyInformation> elements, object objectContainingParameters) {
            foreach(var arg in elements) {
                Command.Parameters.Add(arg.Name, arg.GetValue(objectContainingParameters, null));
            }
        }

        internal AsynchronouslyEnumerableList<object> InvokeAsyncIfPossible() {
            CommandPipeline.Commands.Add(Command);
            CommandPipeline.Input.Close();

            CommandPipeline.Output.DataReady += (sender, args) => {
                lock (Result) {
                    if (Result.IsCompleted) {
                        throw new ClrPlusException("Attempted to add to completed collection");
                    }

                    var items = CommandPipeline.Output.NonBlockingRead();
                    foreach (var item in items) {
                        Result.Add(item.ImmediateBaseObject);
                    }
                }
            };


            CommandPipeline.StateChanged += (x, y) => {
                switch(CommandPipeline.PipelineStateInfo.State) {
                    case PipelineState.NotStarted:
                        break;

                    case PipelineState.Completed:
                    // case PipelineState.Disconnected:
                    case PipelineState.Failed:
                    case PipelineState.Stopped:

                        while(!CommandPipeline.Output.EndOfPipeline) {
                            Thread.Sleep(1);
                        }

                        lock (Result) {
                            Result.Completed();
                            Dispose();
                        }
                        break;

                    case PipelineState.Stopping:
                        break;

                    case PipelineState.Running:
                        break;
                }
            };

            if(CommandPipeline.IsNested) {
                // goofy-powershell doesn't let nested pipelines async.
                CommandPipeline.Invoke();
            } else {
                CommandPipeline.InvokeAsync();
            }

            return Result;
        }
    }


  
#if FALSE
    public class DynamicPowershell : DynamicObject, IDisposable {
        private static OnDisposable<RunspacePool> _sharedRunspacePool;

        private OnDisposable<RunspacePool> _runspacePool;

        private EnumerableForMutatingCollection<PSObject, object> _result;
        private IDictionary<string, PSObject> _commands;
        private PowerShell _powershell;

        private RunspacePool RunspacePool {
            get {
                return _runspacePool.Value;
            }
        }

        ~DynamicPowershell() {
            Dispose();
        }

        public DynamicPowershell(OnDisposable<RunspacePool> pool = null) {
            if (pool == null) {
                InitSharedPool();
                pool = _sharedRunspacePool;
            }
            _runspacePool = pool;
            Reset();
            RefreshCommandList();

            
        }

        private string GetPropertyValue(PSObject obj, string propName) {
            var property = obj.Properties.FirstOrDefault(prop => prop.Name == propName);
            return property != null ? property.Value.ToString() : null;
        }

        private static void InitSharedPool() {
            if (_sharedRunspacePool == null) {
                _sharedRunspacePool = new OnDisposable<RunspacePool>(RunspaceFactory.CreateRunspacePool());
                _sharedRunspacePool.Value.Open();
            }
        }

        public void Reset() {
            lock (this) {
                _powershell = PowerShell.Create();
                _powershell.RunspacePool = RunspacePool;
            }
        }

        public void WaitForResult() {
            
            _result.Wait();
            _result = null;
        }

        private void AddCommandNames(IEnumerable<PSObject> cmdsOrAliases) {
            foreach (var item in cmdsOrAliases) {
                var cmdName = GetPropertyValue(item, "Name").ToLower();
                var name = cmdName.Replace("-", "");
                if (!string.IsNullOrEmpty(name)) {
                    _commands.Add(name, item);
                }
            }
        }

        private void RefreshCommandList() {
            lock (this) {
                _powershell.Commands.Clear();

                _commands = new XDictionary<string, PSObject>();
                AddCommandNames(_powershell.AddCommand("get-command").Invoke());

                _powershell.Commands.Clear();
                AddCommandNames(_powershell.AddCommand("get-alias").Invoke());
            }
        }

        public PSObject ResolveCommand(string name) {
            if (!_commands.ContainsKey(name)) {
                RefreshCommandList();
            }
            return _commands.ContainsKey(name) ? _commands[name] : null;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            try {
                SetCommandName(binder.Name.ToLower());
                var unnamedCount = args.Length - binder.CallInfo.ArgumentNames.Count();
                var namedArguments = binder.CallInfo.ArgumentNames.Select((each, index) => new KeyValuePair<string, object>(each, args[index + unnamedCount]));
                SetParameters(args.Take(unnamedCount), namedArguments);
                InvokeAsyncIfPossible();
                result = _result;
                return true;
            } catch (Exception) {
                result = null;
                return false;
            }
        }

        public IEnumerable<object> Invoke(string functionName, IEnumerable<PersistablePropertyInformation> elements, object objectContainingParameters) {
            SetCommandName(functionName);
            SetParameters(elements, objectContainingParameters);
            InvokeAsyncIfPossible();
            return _result;
        }

        private void SetCommandName(string functionName) {
            if (_result != null) {
                WaitForResult();
            }

            var item = ResolveCommand(functionName.ToLower());
            if (item == null) {
                throw new ClrPlusException("Unable to find appropriate cmdlet.");
            }

            var cmd = GetPropertyValue(item, "Name");
            _powershell.Commands.Clear();
            _powershell.AddCommand(cmd);
        }

        private PSDataCollection<PSObject> NewOutputCollection() {
            var output = new PSDataCollection<PSObject>();
            _result = new EnumerableForMutatingCollection<PSObject, object>(output, each => each.ImmediateBaseObject);
            output.DataAdded += (sender, eventArgs) => _result.ElementAdded();
            return output;
        }

        private void SetParameters(IEnumerable<object> unnamedArguments, IEnumerable<KeyValuePair<string, object>> namedArguments) {
            foreach (var arg in unnamedArguments) {
                _powershell.AddArgument(arg);
            }
            foreach (var arg in namedArguments) {
                _powershell.AddParameter(arg.Key, arg.Value);
            }
        }

        private void SetParameters(IEnumerable<PersistablePropertyInformation> elements, object objectContainingParameters) {
            foreach (var arg in elements) {
                _powershell.AddParameter(arg.Name, arg.GetValue(objectContainingParameters, null));
            }
        }

        private void InvokeAsyncIfPossible() {
            var output = NewOutputCollection();
            Task.Factory.StartNew(() => {
                var input = new PSDataCollection<object>();
                input.Complete();

                var asyncResult = _powershell.BeginInvoke(input, output);

                _powershell.EndInvoke(asyncResult);
                _result.Completed();
            });
        }

        public void Dispose() {
            _runspacePool = null; // will call dispose if this is the last instance using it.
        }
    }
#endif 
}