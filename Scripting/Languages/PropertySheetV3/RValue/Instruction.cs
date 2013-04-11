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
    using Languages.PropertySheet;

    public class Instruction : IValue {
        private readonly SourceLocation[] _sourceLocations = SourceLocation.Unknowns;
        public readonly string InstructionText;
        public IValueContext Context {get; set;}

        public Instruction(ObjectNode context, string instructionText, params SourceLocation[] sourceLocations) {
            InstructionText = instructionText;
            Context = context;
            _sourceLocations = sourceLocations;
        }

        public string GetValue(IValueContext currentContext) {
            return "Instruction as single value";
        }

        public IEnumerable<string> GetValues(IValueContext currentContext) {
            return "Instruction as a set of values".SingleItemAsEnumerable();
        }

        public static implicit operator string(Instruction rvalue) {
            return rvalue.GetValue(rvalue.Context);
        }

        public static implicit operator string[](Instruction rvalue) {
            return rvalue.GetValues(rvalue.Context).ToArray();
        }

        public IEnumerable<string> SourceText {
            get {
                yield return "";
            }
        }

        public IEnumerable<SourceLocation> SourceLocations {
            get {
                return _sourceLocations;
            }
        }
    }
}