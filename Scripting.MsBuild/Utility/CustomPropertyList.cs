namespace ClrPlus.Scripting.MsBuild.Utility {
    using System;
    using System.Collections.Generic;
    using Core.Collections;

    public class CustomPropertyList : ObservableList<string> {
        public CustomPropertyList (Action<CustomPropertyList> onChanged,  Func<IEnumerable<string>> initialItems = null ) {
            if (initialItems != null) {
                foreach (var i in initialItems()) {
                    Add(i);
                }
            }
            ListChanged += (source, args) => onChanged(this);
        }
    }
}