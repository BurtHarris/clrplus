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
    using Core.Collections;
    using Core.Extensions;

    public class Change {
        internal RValue RValue {get; set;}
        internal RValueOperation RVOperation {get; set;}
    }

    public class Property : List<Change>, IHasMetadata {
        protected readonly Lazy<XDictionary<string, RValue>> Meta= new Lazy<XDictionary<string, RValue>>(() => new XDictionary<string, RValue>());

        public XDictionary<string, RValue> Metadata {
            get {
                return Meta.Value;
            }
        }
    }

    public class PropertyModel : IPropertyModel, IHasMetadata {
        protected readonly Lazy<XDictionary<string, Alias>> _aliases = new Lazy<XDictionary<string, Alias>>(() => new XDictionary<string, Alias>());
        protected readonly Lazy<XDictionary<Selector, Property>> _properties = new Lazy<XDictionary<Selector, Property>>(() => new XDictionary<Selector, Property>());
        protected readonly Lazy<XDictionary<Selector, IPropertyModel>> _children = new Lazy<XDictionary<Selector, IPropertyModel>>(() => new XDictionary<Selector, IPropertyModel>());
        protected readonly Lazy<XDictionary<string, IPropertyModel>> _imports = new Lazy<XDictionary<string, IPropertyModel>>(() => new XDictionary<string, IPropertyModel>());
        protected readonly Lazy<XDictionary<string, RValue>> Meta = new Lazy<XDictionary<string, RValue>>(() => new XDictionary<string, RValue>());
        protected readonly Selector Selector;
        public virtual PropertyModel Parent {get; protected set;}

        public PropertyModel() : this(Selector.Empty) {
        }

        public PropertyModel(Selector selector) {
            Selector = selector;
        }

        public virtual IPropertyModel CreatePropertyModel() {
            return new PropertyModel() {
                Parent = this
            };
        }

        public XDictionary<string, RValue> Metadata {
            get {
                return Meta.Value;
            }
        }

        public virtual void AddAlias(string aliasName, Selector aliasReference) {
            _aliases.Value.Add(aliasName, new Alias(aliasName, aliasReference));
        }

        public virtual Alias GetAlias(string aliasName) {
            throw new NotImplementedException();
        }

        public virtual IDictionary<string, IPropertyModel> Imports {
            get {
                return _imports.Value;
            }
        }

        private  Property GetPropertyNode(Selector path) {
            // first, find the appropriate Selector. 
            path = this.ResolveAliasesInPath(path);
            return _properties.Value.GetOrAdd(path, () => new Property());
        }

        public virtual Alias GetAlias(Selector containerContext, string aliasName) {
            // start at the container context, and move upwards looking for a 
            return null;
        }

        public virtual RValue ResolveMacro(string key) {
            return null;
        }

        public virtual void SetCollection(Selector path, RValue rvalue) {
            Console.WriteLine("SetCollection {0} :: {1} = `{2}`", FullPath, path , rvalue.Single().Value );
            GetPropertyNode(path).Add(new Change {
                RValue = rvalue,
                RVOperation = RValueOperation.CollectionAssignment
            });
        }

        public virtual void AddToCollection(Selector path, RValue rvalue) {
            Console.WriteLine("AddCollection {0} :: {1} = `{2}`", FullPath, path, rvalue.Single().Value);
            GetPropertyNode(path).Add(new Change {
                RValue = rvalue,
                RVOperation = RValueOperation.AddToCollection
            });
        }

        public virtual void SetValue(Selector path, RValue rvalue) {
            Console.WriteLine("SetValue {0} :: {1} = `{2}`", FullPath, path, rvalue.Single().Value);
            GetPropertyNode(path).Add(new Change {
                RValue = rvalue,
                RVOperation = RValueOperation.Assignment
            });
        }

        public virtual IPropertyModel this[Selector index] {
            get {
                return _children.Value.GetOrAdd(index, () => new PropertyModel(index) { Parent = this});
            }
            set {
                // allows the consumer to explictly set a new property model type at a particular spot in the model.
                _children.Value[index] = value;
                if (value is PropertyModel) {
                    (value as PropertyModel).Parent = this;
                }
            }
        }

        public virtual void AddMetadata(Selector collectionSelector, string identifier, RValue rValue) {
            Console.WriteLine("SetMetadata {0} :: {1} # {2} = `{3}`", FullPath , collectionSelector, identifier, rValue.Single().Value);
            GetPropertyNode(collectionSelector).Metadata.Add(identifier, rValue);
        }

        public virtual void AddMetadata(Selector collectionSelector, string identifier, IDictionary<string, RValue> values) {
            foreach (var key in values.Keys) {
                AddMetadata( collectionSelector, identifier+"#"+key , values[key]);
            }
        }

        public virtual void AddMetadata(string identifier, RValue rValue) {
            Console.WriteLine("SetMetadata {0} # {1} = `{2}`", FullPath, identifier, rValue.Single().Value);
            Metadata.Add(identifier, rValue);
        }

        public virtual void AddMetadata(string identifier, IDictionary<string, RValue> rValues) {
            foreach(var key in rValues.Keys) {
                AddMetadata(identifier + "#" + key, rValues[key]);
            }
        }

        protected string FullPath { get {
            if (Parent != null) {
                return Parent.FullPath + "//" + Selector;
            }
            return Selector.ToString();
        }}
    }
}