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

namespace ClrPlus.Scripting.Languages.PropertySheetV3 {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Collections;
    using Mapping;
    using RValue;

    public class PropertyNode : List<PropertyNode.Change>, INode {
        public enum Operation {
            AddToCollection,
            Assignment,
            CollectionAssignment,
            Clear
        }

        private readonly Lazy<IDictionary<string, IValue>> _metadata = new Lazy<IDictionary<string, IValue>>(() => new XDictionary<string, IValue>());
        internal Action<ICanSetBackingValue> SetResult;
        internal Action<ICanSetBackingValues> SetResults;

        private Result _value;

        public PropertyNode() {
            SetResult = (i) => {
                setResult(i);
                SetResult = (x) => {};
            };
            SetResults = (i) => {
                setResults(i);
                SetResults = (x) => {};
            };
        }

        internal string Value {
            get {
                if (_value == null) {
                    _value = new Result();
                    setResult(_value);
                }

                switch (_value.Count) {
                    case 0:
                        return string.Empty;

                    case 1:
                        return _value.First();

                    default:
                        return _value.Aggregate("", (current, each) => current + ", " + each).Trim(',', ' ');
                }
            }
        }

        internal IEnumerable<string> Values {
            get {
                if (_value == null) {
                    _value = new Result();
                    setResults(_value);
                }
                return _value;
            }
        }

        public Lazy<IDictionary<string, IValue>> Metadata {
            get {
                return _metadata;
            }
        }

        private void setResult(ICanSetBackingValue targetObject) {
            if (Count > 0) {
                foreach (var op in this) {
                    switch (op.Operation) {
                        case Operation.AddToCollection:
                            targetObject.AddValue(op.Value.Value);
                            break;

                        case Operation.Assignment:
                            targetObject.SetValue(op.Value.Value);
                            break;

                        case Operation.CollectionAssignment:
                            targetObject.Reset();
                            foreach (var i in op.Value.Values) {
                                targetObject.AddValue(i);
                            }

                            break;
                    }
                }
            }
        }

        private void setResults(ICanSetBackingValues targetObject) {
            if (Count > 0) {
                foreach (var op in this) {
                    switch (op.Operation) {
                        case Operation.AddToCollection:
                            targetObject.AddValue(op.Value.Value);
                            break;

                        case Operation.Assignment:
                        case Operation.CollectionAssignment:
                            targetObject.Reset();
                            foreach (var i in op.Value.Values) {
                                targetObject.AddValue(i);
                            }
                            break;
                    }
                }
            }
        }

        public void SetCollection(IValue rvalue) {
            Add(new Change {
                Value = rvalue,
                Operation = Operation.CollectionAssignment
            });
        }

        public void AddToCollection(IValue rvalue) {
            Add(new Change {
                Value = rvalue,
                Operation = Operation.AddToCollection
            });
        }

        public void SetValue(IValue rvalue) {
            Add(new Change {
                Value = rvalue,
                Operation = Operation.Assignment
            });
        }

        public class Change {
            public IValue Value {get; set;}
            public Operation Operation {get; set;}
        }

        protected class Result : List<string>, ICanSetBackingValue, ICanSetBackingValues {
            public void Reset() {
                Clear();
            }

            public void AddValue(string value) {
                Add(value);
            }

            public void SetValue(string value) {
                Reset();
                Add(value);
            }
        }
    }
}