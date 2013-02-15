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
    using Core.Exceptions;
    using Core.Extensions;
    using RValue;

    public class Reference<T> : Reference {
        public Reference(Func<T> backingObjectAccessor) : base(() => backingObjectAccessor()) {
            _backingType = typeof (T);
        }

        public Reference(Func<object> backingObjectAccessor)
            : base(backingObjectAccessor) {
            _backingType = typeof(T);
        }

        public Reference(T backingObject) : base(backingObject) {
            _backingType = typeof(T);
        }

        public Reference(Func<object> backingObjectAccessor, object routes)
            : base(backingObjectAccessor) {
            _backingType = typeof(T);
            if(routes != null) {
                AddRoutes(routes);
            }
        }

        public Reference(Func<T> backingObjectAccessor, object routes)
            : base(() => backingObjectAccessor()) {
            _backingType = typeof(T);
            if(routes != null) {
                AddRoutes(routes);
            }
        }

        public Reference(object backingObject, object routes)
            : base(backingObject) {
            _backingType = typeof(T);
            if(routes != null) {
                AddRoutes(routes);
            }
        }
    }

    public class Reference : View {
        private Func<object> _backingObjectAccessor;
        protected Type _backingType;
        private Dictionary<string, PersistablePropertyInformation> _backingObjectPropertyInfo;

        public Reference(Func<object> backingObjectAccessor) {
            _backingObjectAccessor = backingObjectAccessor;
        }

        public Reference(object backingObject) {
            _backingObjectAccessor = () => backingObject;
        }

        public Reference(Func<object> backingObjectAccessor, object routes) {
            _backingObjectAccessor = backingObjectAccessor;
            if(routes != null) {
                AddRoutes(routes);
            }
        }

        public Reference(object backingObject, object routes) {
            _backingObjectAccessor = () => backingObject;
            if(routes != null) {
                AddRoutes(routes);
            }
        }

        public object BackingObject() {
            return _backingObjectAccessor();
        }

        public override View GetChildView(Selector selector) {
            // if there is an exact selector match:
            if(Routes.ContainsKey(selector)) {
                return Routes[selector](this, selector);
            }

            // otherwise, if we've got a parameter, try it without the parameter. 
            // HMM: if there isn't a match for this, it's gonna return just the base object you know...
            if(!string.IsNullOrEmpty(selector.Parameter)) {
                var parameterless = new Selector {
                    Name = selector.Name
                };
                if(Routes.ContainsKey(parameterless)) {
                    var result = Routes[parameterless](this, selector);
                    if(result != null) {
                        Routes[selector] = (context, sel) => result;
                        return result;
                    }
                }
            }

            // if there isn't anything there, see if there is a @default rule.
            if(Routes.ContainsKey(new Selector { Name = "default" })) {
                var result = Routes[selector](this, selector);
                if(result != null) {
                    Routes[selector] = (context, sel) => result;
                    return result;
                }
            }

            return LookupChildObject(selector);
        }

        protected void EnsurePropertyInfo() {
            if(_backingObjectPropertyInfo == null) {
                if(_backingType == null) {
                    var backingObject = _backingObjectAccessor();
                    if(backingObject != null) {
                        _backingType = backingObject.GetType();
                        _backingObjectAccessor = () => backingObject; // reduce the accessor to the result.
                    }
                    else {
                        // uh, this is bad, we don't know the destination type, and the object is null.
                        throw new ClrPlusException("Unknown child object type.");
                    }
                }
                _backingObjectPropertyInfo = _backingType.GetPersistableElements().ToDictionary(each => each.Name, each => each);
            }
        }

        public override View LookupChildObject(Selector selector) {
            // if there isn't a match at this point, I guess we should check the backing object for a child with that name.
            EnsurePropertyInfo();

            if(_backingObjectPropertyInfo.ContainsKey(selector.Name)) {

                var pi = _backingObjectPropertyInfo[selector.Name];

                switch (pi.ActualType.GetPersistableInfo().PersistableCategory) {
                    case PersistableCategory.Nullable:
                    case PersistableCategory.String:
                    case PersistableCategory.Enumeration:
                    case PersistableCategory.Parseable:
                        // parsable types should probably be returned as a Property.
                        return LookupChildProperty(selector);    

                    case PersistableCategory.Array:
                    case PersistableCategory.Enumerable:
                        return LookupChildProperty(selector);   

                    case PersistableCategory.Dictionary:
                        break;
                }
                
                // looks like the child is an object I guess.
                var result = (Reference)Create(pi.ActualType, ()=>pi.GetValue(_backingObjectAccessor(), null));
                //  var result = new Reference(pi.GetValue(_backingObjectAccessor(), null));
                Routes[selector] = (context, sel) => result;
                return result;
            }

            // if we *still* haven't found anything, I guess we'll make a new virtual view for that 
            var anonResult = new Reference(new object());
            Routes[selector] = (context, sel) => anonResult;
            return anonResult;
        }

        public override Property LookupChildProperty(Selector selector) {
            EnsurePropertyInfo();

            if(_backingObjectPropertyInfo.ContainsKey(selector.Name)) {
                // we're looking for a viewpropery object
                // we need to use a type-specific version that maps to the right type.
                // var result = new Property<object>(() => _backingObjectPropertyInfo[selector.Name].GetValue(backingObject, null), value => _backingObjectPropertyInfo[selector.Name].SetValue(backingObject, value, null));
                var pi = _backingObjectPropertyInfo[selector.Name];

                var result = (Property)Property.Create(pi.ActualType, () => pi.GetValue(_backingObjectAccessor(), null), value => pi.SetValue(_backingObjectAccessor(), value, null));
                Routes[selector] = (context, sel) => result;
                return result;
            }

            // if we *still* haven't found anything, I guess we'll make a new virtual view for that 
            var anonResult = new Property<IValue>();
            Routes[selector] = (context, sel) => anonResult;
            return anonResult;
        }

        public static object Create(Type type, Func<object> objectAccessor) {
            Type genericType = typeof(Reference<>).MakeGenericType(new[] { type });
            return Activator.CreateInstance(genericType, new object[] { objectAccessor });
        }

        public IEnumerable<string> Keys {
            get {
                EnsurePropertyInfo();

                return Routes.Keys.Select( each => each.ToString()).Union(_backingObjectPropertyInfo.Keys);
            }
        }

        public IEnumerable<Selector> GetChildSelectors() {
            return null;
        }

        public IEnumerable<Selector> GetPropertySelectors() {
            return null;
        }

        public IEnumerable<string> GetChildNames() {
            return GetChildSelectors().Select(each => each.Name).Distinct();
        }

        public IEnumerable<string> GetPropertyNames() {
            return GetPropertySelectors().Select(each => each.Name).Distinct();
        }

        public override void CopyToBackingObject() {
            foreach (var r in Routes.ToArray()) {
                var view = r.Value(this, r.Key);
                view.CopyToBackingObject();
            }
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
            return base.TryGetIndex(binder, indexes, out result);
        }
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value) {
            return base.TrySetIndex(binder, indexes, value);
        }
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            var selector = new Selector {
                Name = binder.Name
            };

            var child = GetChildView(selector);
            result = child is Property ? (object) (child as Property).Value : child;
            
            return true;
        }
        public override bool TrySetMember(SetMemberBinder binder, object value) {
            return base.TrySetMember(binder, value);
        }

#if false
        public override IEnumerable<string> GetDynamicMemberNames() {
            return base.GetDynamicMemberNames();
        }

        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object result) {
            return base.TryBinaryOperation(binder, arg, out result);
        }
        public override bool TryDeleteIndex(DeleteIndexBinder binder, object[] indexes) {
            return base.TryDeleteIndex(binder, indexes);
        }
        public override bool TryCreateInstance(CreateInstanceBinder binder, object[] args, out object result) {
            return base.TryCreateInstance(binder, args, out result);
        }
        public override bool TryDeleteMember(DeleteMemberBinder binder) {
            return base.TryDeleteMember(binder);
        }
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            return base.TryInvokeMember(binder, args, out result);
        }
        public override bool TryConvert(ConvertBinder binder, out object result) {
            return base.TryConvert(binder, out result);
        }
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
            return base.TryInvoke(binder, args, out result);
        }
        public override bool TryUnaryOperation(UnaryOperationBinder binder, out object result) {
            return base.TryUnaryOperation(binder, out result);
        }
#endif
    }
}