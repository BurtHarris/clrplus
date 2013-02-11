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
    using System.Dynamic;
    using System.Linq;
    using System.Threading;
    using Core.Collections;
    using Core.Extensions;

    public class Change {
        public RValue Value { get; set; }
        public RValueOperation Operation { get; set; }
    }

    public class Property : List<Change>, IProperty {
        protected readonly Lazy<XDictionary<string, RValue>> _metadata = new Lazy<XDictionary<string, RValue>>(() => new XDictionary<string, RValue>());

        public IDictionary<string, RValue> Metadata {
            get {
                return _metadata.Value;
            }
        }
       
        public void SetCollection(RValue rvalue) {
            Add(new Change {
                Value = rvalue,
                Operation = RValueOperation.CollectionAssignment
            });
        }

        public void AddToCollection(RValue rvalue) {
            Add(new Change {
                Value = rvalue,
                Operation = RValueOperation.AddToCollection
            });
        }

        public void SetValue(RValue rvalue) {
            Add(new Change {
                Value = rvalue,
                Operation = RValueOperation.Assignment
            });
        }

        public RVSingle Value {get; private set;}
        public RVCollection Values {get; private set;}
    }
    
    public class DataModel : XDictionary<Selector, IItem>, IModel {
        private OrderedDictionaryProxy<string, IModel> _imports;
        protected readonly Lazy<XDictionary<string, Alias>> _aliases = new Lazy<XDictionary<string, Alias>>(() => new XDictionary<string, Alias>());
        protected readonly Lazy<XDictionary<string, RValue>> _metadata = new Lazy<XDictionary<string, RValue>>(() => new XDictionary<string, RValue>());

        public Selector Selector {get; private set;}
        public IModel Root {get; private set;}
        public INode Parent {get; private set;}

        public DataModel()
            : this(null, Selector.Empty) {
        }

        public DataModel(INode parent)
            : this(parent, Selector.Empty) {
        }

        public DataModel(INode parent, Selector selector) {
            Parent = parent;
            Root = parent == null ? this : parent.Root;
            Selector = selector;
        }

        public IProperty NewProperty() {
            return new Property();
        }

        public INode NewNode(Selector key) {
            return new DataModel(this, key);
        }

        public override IItem this[Selector key] {
            get {
                return this.GetOrAdd(key, () => new DataModel(this, key));
            }
            set {
                this.AddOrSet(key, value);
            }
        }

        public virtual IModel CreatePropertyModel() {
            return new DataModel(this);
        }

        public IDictionary<string, RValue> Metadata {
            get {
                return _metadata.Value;
            }
        }
       
        public virtual IOrderedDictionary<string, IModel> Imports {
            get {
                if (this != Root) {
                    return Root.Imports;
                }
                if (_imports == null) {
                    var imports = new OrderedDictionary<string, IModel>();
                    _imports  = new OrderedDictionaryProxy<string, IModel>(imports, get: key =>  imports.GetOrAdd(key, () => new DataModel()) );
                }
                return _imports;
            }
        }

        public dynamic MapTo(object backingObject, object routes) {
            return new ViewObject(backingObject, routes).AddRoutesFrom(this);
        }

        public virtual void AddAlias(string aliasName, Selector aliasReference) {
            _aliases.Value.Add(aliasName, new Alias(aliasName, aliasReference));
        }

        public virtual Alias GetAlias(string aliasName) {
            throw new NotImplementedException();
        }

        /*
        protected readonly Lazy<XDictionary<Selector, IProperty>> _properties = new Lazy<XDictionary<Selector, IProperty>>(() => new XDictionary<Selector, IProperty>());
        public IDictionary<Selector, IProperty> Properties {
            get {
                return _properties.Value;
            }
        }
        */

        protected string FullPath {
            get {
                if (Parent is DataModel) {
                    return ((Parent as DataModel).FullPath) + "//" + Selector;
                }
                return Selector.ToString();
            }
        }
    }
}