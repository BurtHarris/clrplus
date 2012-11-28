//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Tasks {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;

    public static class CoTask {
        private static readonly FieldInfo ParentTaskField = typeof (Task).GetField("m_parent", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance);
        private static readonly PropertyInfo CurrentTaskProperty = typeof (Task).GetProperty("InternalCurrent", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Static);
        private static readonly IDictionary<Task, List<Delegate>> Tasks = new XDictionary<Task, List<Delegate>>();
        private static readonly IDictionary<Task, Task> ParentTasks = new XDictionary<Task, Task>();
        private static readonly List<Delegate> NullTaskDelegates = new List<Delegate>();

        public static Task<T> AsResultTask<T>(this T result) {
            var x = new TaskCompletionSource<T>(TaskCreationOptions.AttachedToParent);
            x.SetResult(result);
            return x.Task;
        }

        public static Task<T> AsCanceledTask<T>(this T result) {
            var x = new TaskCompletionSource<T>(TaskCreationOptions.AttachedToParent);
            x.SetCanceled();
            return x.Task;
        }

        private static bool IsTaskReallyCompleted(Task task) {
            if (!task.IsCompleted) {
                return false;
            }

            return !(from child in ParentTasks.Keys where ParentTasks[child] == task && !IsTaskReallyCompleted(child) select child).Any();
        }

        public static void Collect() {
            lock (Tasks) {
                var completedTasks = (from t in Tasks.Keys where IsTaskReallyCompleted(t) select t).ToArray();
                foreach (var t in completedTasks) {
                    Tasks.Remove(t);
                }
            }

            lock (ParentTasks) {
                var completedTasks = (from t in ParentTasks.Keys where IsTaskReallyCompleted(t) select t).ToArray();
                foreach (var t in completedTasks) {
                    ParentTasks.Remove(t);
                }
            }
        }

        /// <summary>
        ///     This associates a child task with the parent task. This isn't necessary (and will have no effect) when the child task is created with AttachToParent in the creation/continuation options, but it does take a few cycles to validate that there is actually a parent, so don't call this when not needed.
        /// </summary>
        /// <param name="task"> </param>
        /// <returns> </returns>
        public static Task AutoManage(this Task task) {
            if (task == null) {
                return null;
            }
#if DEBUG
            if (task.GetParentTask() != null) {
                var stackTrace = new StackTrace(true);
                var frames = stackTrace.GetFrames();
                if (frames != null) {
                    foreach (var frame in frames) {
                        if (frame != null) {
                            var method = frame.GetMethod();
                            var fnName = method.Name;
                            var cls = method.DeclaringType;
                            if (cls != null) {
                                if (cls.Namespace != null && cls.Namespace.Contains("Tasks")) {
                                    continue;
                                }
                                // Logger.Warning("Unneccesary Automanage() in (in {2}.{3}) call at {0}:{1} ", frame.GetFileName(), frame.GetFileLineNumber(), cls.Name, fnName);
                            }
                            break;
                        }
                    }
                }
            }
#endif

            // if the task isn't associated with it's parent
            // we can insert a 'cheat'
            if (task.GetParentTask() == null) {
                lock (ParentTasks) {
                    var currentTask = CurrentTask;
                    if (currentTask != null) {
                        // the given task isn't attached to the parent.
                        // we can fake out attachment, by using the current task
                        ParentTasks.Add(task, currentTask);
                    }
                }
            }
            return task;
        }

        public static Task<T> AutoManage<T>(this Task<T> task) {
            AutoManage((Task)task);
            return task;
        }

        internal static Task CurrentTask {
            get {
                return CurrentTaskProperty.GetValue(null, null) as Task;
            }
        }

        internal static Task GetParentTask(this Task task) {
            if (task == null) {
                return null;
            }

            return ParentTaskField.GetValue(task) as Task ?? (ParentTasks.ContainsKey(task) ? ParentTasks[task] : null);
        }

        internal static Task ParentTask {
            get {
                return CurrentTask.GetParentTask();
            }
        }

        /// <summary>
        ///     Gets the message handler.
        /// </summary>
        /// <param name="task"> The task to get the message handler for. </param>
        /// <param name="eventDelegateHandlerType"> the delegate handler class </param>
        /// <returns> A delegate handler; null if there isn't one. </returns>
        /// <remarks>
        /// </remarks>
        internal static Delegate GetEventHandler(this Task task, Type eventDelegateHandlerType) {
            if (task == null) {
                return Delegate.Combine((from handlerDelegate in NullTaskDelegates where eventDelegateHandlerType.IsInstanceOfType(handlerDelegate) select handlerDelegate).ToArray());
            }

            // if the current task has an entry.
            if (Tasks.ContainsKey(task)) {
                var result = Delegate.Combine((from handler in Tasks[task] where handler.GetType().IsAssignableFrom(eventDelegateHandlerType) select handler).ToArray());
                return Delegate.Combine(result, GetEventHandler(task.GetParentTask(), eventDelegateHandlerType));
            }

            // otherwise, check with the parent.
            return GetEventHandler(task.GetParentTask(), eventDelegateHandlerType);
        }

        internal static Delegate AddEventHandler(this Task task, Delegate handler) {
            if (handler == null) {
                return null;
            }

            for (var count = 10; count > 0 && task.GetParentTask() == null; count--) {
                Thread.Sleep(10); // yeild for a bit
            }

            lock (Tasks) {
                if (task == null) {
                    NullTaskDelegates.Add(handler);
                } else {
                    if (!Tasks.ContainsKey(task)) {
                        Tasks.Add(task, new List<Delegate>());
                    }
                    Tasks[task].Add(handler);
                }
            }
            return handler;
        }

        internal static void RemoveEventHandler(this Task task, Delegate handler) {
            if (handler != null) {
                lock (Tasks) {
                    if (task == null) {
                        if (NullTaskDelegates.Contains(handler)) {
                            NullTaskDelegates.Remove(handler);
                        }
                    } else {
                        if (Tasks.ContainsKey(task) && Tasks[task].Contains(handler)) {
                            Tasks[task].Remove(handler);
                        }
                    }
                }
            }
        }

        public static void Iterate<TResult>(this TaskCompletionSource<TResult> tcs, IEnumerable<Task> asyncIterator) {
            var enumerator = asyncIterator.GetEnumerator();
            Action<Task> recursiveBody = null;
            recursiveBody = completedTask => {
                if (completedTask != null && completedTask.IsFaulted) {
                    tcs.TrySetException(completedTask.Exception.InnerExceptions);
                    enumerator.Dispose();
                } else if (enumerator.MoveNext()) {
                    enumerator.Current.ContinueWith(recursiveBody, TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.ExecuteSynchronously);
                } else {
                    enumerator.Dispose();
                }
            };
            recursiveBody(null);
        }

        public static void Ignore(this AggregateException aggregateException, Type type, Action saySomething = null) {
            foreach (var exception in aggregateException.Flatten().InnerExceptions) {
                if (exception.GetType() == type) {
                    if (saySomething != null) {
                        saySomething();
                    }
                    continue;
                }
                throw new ClrPlusException("Exception Caught: {0}\r\n    {1}".format(exception.Message, exception.StackTrace));
            }
        }
    }
}