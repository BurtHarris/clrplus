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
    using Core.Extensions;

    public class Alias {
        private static int _priorityCounter = 1;

        public readonly string Name;
        public readonly Selector Reference;

        private int _priority;

        public Alias(string name, Selector reference) {
            Name = name;
            Reference = reference;
        }

        public bool Used {
            get {
                return _priority > 0;
            }
            set {
                if (value && _priority == 0) {
                    _priority = _priorityCounter++;
                }
            }
        }

        public override int GetHashCode() {
            return this.CreateHashCode(Name, Reference);
        }
    }
}