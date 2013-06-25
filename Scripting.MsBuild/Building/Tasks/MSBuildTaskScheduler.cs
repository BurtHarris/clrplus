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

namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// Provides a task scheduler that ensures a maximum concurrency level while 
    /// running on top of the ThreadPool.
    /// </summary>
    public class MSBuildTaskScheduler : TaskScheduler {
        /// <summary>Whether the current thread is processing work items.</summary>
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        /// <summary>The maximum concurrency level allowed by this scheduler.</summary>
        private readonly int _maxDegreeOfParallelism;

        /// <summary>The list of tasks to be executed.</summary>
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks) 

        /// <summary>Whether the scheduler is currently processing work items.</summary>
        private int _delegatesQueuedOrRunning; // protected by lock(_tasks) 

        /// <summary>
        ///     Initializes an instance of the LimitedConcurrencyLevelTaskScheduler class with the
        ///     specified degree of parallelism.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism provided by this scheduler.</param>
        public MSBuildTaskScheduler(int maxDegreeOfParallelism) {
            if (maxDegreeOfParallelism < 1) {
                maxDegreeOfParallelism = 1;
            }
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        /// <summary>Gets the maximum concurrency level supported by this scheduler.</summary>
        public override sealed int MaximumConcurrencyLevel {
            get {
                return _maxDegreeOfParallelism;
            }
        }

        public bool IsRunning {
            get {
                return _delegatesQueuedOrRunning > 0;
            }
        }

        /// <summary>Queues a task to the scheduler.</summary>
        /// <param name="task">The task to be queued.</param>
        protected override sealed void QueueTask(Task task) {
            // Add the task to the list of tasks to be processed.  If there aren't enough 
            // delegates currently queued or running to process tasks, schedule another. 
            lock (_tasks) {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism) {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        /// <summary>
        ///     Informs the ThreadPool that there's work to be executed for this scheduler.
        /// </summary>
        private void NotifyThreadPoolOfPendingWork() {
            ThreadPool.UnsafeQueueUserWorkItem(_ => {
                // Note that the current thread is now processing work items. 
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try {
                    // Process all available items in the queue. 
                    while (true) {
                        Task item;
                        lock (_tasks) {
                            // When there are no more items to be processed, 
                            // note that we're done processing, and get out. 
                            if (_tasks.Count == 0) {
                                --_delegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue 
                        base.TryExecuteTask(item);
                    }
                }
                    // We're done processing items on the current thread 
                finally {
                    _currentThreadIsProcessingItems = false;
                }
            }, null);
        }

        /// <summary>Attempts to execute the specified task on the current thread.</summary>
        /// <param name="task">The task to be executed.</param>
        /// <param name="taskWasPreviouslyQueued"></param>
        /// <returns>Whether the task could be executed on the current thread.</returns>
        protected override sealed bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
            // If this thread isn't already processing a task, we don't support inlining 
            if (!_currentThreadIsProcessingItems) {
                return false;
            }

            // If the task was previously queued, remove it from the queue 
            if (taskWasPreviouslyQueued) {
                TryDequeue(task);
            }

            // Try to run the task. 
            return base.TryExecuteTask(task);
        }

        /// <summary>Attempts to remove a previously scheduled task from the scheduler.</summary>
        /// <param name="task">The task to be removed.</param>
        /// <returns>Whether the task could be found and removed.</returns>
        protected override sealed bool TryDequeue(Task task) {
            lock (_tasks)
                return _tasks.Remove(task);
        }

        /// <summary>Gets an enumerable of the tasks currently scheduled on this scheduler.</summary>
        /// <returns>An enumerable of the tasks currently scheduled.</returns>
        protected override sealed IEnumerable<Task> GetScheduledTasks() {
            bool lockTaken = false;
            try {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) {
                    return _tasks.ToArray();
                } else {
                    throw new NotSupportedException();
                }
            } finally {
                if (lockTaken) {
                    Monitor.Exit(_tasks);
                }
            }
        }
    }
}