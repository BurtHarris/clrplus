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

namespace ClrPlus.Scripting.Languages.PropertySheetV3.RValue {
    using System.Collections.Generic;
    using System.Linq;
    using Core.Extensions;

    public class Instruction : IValue {
        public readonly string InstructionText;
        public IValueContext Context {get; set;}

        public Instruction(ObjectNode context, string instructionText) {
            InstructionText = instructionText;
            Context = context;
        }

        public string Value {
            get {
                return "Instruction as single value";
            }
        }

        public IEnumerable<string> Values {
            get {
                return "Instruction as a set of values".SingleItemAsEnumerable();
            }
        }

        public static implicit operator string(Instruction rvalue) {
            return rvalue.Value;
        }

        public static implicit operator string[](Instruction rvalue) {
            return rvalue.Values.ToArray();
        }
    }
}