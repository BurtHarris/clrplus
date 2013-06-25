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

namespace ClrPlus.Scripting.MsBuild.Utility {
    using System;
    using System.Linq;
    using ClrPlus.Core.Collections;

    public class StringPropertyList : ObservableList<string> {
        public StringPropertyList(Func<string> getter, Action<string> setter, bool append = false) {
            var initial = getter();
            if (!string.IsNullOrEmpty(initial)) {
                foreach (var i in initial.Split(';')) {
                    Add(i);
                }
            }
            if (append) {
                ListChanged += (source, args) => setter(this.Aggregate((current, each) => current + ";" + each));
            } else {
                ListChanged += (source, args) => setter(this.Reverse().Aggregate((current, each) => current + ";" + each));
            }
        }

        public StringPropertyList(Func<string> getter, Action<string> setter, Action<string> onAdded, bool append = false)
            : this(getter, setter, append) {
            ItemAdded += (source, args) => onAdded(args.item);
        }

    }
}