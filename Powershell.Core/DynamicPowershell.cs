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
    using System.Dynamic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Threading.Tasks;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;

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

    public static class PowershellExtensions {
        private static Runspace _defaultRunspace;

        public static dynamic GetDynamicPowershell(this Runspace runspace) {
            // do we have to do something different 
            return new DynamicPowershell2((runspace ?? _defaultRunspace ?? (_defaultRunspace = RunspaceFactory.CreateRunspace())).CreateNestedPipeline());
        }
    }


    public class DynamicPowershell2 : DynamicObject, IDisposable {
        private Pipeline _pipeline;
        private EnumerableForMutatingCollection<PSObject, object> _lastResult;
        private IDictionary<string, PSObject> _commands;

        public DynamicPowershell2(Pipeline pipeline) {
            _pipeline = pipeline;
        }

        public void Dispose() {
            _pipeline.Dispose();
            _pipeline = null;
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

        public PSObject ResolveCommand(string name) {
            if (!_commands.ContainsKey(name)) {
                RefreshCommandList();
            }
            return _commands.ContainsKey(name) ? _commands[name] : null;
        }

        private string GetPropertyValue(PSObject obj, string propName) {
            var property = obj.Properties.FirstOrDefault(prop => prop.Name == propName);
            return property != null ? property.Value.ToString() : null;
        }

        private void RefreshCommandList() {
            lock (this) {
                _pipeline.Commands.Clear();

                _commands = new XDictionary<string, PSObject>();
                _pipeline.Commands.Add("get-command");
                AddCommandNames(_pipeline.Invoke());

                _pipeline.Commands.Clear();
                _pipeline.Commands.Add("get-alias");

                AddCommandNames(_pipeline.Invoke());
            }
        }
    }

    public class DynamicPowershell : DynamicObject, IDisposable {
        private static OnDisposable<RunspacePool> _sharedRunspacePool;

        private OnDisposable<RunspacePool> _runspacePool;

        private EnumerableForMutatingCollection<PSObject, object> _lastResult;
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
            
            _lastResult.Wait();
            _lastResult = null;
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
                InvokeAsync();
                result = _lastResult;
                return true;
            } catch (Exception) {
                result = null;
                return false;
            }
        }

        public IEnumerable<object> Invoke(string functionName, IEnumerable<PersistablePropertyInformation> elements, object objectContainingParameters) {
            SetCommandName(functionName);
            SetParameters(elements, objectContainingParameters);
            InvokeAsync();
            return _lastResult;
        }

        private void SetCommandName(string functionName) {
            if (_lastResult != null) {
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
            _lastResult = new EnumerableForMutatingCollection<PSObject, object>(output, each => each.ImmediateBaseObject);
            output.DataAdded += (sender, eventArgs) => _lastResult.ElementAdded();
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

        private void InvokeAsync() {
            var output = NewOutputCollection();
            Task.Factory.StartNew(() => {
                var input = new PSDataCollection<object>();
                input.Complete();

                var asyncResult = _powershell.BeginInvoke(input, output);

                _powershell.EndInvoke(asyncResult);
                _lastResult.Completed();
            });
        }

        public void Dispose() {
            _runspacePool = null; // will call dispose if this is the last instance using it.
        }
    }
}