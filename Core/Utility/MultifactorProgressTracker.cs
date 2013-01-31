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

namespace ClrPlus.Core.Utility {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public enum ProgressWeight {
        Tiny = 1,
        Low,
        Medium,
        Large = 5,
        Huge = 10,
        Massive = 20,
    }

    public class ProgressFactor {
        internal int Weight;
        private int _progress;

        public int Progress {
            get {
                return _progress;
            }
            set {
                if (value >= 0 && value <= 100 && _progress != value) {
                    _progress = value;
                    Tracker.Updated();
                }
            }
        }

        public ProgressFactor(ProgressWeight weight) {
            Weight = (int)weight;
        }

        internal MultifactorProgressTracker Tracker;
    }

    public class MultifactorProgressTracker : IEnumerable {
        private readonly List<ProgressFactor> _factors = new List<ProgressFactor>();
        private int _total;
        public int Progress {get; private set;}

        public delegate void Changed(int progress);

        public event Changed ProgressChanged;

        public void Updated() {
            var progress = _factors.Sum(each => each.Weight*each.Progress);
            progress = (progress*100/_total);

            if (Progress != progress) {
                Progress = progress;
                if (ProgressChanged != null) {
                    ProgressChanged(Progress);
                }
            }
        }

        public static implicit operator int(MultifactorProgressTracker progressTracker) {
            return progressTracker.Progress;
        }

        public void Add(ProgressFactor factor) {
            _factors.Add(factor);
            factor.Tracker = this;

            _total = _factors.Sum(each => each.Weight*100);
            Updated();
        }

        public IEnumerator GetEnumerator() {
            throw new NotImplementedException();
        }
    }
}