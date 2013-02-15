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
    using Core.Extensions;

    [DebuggerDisplay("Selector = {Name}[{Parameter}]")]
    public class Selector {
        public static Selector Empty = new Selector {
            Name = string.Empty
        };

        public string Name {get; set;}
        public string Parameter {get; set;}

        public bool IsCompound {
            get {
                return Name.IndexOf('.') > 0;
            }
        }

        public Selector Prefix {
            get {
                var p = Name.IndexOf('.');
                return p > 0 ? new Selector {
                    Name = Name.Substring(0, p)
                } : this;
            }
        }

        public Selector Suffix {
            get {
                var p = Name.IndexOf('.');
                return p <= 0 ? this : new Selector {
                    Name = Name.Substring(p + 1),
                    Parameter = Parameter
                };
            }
        }

        public override int GetHashCode() {
            return this.CreateHashCode(Name, Parameter);
        }

        public override bool Equals(object obj) {
            var s = obj as Selector;
            return s != null && (s.Name == Name && s.Parameter == Parameter);
        }

        public override string ToString() {
            return string.Format("{0}{1}", Name, string.IsNullOrEmpty(Parameter) ? "" : "[{0}]".format(Parameter));
        }

        public static implicit operator Selector(string s) {
            return new Selector {
                Name = s
            };
        }
    }
}