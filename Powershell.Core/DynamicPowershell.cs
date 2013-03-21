//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors. 
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
    using System.Threading;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Core.Utility;

    public class DynamicPowershell : DynamicObject, IDisposable  {
        private readonly dynamic _runspacePool;
        private Runspace _runspace;
        private bool _runspaceBorrowedFromPool;
        private readonly bool _runspaceIsOwned;
        private DynamicPowershellCommand _currentCommand;
        private IDictionary<string, PSObject> _commands;
        private bool _runspaceWasLikeThatWhenIGotHere;

        private Runspace Runspace {
            get {
                lock (this) {
                    if (_runspace == null) {
                        // get one from the pool
                        AsyncCallback callback = ar => {
                            _runspace = _runspacePool.EndGetRunspace(ar);
                            _runspaceBorrowedFromPool = true;
                        };

                        _runspacePool.BeginGetRunspace(callback, this);

                        if (_runspace == null) {
                            throw new ClrPlusException("Runspace pool is null");
                        }
                    }

                    if (_runspace.RunspaceStateInfo.State == RunspaceState.BeforeOpen) {
                        _runspace.OpenAsync();
                    }

                    if (_runspace.RunspaceAvailability == RunspaceAvailability.AvailableForNestedCommand ||
                        _runspace.RunspaceAvailability == RunspaceAvailability.Busy) {
                        _runspaceWasLikeThatWhenIGotHere = true;
                    }
                }
                return _runspace;
            }
        }

        internal Pipeline CreatePipeline() {
            while(Runspace.RunspaceStateInfo.State == RunspaceState.Opening) {
                Thread.Sleep(5);
            }

            if (_runspaceWasLikeThatWhenIGotHere) {
                return Runspace.CreateNestedPipeline();
            } else {
                try {
                    TestIfInNestedPipeline();
                    return Runspace.CreatePipeline();

                } catch (Exception) {
                    _runspaceWasLikeThatWhenIGotHere = true;
                    return Runspace.CreateNestedPipeline();
                    
                }
            }
        }

        private void TestIfInNestedPipeline() {
            var pipeline = Runspace.CreatePipeline();
            //we're running a short command to verify that we're not in a nested pipeline
            pipeline.Commands.Add("get-alias");
            pipeline.Invoke();
        }

        public DynamicPowershell() {
            _runspace = RunspaceFactory.CreateRunspace();
            if(_runspace.RunspaceStateInfo.State == RunspaceState.BeforeOpen) {
                _runspace.OpenAsync();
            }
            _runspaceIsOwned = true;
        }

        public DynamicPowershell(Runspace runspace) {
            _runspace = runspace;

            if(_runspace.RunspaceAvailability == RunspaceAvailability.AvailableForNestedCommand ||
                   _runspace.RunspaceAvailability == RunspaceAvailability.Busy) {
                _runspaceWasLikeThatWhenIGotHere = true;
            }

            _runspaceIsOwned = false;
        }

        public DynamicPowershell(RunspacePool runspacePool) {
            _runspacePool = new AccessPrivateWrapper(runspacePool);
            if(_runspacePool.RunspacePoolStateInfo.State == RunspacePoolState.BeforeOpen) {
                _runspacePool.Open();
            }
            _runspaceIsOwned = false;
        }

        public void Dispose() {
            Wait();

            if(_runspaceBorrowedFromPool) {
                _runspacePool.ReleaseRunspace(_runspace);
                _runspace = null;
                return;
            }

            if(_runspaceIsOwned) {
                _runspace.Dispose();
                _runspace = null;
            }
        }

        private void AddCommandNames(IEnumerable<PSObject> cmdsOrAliases) {
            foreach(var item in cmdsOrAliases) {
                var cmdName = GetPropertyValue(item, "Name").ToLower();
                var name = cmdName.Replace("-", "");
                if(!string.IsNullOrEmpty(name)) {
                    _commands.Add(name, item);
                }
            }
        }

        private string GetPropertyValue(PSObject obj, string propName) {
            var property = obj.Properties.FirstOrDefault(prop => prop.Name == propName);
            return property != null ? property.Value.ToString() : null;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            Wait();

            try {
                // command 
                _currentCommand = new DynamicPowershellCommand(CreatePipeline()) {
                    Command = new Command(GetPropertyValue(LookupCommand(binder.Name), "Name"))
                };

                // parameters
                var unnamedCount = args.Length - binder.CallInfo.ArgumentNames.Count();
                var namedArguments = binder.CallInfo.ArgumentNames.Select((each, index) => new KeyValuePair<string, object>(each, args[index + unnamedCount]));
                _currentCommand.SetParameters(args.Take(unnamedCount), namedArguments);

                // invoke
                AsynchronouslyEnumerableList<ErrorRecord> errors;
                result = _currentCommand.InvokeAsyncIfPossible(out errors);

                return true;
            } catch(Exception e) {
                Console.WriteLine(e.Message);
                result = null;
                return false;
            }
        }

        public DynamicPowershellResult Invoke(string functionName, IEnumerable<PersistablePropertyInformation> elements, object objectContainingParameters, IDictionary<string, object> defaults, IDictionary<string, object> forced, out AsynchronouslyEnumerableList<ErrorRecord> errors) {
            Wait();

            // command
            _currentCommand = new DynamicPowershellCommand(CreatePipeline()) {
                Command = new Command(GetPropertyValue(LookupCommand(functionName), "Name"))
            };

            // parameters
            _currentCommand.SetParameters(elements, objectContainingParameters,defaults,forced);

            // invoke
            return _currentCommand.InvokeAsyncIfPossible(out errors);
        }

        public void Wait() {
            lock (this) {
                if (_currentCommand != null) {
                    _currentCommand.Wait();
                    _currentCommand = null;
                }
            }
        }

        public PSObject LookupCommand(string commandName) {
            var name = commandName.DashedToCamelCase().ToLower();
            if(_commands == null || !_commands.ContainsKey(name)) {

                _commands = new XDictionary<string, PSObject>();

                using (var pipeline = CreatePipeline()) {
                    pipeline.Commands.Add("get-command");
                    AddCommandNames(pipeline.Invoke());
                }

                using (var pipeline = CreatePipeline()) {
                    pipeline.Commands.Add("get-alias");
                    AddCommandNames(pipeline.Invoke());
                }

            }
            var item = _commands.ContainsKey(name) ? _commands[name] : null;
            if(item == null) {
                throw new ClrPlusException("Unable to find appropriate cmdlet.");
            }
            return item;
        }
    }
}