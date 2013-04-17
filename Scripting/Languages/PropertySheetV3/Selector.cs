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
    using Languages.PropertySheet;

    public class Selector {
        public static Selector Empty = new Selector(string.Empty, SourceLocation.Unknown);
        public readonly SourceLocation SourceLocation;

        public readonly string Name;
        public readonly string Parameter;
        public readonly string AfterTheParameter;

        private readonly int _hashCode;
        
        public Selector(string selector,SourceLocation sourceLocation)  {
            AfterTheParameter = null;

            // parse up to the first square bracket.
            if (string.IsNullOrEmpty(selector)) {
                Name = string.Empty;
                Parameter = null;
            } else {
                selector = selector.TrimStart('.');
                var p = selector.IndexOf('[');
                var c = selector.IndexOf(']', p + 1);
                if (p == -1) {
                    Name = selector;
                    Parameter = null;
                } else {
                    Name = p == 0 ? "*" : selector.Substring(0, p);
                    p++;
                    Parameter = selector.Substring(p, c - p).Trim();
                    if(c < selector.Length) {
                        AfterTheParameter = selector.Substring(c + 1);
                    }
                    
                }
            }
            SourceLocation = sourceLocation;
            
            _hashCode = AfterTheParameter == null ? this.CreateHashCode(Name, Parameter) : this.CreateHashCode(Name,Parameter,AfterTheParameter);
        }

        public Selector(string name, string parameter, SourceLocation sourceLocation, string afterTheParameter = null) {
            Name = name.Is() ? name : (parameter.Is() ? "*" : null);
            Parameter = parameter;
            SourceLocation = sourceLocation;
            AfterTheParameter = afterTheParameter;
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

        public bool IsGlobal {
            get {
                return Name.StartsWith("::");
            }
        }

        public Selector DeGlobaled {
            get {
                return IsGlobal ? new Selector(Name.TrimStart(':'), Parameter,SourceLocation, AfterTheParameter) : this;
            }
        }

        public Selector WithoutAfterTheParameter {
            get {
                return AfterTheParameter.Is() ? new Selector(Name,Parameter,SourceLocation) : this;
            }
        }

        public Selector Prefix {
            get {
                var p = Name.IndexOf('.');
                return p > 0 ? new Selector (Name.Substring(0, p),SourceLocation): this;
            }
        }

        public Selector Suffix {
            get {
                var p = Name.IndexOf('.');
                return p <= 0 ? this : new Selector(Name.Substring(p + 1),Parameter,SourceLocation, AfterTheParameter);
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
            if (Parameter == null) {
                return Name;
            }
            if (AfterTheParameter.Is()) {
                return string.Format("{0}[{1}]{2}", Name, Parameter,AfterTheParameter);
            }
            return string.Format("{0}[{1}]", Name, Parameter);
        }

        public static implicit operator Selector(string s) {
            return new Selector(s,SourceLocation.Unknown);
        }

        public static implicit operator string(Selector s) {
            return s.ToString();
        }

        public bool IsSpecialCase {
            get {
                return (Name == "*" || Name == string.Empty) && string.IsNullOrEmpty(Parameter);
            }
        }

        public Selector AfterSelector {
            get {
                return new Selector(AfterTheParameter, SourceLocation);
            }
        }

        public bool IsEmpty {
            get {
                return (string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Parameter) && string.IsNullOrEmpty(AfterTheParameter));
            }
        }
    }
}