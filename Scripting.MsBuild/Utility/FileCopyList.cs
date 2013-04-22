namespace ClrPlus.Scripting.MsBuild.Utility {
    using System;
    using Core.Collections;

    public class FileCopyList : ObservableList<string> {
        public FileCopyList(Action<string> onAdded) {
            ItemAdded += (source, args) => onAdded(args.item);
        }

        public override int Add(object value) {
            if(!Contains(value)) {
                return base.Add(value);
            }
            return IndexOf(value);
        }

        public override void Add(string item) {
            if(!Contains(item)) {
                base.Add(item);
            }
        }

    }
}