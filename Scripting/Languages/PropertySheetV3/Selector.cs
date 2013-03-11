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
    using System.Diagnostics;
    using Core.Exceptions;
    using Core.Extensions;

    [DebuggerDisplay("Selector = {Name}[{Parameter}]")]
    public class Selector {
        public static Selector Empty = new Selector(string.Empty);

        public readonly string Name;
        public readonly string Parameter;
        private readonly int _hashCode;
        
        public Selector(string selector) :this(ParseName(selector), ParseParameter(selector)) {
        }

        private static string ParseParameter(string selector) {
            if (string.IsNullOrEmpty(selector)) {
                return null;
            }

            var p = selector.IndexOf('[');
            if (p > -1) {
                var c = selector.LastIndexOf(']');
                if (c == -1) {
                    throw new ClrPlusException("Missing ']' in Selector '{0}'".format(selector));
                }
                p++;
                return selector.Substring(p, c - p);
            }
            return null;
        }

        private static string ParseName(string selector) {
            if(string.IsNullOrEmpty(selector)) {
                return string.Empty;
            }
            var p = selector.IndexOf('[');
            return p > -1 ? ParseName(selector.Substring(0, p)) : selector;
        }

        public Selector(string name, string parameter) {
            Name = name;
            Parameter = parameter;
            _hashCode = this.CreateHashCode(Name, Parameter);
        }

        public bool HasParameter { get {
            return !string.IsNullOrEmpty(Parameter);
        }}

        public bool IsCompound {
            get {
                return Name.IndexOf('.') > 0;
            }
        }

        public Selector Prefix {
            get {
                var p = Name.IndexOf('.');
                return p > 0 ? new Selector (Name.Substring(0, p)): this;
            }
        }

        public Selector Suffix {
            get {
                var p = Name.IndexOf('.');
                return p <= 0 ? this : new Selector(Name.Substring(p + 1),Parameter);
            }
        }

        public override int GetHashCode() {
            return _hashCode;
        }

        public override bool Equals(object obj) {
            var s = obj as Selector;
            return s != null && (s.Name == Name && s.Parameter == Parameter);
        }

        public override string ToString() {
            return string.Format("{0}{1}", Name, string.IsNullOrEmpty(Parameter) ? "" : "[{0}]".format(Parameter));
        }

        public static implicit operator Selector(string s) {
            return new Selector(s);
        }

        public static implicit operator string(Selector s) {
            return s.ToString();
        }
    }
}