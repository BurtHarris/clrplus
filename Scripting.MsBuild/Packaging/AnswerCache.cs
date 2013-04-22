namespace ClrPlus.Scripting.MsBuild.Packaging {
    using System;
    using System.Collections.Generic;
    using Core.Extensions;

    public class AnswerCache<T> {
        private IDictionary<string, T> _cache = new Dictionary<string, T>();
        public T GetCachedAnswer(Func<T> calculate, params object[] inputs) {
            var k = inputs.CreateHashForObjects(inputs);
            if (!_cache.ContainsKey(k)) {
                _cache.Add(k, calculate());
            }
            return _cache[k];
        }
    }
}
