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
    using RValue;

    public class PropertyNode : List<PropertyNode.Change>, INode {
        public enum Operation {
            AddToCollection,
            Assignment,
            CollectionAssignment
        }

        private readonly Lazy<XDictionary<string, IValue>> _metadata = new Lazy<XDictionary<string, IValue>>(() => new XDictionary<string, IValue>());
        private readonly Property _view;
        private IValue _value;

        public PropertyNode() {
        }

        internal PropertyNode(Property view) {
            _view = view;
        }

        public IValue Result {
            get {
                if (Count == 0) {
                    return Scalar.Empty;
                }

                if (_value == null) {
                    var items = (this[0].Operation == Operation.AddToCollection && _view != null) ? new Collection(this[0].Value.Context, _view.CurrentValues.Select(each => new Scalar(this[0].Value.Context, each))) : new Collection(this[0].Value.Context);

                    foreach (var op in this) {
                        switch (op.Operation) {
                            case Operation.AddToCollection:
                                items.Add(op.Value);
                                break;

                            case Operation.Assignment:
                                items.Clear();
                                items.Add(op.Value);
                                break;

                            case Operation.CollectionAssignment:
                                items.Clear();
                                items.Add(op.Value);

                                break;
                        }
                    }

                    switch (items.Count) {
                        case 0:
                            _value = Scalar.Empty;
                            break;

                        case 1:
                            _value = items[0];
                            break;

                        default:
                            _value = items;
                            break;
                    }
                }
                return _value;
            }
        }

        public IDictionary<string, IValue> Metadata {
            get {
                return _metadata.Value;
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
    }
}