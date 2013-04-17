namespace ClrPlus.Scripting.MsBuild.Utility {
    using System;

    public class UniqueStringPropertyList : StringPropertyList {
        public UniqueStringPropertyList(Func<string> getter, Action<string> setter) : base(getter,setter) {
        }

        public UniqueStringPropertyList(Func<string> getter, Action<string> setter, Action<string> onAdded)
            : base(getter, setter,onAdded) {
        }

        public override int Add(object value) {
            if (!Contains(value)) {
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