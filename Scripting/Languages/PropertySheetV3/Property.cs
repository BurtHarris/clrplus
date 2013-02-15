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
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Extensions;

    internal static class ObjectExtensions {
        public static string SafeToString(this object o) {
            return o == null ? string.Empty : o.ToString();
        }
    }

    public class Property<T> : Property {
        private static readonly Func<T> _defaultGet = () => default(T);
        private static readonly Action<T> _defaultSet = v => {};
        private static readonly Action<T> _alreadySet = v => {};
        private static readonly PersistableInfo _persistableInfo = typeof (T).GetPersistableInfo();
        protected PropertyNode _propertyNode;

        static Property() {
        }

        private Func<T> _get;
        private Action<T> _set;

        public override PropertyNode PropertyNode {
            get {
                return _propertyNode ?? (_propertyNode = new PropertyNode(this));
            }
        }

        public override bool HasProperty {
            get {
                return _propertyNode != null;
            }
        }

        public override bool HasChangeList {
            get {
                return HasProperty && _propertyNode.Any();
            }
        }

        public Property(bool bla, Func<object> getAccessor, Action<object> setAccessor) {
            _get = () => (T)getAccessor();
            _set = v => setAccessor(v);
        }

        public Property(Func<T> getAccessor, Action<T> setAccessor) {
            _get = getAccessor;
            _set = setAccessor;
        }

        public Property(Func<T> getAccessor)
            : this(getAccessor, _defaultSet) {
        }

        public Property(Action<T> setAccessor)
            : this(_defaultGet, setAccessor) {
        }

        public Property(Func<T> getAccessor, Action<T> setAccessor, object routes)
            : this(getAccessor, setAccessor) {
            AddRoutes(routes);
        }

        public Property(Func<T> getAccessor, object routes)
            : this(getAccessor, _defaultSet, routes) {
        }

        public Property(Action<T> setAccessor, object routes)
            : this(_defaultGet, setAccessor, routes) {
        }

        public Property()
            : this(_defaultGet, _defaultSet) {
        }

        public override void CopyToBackingObject() {
            ResolveValue();
        }

        protected override void ResolveValue() {
            if (_set != _alreadySet) {
                if (HasChangeList && _set != _defaultSet) {
                    switch (_persistableInfo.PersistableCategory) {
                        case PersistableCategory.Nullable:
                        case PersistableCategory.String:
                        case PersistableCategory.Enumeration:
                        case PersistableCategory.Parseable:
                            _set((T)_persistableInfo.Type.ParseString(PropertyNode.Result.Value));
                            break;

                        case PersistableCategory.Array:
                        case PersistableCategory.Enumerable:
                            // take the collection of strings, and turn it into a collection of collectiontype.
                            var values = PropertyNode.Result.Values;
                            Console.WriteLine("HEY! NOT FINISHED THIS PART======Set collection values");
                            break;

                        default:
                            Console.WriteLine("UNKNOWN SET TYPE");
                            break;
                    }
                    _set = _alreadySet;
                }
            }
        }

        internal override IEnumerable<string> CurrentValues {
            get {
                if(_get == _defaultGet) {
                    if(HasProperty) {
                        return PropertyNode.Result.Values;
                    }
                    return new[] {
                        string.Empty
                    };
                }
                switch(_persistableInfo.PersistableCategory) {
                    case PersistableCategory.Nullable:
                    case PersistableCategory.String:
                    case PersistableCategory.Enumeration:
                    case PersistableCategory.Parseable:
                        return new[] {
                            _get().SafeToString()
                        };

                    case PersistableCategory.Array:
                    case PersistableCategory.Enumerable:
                        return (from object i in (IEnumerable)_get() select i.SafeToString()).ToList();

                    case PersistableCategory.Dictionary:
                        return (from object i in (IDictionary)_get() select i.SafeToString()).ToList();
                }

                // case PersistableCategory.Other:
                return new[] {
                    _get().SafeToString()
                };
            }
        }

        internal override string CurrentValue {
            get {
                if(_get == _defaultGet) {
                    // we're only getting the value from the backing property.
                    if(HasProperty) {
                        return PropertyNode.Result.Value;
                    }
                    return string.Empty;
                }

                switch(_persistableInfo.PersistableCategory) {
                    case PersistableCategory.Nullable:
                    case PersistableCategory.String:
                    case PersistableCategory.Enumeration:
                    case PersistableCategory.Parseable:
                        return _get().SafeToString();

                    case PersistableCategory.Array:
                    case PersistableCategory.Enumerable:
                        return (from object i in (IEnumerable)_get() select i.SafeToString()).Aggregate("", (current, each) => current + ", " + each).Trim(',', ' ');

                    case PersistableCategory.Dictionary:
                        return (from object i in (IDictionary)_get() select i.SafeToString()).Aggregate("", (current, each) => current + ", " + each).Trim(',', ' ');
                }

                // case PersistableCategory.Other:
                return _get().SafeToString();
            }
        }

        public override string Value {
            get {
                if (_get == _defaultGet) {
                    // we're only getting the value from the backing property.
                    if(HasProperty) {
                        return PropertyNode.Result.Value;
                    }
                    return string.Empty;
                }

                // ensure that we've done any work that we need to
                ResolveValue();

                return CurrentValue;
            }
        }

        public override IEnumerable<string> Values {
            get {
                if (_get == _defaultGet) {
                    if(HasProperty) {
                        return PropertyNode.Result.Values;
                    }
                    return new[] { string.Empty };
                }

                // ensure that we've done any work that we need to
                ResolveValue();

                return CurrentValues;
            }
        }
    }

    public abstract class Property : View {
        public abstract PropertyNode PropertyNode {get;}
        public abstract bool HasProperty {get;}
        public abstract bool HasChangeList {get;}
        public abstract string Value {get;}
        public abstract IEnumerable<string> Values {get;}
        internal abstract string CurrentValue { get; }
        internal abstract IEnumerable<string> CurrentValues { get; }
        protected abstract void ResolveValue();

        public static implicit operator string(Property rvalue) {
            return rvalue.Value;
        }

        public static implicit operator string[](Property rvalue) {
            return rvalue.Values.ToArray();
        }

        public static object Create(Type type, Func<object> getAccessor, Action<object> setAccessor) {
            Type genericType = typeof(Property<>).MakeGenericType(new[] { type });
            return Activator.CreateInstance(genericType,new object[] { true, getAccessor, setAccessor});
        }
    }
}