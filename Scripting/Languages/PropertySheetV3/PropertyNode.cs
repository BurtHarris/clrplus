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
        internal Action<ICanSetBackingValue,IValueContext> SetResult;
        internal Action<ICanSetBackingValues,IValueContext> SetResults;

        private Result _value;

        public PropertyNode() {
            SetResult = (i,vc) => {
                setResult(i,vc);
                SetResult = (x,v) => {};
            };
            SetResults = (i,vc) => {
                setResults(i,vc);
                SetResults = (x,v) => {};
            };
        }

        internal string GetValue(IValueContext currentContext) {
            if (_value == null) {
                _value = new Result();
                setResult(_value,currentContext);
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

        internal IEnumerable<string> GetValues(IValueContext currentContext) {
            if (_value == null) {
                _value = new Result();
                setResults(_value,currentContext);
            }
            return _value;
        }

        public Lazy<IDictionary<string, IValue>> Metadata {
            get {
                return _metadata;
            }
        }

        private void setResult(ICanSetBackingValue targetObject,IValueContext context) {
            if (Count > 0) {
                foreach (var op in this) {
                    switch (op.Operation) {
                        case Operation.AddToCollection:
                            if (op.Value is Scalar) {
                                targetObject.AddValue(op.Value.GetValue(context));
                            } else {
                                foreach (var i in op.Value.GetValues(context)) {
                                    targetObject.AddValue(i);
                                }
                            }
                            break;

                        case Operation.Assignment:
                            targetObject.SetValue(op.Value.GetValue(context));
                            break;

                        case Operation.CollectionAssignment:
                            targetObject.Reset();
                            foreach (var i in op.Value.GetValues(context)) {
                                targetObject.AddValue(i);
                            }

                            break;
                    }
                }
            }
        }

        private void setResults(ICanSetBackingValues targetObject,IValueContext context) {
            if (Count > 0) {
                foreach (var op in this) {
                    switch (op.Operation) {
                        case Operation.AddToCollection:
                            if(op.Value is Scalar) {
                                targetObject.AddValue(op.Value.GetValue(context));
                            }
                            else {
                                foreach(var i in op.Value.GetValues(context)) {
                                    targetObject.AddValue(i);
                                }
                            }
                            break;

                        case Operation.Assignment:
                        case Operation.CollectionAssignment:
                            targetObject.Reset();
                            foreach (var i in op.Value.GetValues(context)) {
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

        public IEnumerable<string> GetSourceText(int indent) {
            /*
            if (_metadata.IsValueCreated) {
                // write metadata out.
                var categories = (_metadata.Value.Keys.Select(key => new {
                    key,
                    dot = key.IndexOf('.')
                }).Where(each => each.dot >= 0).Select(each => each.key.Substring(each.dot))).Distinct().ToCacheEnumerable();

                foreach (var c in categories) {
                    var category = c;
                    var l = category.Length;

                    yield return 
                    
                    var items = _metadata.Value.Keys.Where(each => each.StartsWith(category)).Select(each => new {
                        key = each.Substring(l + 1),
                        value = _metadata.Value[each].SourceText.Aggregate((e, cur) => e + cur)
                    });

                }

                foreach (var m in _metadata.Value.Keys) {
                    var i = m.IndexOf('.');
                    if (i < 0) {
                        yield
                    }
                }
            }
            */
            yield return "";
        }
    }

    public class ExpansionPropertyNode : PropertyNode {
        public ObjectIterator ObjectIterator {
            get; set;
        }
    }
}