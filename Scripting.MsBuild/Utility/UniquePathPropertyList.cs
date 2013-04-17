namespace ClrPlus.Scripting.MsBuild.Utility {
    using System;

    public class UniquePathPropertyList : UniqueStringPropertyList {
        public UniquePathPropertyList(Func<string> getter, Action<string> setter)
            : base(getter, setter) {
        }

        public UniquePathPropertyList(Func<string> getter, Action<string> setter, Action<string> onAdded)
            : base(getter, setter, onAdded) {
        }

        public override int Add(object value) {
            return base.Add((object)(value.ToString().Replace(@"\\",@"\")));
        }

        public override void Add(string item) {
            base.Add(item.Replace(@"\\", @"\"));
        }

    }
}