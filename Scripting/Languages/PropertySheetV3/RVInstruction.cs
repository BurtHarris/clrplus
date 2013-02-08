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
    public class RVInstruction : RValue {
        public readonly string InstructionText;
        public RVInstruction(string instructionText) {
            InstructionText = instructionText;
        }

        public override RVSingle Single() {
            return new RVSingle("Instruction as single value");
            
        }

        public override RVCollection Collection() {
            return new RVSingle("Instruction as collection value").Collection();
            
        }
    }
}