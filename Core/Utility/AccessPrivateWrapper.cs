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

namespace ClrPlus.Core.Utility {
    using System.Dynamic;
    using System.Linq;
    using System.Reflection;

    public static class PrivateWrapperExensions {
        public static dynamic AccessPrivate(this object obj) {
            return new AccessPrivateWrapper(obj);
        }
    }

    /// <summary>
    ///     A 10 minute wrapper to access private members, havn't tested in detail.
    ///     Use under your own risk - amazedsaint@gmail.com
    /// </summary>
    public class AccessPrivateWrapper : DynamicObject {
        /// <summary>
        ///     The object we are going to wrap
        /// </summary>
        private readonly object _wrapped;

        /// <summary>
        ///     Specify the flags for accessing members
        /// </summary>
        private static BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.Public;

        /// <summary>
        ///     Create a simple private wrapper
        /// </summary>
        public AccessPrivateWrapper(object o) {
            _wrapped = o;
        }

        /// <summary>
        ///     Create an instance via the constructor matching the args
        /// </summary>
        public static dynamic FromType(Assembly asm, string type, params object[] args) {
            var allt = asm.GetTypes();
            var t = allt.First(item => item.Name == type);

            var types = from a in args
                select a.GetType();

            //Gets the constructor matching the specified set of args
            var ctor = t.GetConstructor(flags, null, types.ToArray(), null);

            if (ctor != null) {
                var instance = ctor.Invoke(args);
                return new AccessPrivateWrapper(instance);
            }

            return null;
        }

        /// <summary>
        ///     Try invoking a method
        /// </summary>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            var types = args.Select(a => a != null ? a.GetType() : typeof(object));

            var method = _wrapped.GetType().GetMethod
                (binder.Name, flags, null, types.ToArray(), null) ?? _wrapped.GetType().GetMethod(binder.Name,flags );

            if (method == null) {
                return base.TryInvokeMember(binder, args, out result);
            }

            result = method.Invoke(_wrapped, args);
            return true;
        }

        /// <summary>
        ///     Tries to get a property or field with the given name
        /// </summary>
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            //Try getting a property of that name
            var prop = _wrapped.GetType().GetProperty(binder.Name, flags);

            if (prop == null) {
                //Try getting a field of that name
                var fld = _wrapped.GetType().GetField(binder.Name, flags);
                if (fld != null) {
                    result = fld.GetValue(_wrapped);
                    return true;
                }
                return base.TryGetMember(binder, out result);
            }
            result = prop.GetValue(_wrapped, null);
            return true;
        }

        /// <summary>
        ///     Tries to set a property or field with the given name
        /// </summary>
        public override bool TrySetMember(SetMemberBinder binder, object value) {
            var prop = _wrapped.GetType().GetProperty(binder.Name, flags);
            if (prop == null) {
                var fld = _wrapped.GetType().GetField(binder.Name, flags);
                if (fld != null) {
                    fld.SetValue(_wrapped, value);
                    return true;
                }
                return base.TrySetMember(binder, value);
            }

            prop.SetValue(_wrapped, value, null);
            return true;
        }
    }
}