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

namespace ClrPlus.Core.Tasks {
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using Exceptions;
    using Extensions;

    public static class Event<T> where T : class {
        private static T _emptyDelegate;

        /// <summary>
        ///     Gets the parameter types of a Delegate
        /// </summary>
        /// <param name="d"> The d. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        private static Type[] GetDelegateParameterTypes(Type d) {
            if (d.BaseType != typeof (MulticastDelegate)) {
                throw new ApplicationException("Not a delegate.");
            }

            var invoke = d.GetMethod("Invoke");
            if (invoke == null) {
                throw new ApplicationException("Not a delegate.");
            }

            var parameters = invoke.GetParameters();
            var typeParameters = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) {
                typeParameters[i] = parameters[i].ParameterType;
            }
            return typeParameters;
        }

        /// <summary>
        ///     Gets the Return type of a delegate
        /// </summary>
        /// <param name="d"> The d. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        private static Type GetDelegateReturnType(Type d) {
            if (d.BaseType != typeof (MulticastDelegate)) {
                throw new ApplicationException("Not a delegate.");
            }

            MethodInfo invoke = d.GetMethod("Invoke");
            if (invoke == null) {
                throw new ApplicationException("Not a delegate.");
            }

            return invoke.ReturnType;
        }

        /// <summary>
        ///     Returns a delegate that does nothing, and returns default(T) that can be used without having to check to see if the delegate is null.
        /// </summary>
        private static T EmptyDelegate {
            get {
                if (_emptyDelegate == null) {
                    Type delegateReturnType = GetDelegateReturnType(typeof (T));
                    Type[] delegateParameterTypes = GetDelegateParameterTypes(typeof (T));

                    var dynamicMethod = new DynamicMethod(string.Empty, delegateReturnType, delegateParameterTypes);
                    ILGenerator il = dynamicMethod.GetILGenerator();

                    if (delegateReturnType.FullName != "System.Void") {
                        if (delegateReturnType.IsValueType) {
                            il.Emit(OpCodes.Ldc_I4, 0);
                        } else {
                            il.Emit(OpCodes.Ldnull);
                        }
                    }
                    il.Emit(OpCodes.Ret);
                    _emptyDelegate = dynamicMethod.CreateDelegate(typeof (T)) as T;
                }
                return _emptyDelegate;
            }
        }

        public static T Raise {
            get {
                try {
                    return (XTask.CurrentExecutingTask.GetEventHandler(typeof (T)) as T) ?? EmptyDelegate;
                } catch (Exception e) {
                    throw new ClrPlusException("A TaskBasedEvent thru an exception of type '{0}' for Delegate Type '{1}'".format(e.GetType(), typeof (T)), e);
                }
            }
        }

        public static T RaiseFirst {
            get {
                try {
                    var dlg = XTask.CurrentExecutingTask.GetEventHandler(typeof (T));
                    return dlg != null ? dlg.GetInvocationList().FirstOrDefault() as T : EmptyDelegate;
                } catch (Exception e) {
                    throw new ClrPlusException("A TaskBasedEvent thru an exception of type '{0}' for Delegate Type '{1}'".format(e.GetType(), typeof (T)), e);
                }
            }
        }
    }
}