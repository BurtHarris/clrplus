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

    [DebuggerDisplay("Selector = {Name}[{Parameter}]#{Id}<{Instruction}>")]
    public class Selector  {
        public string Name { get; set; }
        public string Parameter { get; set; }
        public string Instruction { get; set; }
     
        public static Selector Empty = new Selector {
            Name = string.Empty
        };

        
        public override int GetHashCode() {
            return this.CreateHashCode(Name, Parameter, Instruction);
        }

        public override bool Equals(object obj) {
            var s = obj as Selector;
            return s != null && (s.Name == Name && s.Parameter == Parameter && s.Instruction == Instruction);
        }

        public override string ToString() {
            return string.Format("{0}{1}{2}", Name,
                string.IsNullOrEmpty(Parameter) ? "" : "[{0}]".format(Parameter),
                string.IsNullOrEmpty(Instruction) ? "" : " <{0}>".format(Instruction));
        }
    }
}