namespace ClrPlus.Scripting.MsBuild.Packaging {
    using System.Collections.Generic;

    class ComparableHashSet<T> : HashSet<T> {
        private static readonly IEqualityComparer<HashSet<T>> comparer = HashSet<T>.CreateSetComparer();

        public ComparableHashSet()
            : base() {
        }

        public ComparableHashSet(IEnumerable<T> e)
            : base(e) {
        }

        public override bool Equals(object obj) {
            if (!(obj is HashSet<T>))
                return false;
            return comparer.Equals(this, obj as HashSet<T>);
        }

        public override int GetHashCode() {
            return comparer.GetHashCode(this);
        }
    }
}