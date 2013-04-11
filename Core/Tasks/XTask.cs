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

namespace ClrPlus.Core.Tasks {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Collections;
    using Exceptions;
    using Extensions;

    public static class XTask {
        private static readonly FieldInfo _parentTaskField = typeof (Task).GetField("m_parent", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance);
        private static readonly PropertyInfo _currentTaskProperty = typeof (Task).GetProperty("InternalCurrent", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Static);
        private static readonly IDictionary<Task, List<Delegate>> _tasks = new XDictionary<Task, List<Delegate>>();
        private static readonly IDictionary<Task, Task> _parentTasks = new XDictionary<Task, Task>();
        private static readonly List<Delegate> _nullTaskDelegates = new List<Delegate>();

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

            return !(from child in _parentTasks.Keys where _parentTasks[child] == task && !IsTaskReallyCompleted(child) select child).Any();
        }

        public static void Collect() {
            lock (_tasks) {
                var completedTasks = (from t in _tasks.Keys where IsTaskReallyCompleted(t) select t).ToArray();
                foreach (var t in completedTasks) {
                    _tasks.Remove(t);
                }
            }

            lock (_parentTasks) {
                var completedTasks = (from t in _parentTasks.Keys where IsTaskReallyCompleted(t) select t).ToArray();
                foreach (var t in completedTasks) {
                    _parentTasks.Remove(t);
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

            // if the task isn't associated with it's parent
            // we can insert a 'cheat'
            if (task.GetParentTask() == null) {
                lock (_parentTasks) {
                    var currentTask = CurrentExecutingTask;
                    if (currentTask != null) {
                        // the given task isn't attached to the parent.
                        // we can fake out attachment, by using the current task
                        _parentTasks.Add(task, currentTask);
                    }
                }
            }
            return task;
        }

        public static Task<T> AutoManage<T>(this Task<T> task) {
            AutoManage((Task)task);
            return task;
        }

        private static Task _rootTask = new TaskCompletionSource<int>().Task;

        internal static Task CurrentExecutingTask {
            get {
                return (_currentTaskProperty.GetValue(null, null) as Task);// ?? _rootTask;
            }
        }

        internal static Task GetParentTask(this Task task) {
            if (task == null) {
                return null;
            }

            return _parentTaskField.GetValue(task) as Task ?? (_parentTasks.ContainsKey(task) ? _parentTasks[task] : null);
        }

        internal static Task ParentTask {
            get {
                return CurrentExecutingTask.GetParentTask();
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
                return Delegate.Combine((from handlerDelegate in _nullTaskDelegates where eventDelegateHandlerType.IsInstanceOfType(handlerDelegate) select handlerDelegate).ToArray());
            }

            // if the current task has an entry.
            if (_tasks.ContainsKey(task)) {
                var result = Delegate.Combine((from handler in _tasks[task] where handler.GetType().IsAssignableFrom(eventDelegateHandlerType) select handler).ToArray());
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

            lock (_tasks) {
                if (task == null) {
                    _nullTaskDelegates.Add(handler);
                } else {
                    if (!_tasks.ContainsKey(task)) {
                        _tasks.Add(task, new List<Delegate>());
                    }
                    _tasks[task].Add(handler);
                }
            }
            return handler;
        }

        internal static void RemoveEventHandler(this Task task, Delegate handler) {
            if (handler != null) {
                lock (_tasks) {
                    if (task == null) {
                        if (_nullTaskDelegates.Contains(handler)) {
                            _nullTaskDelegates.Remove(handler);
                        }
                    } else {
                        if (_tasks.ContainsKey(task) && _tasks[task].Contains(handler)) {
                            _tasks[task].Remove(handler);
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

#if FRAMEWORKv40

        /// <summary>
        ///     An already completed task.
        /// </summary>
        private static readonly Task _sPreCompletedTask = FromResult(false);

        /// <summary>
        ///     An already canceled task.
        /// </summary>
        private static Task s_preCanceledTask = ((Func<Task>)(() => {
            var result = new TaskCompletionSource<bool>();
            result.TrySetCanceled();
            return (Task)result.Task;
        }))();

        private const string ArgumentOutOfRange_TimeoutNonNegativeOrMinusOne = "The timeout must be non-negative or -1, and it must be less than or equal to Int32.MaxValue.";

        /// <summary>
        ///     Creates a task that runs the specified action.
        /// </summary>
        /// <param name="action">The action to execute asynchronously.</param>
        /// <returns>
        ///     A task that represents the completion of the action.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="action" /> argument is null.
        /// </exception>
        public static Task Run(Action action) {
            return Run(action, CancellationToken.None);
        }

        /// <summary>
        ///     Creates a task that runs the specified action.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="cancellationToken">The CancellationToken to use to request cancellation of this task.</param>
        /// <returns>
        ///     A task that represents the completion of the action.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="action" /> argument is null.
        /// </exception>
        public static Task Run(Action action, CancellationToken cancellationToken) {
            return Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).AutoManage();
        }

        /// <summary>
        ///     Creates a task that runs the specified function.
        /// </summary>
        /// <param name="function">The function to execute asynchronously.</param>
        /// <returns>
        ///     A task that represents the completion of the action.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="function" /> argument is null.
        /// </exception>
        public static Task<TResult> Run<TResult>(Func<TResult> function) {
            return Run(function, CancellationToken.None).AutoManage();
        }

        /// <summary>
        ///     Creates a task that runs the specified function.
        /// </summary>
        /// <param name="function">The action to execute.</param>
        /// <param name="cancellationToken">The CancellationToken to use to cancel the task.</param>
        /// <returns>
        ///     A task that represents the completion of the action.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="function" /> argument is null.
        /// </exception>
        public static Task<TResult> Run<TResult>(Func<TResult> function, CancellationToken cancellationToken) {
            return Task.Factory.StartNew(function, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).AutoManage();
        }

        /// <summary>
        ///     Creates a task that runs the specified function.
        /// </summary>
        /// <param name="function">The action to execute asynchronously.</param>
        /// <returns>
        ///     A task that represents the completion of the action.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="function" /> argument is null.
        /// </exception>
        public static Task Run(Func<Task> function) {
            return Run(function, CancellationToken.None).AutoManage();
        }

        /// <summary>
        ///     Creates a task that runs the specified function.
        /// </summary>
        /// <param name="function">The function to execute.</param>
        /// <param name="cancellationToken">The CancellationToken to use to request cancellation of this task.</param>
        /// <returns>
        ///     A task that represents the completion of the function.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="function" /> argument is null.
        /// </exception>
        public static Task Run(Func<Task> function, CancellationToken cancellationToken) {
            return Run<Task>(function, cancellationToken).Unwrap().AutoManage();
        }

        /// <summary>
        ///     Creates a task that runs the specified function.
        /// </summary>
        /// <param name="function">The function to execute asynchronously.</param>
        /// <returns>
        ///     A task that represents the completion of the action.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="function" /> argument is null.
        /// </exception>
        public static Task<TResult> Run<TResult>(Func<Task<TResult>> function) {
            return Run(function, CancellationToken.None).AutoManage();
        }

        /// <summary>
        ///     Creates a task that runs the specified function.
        /// </summary>
        /// <param name="function">The action to execute.</param>
        /// <param name="cancellationToken">The CancellationToken to use to cancel the task.</param>
        /// <returns>
        ///     A task that represents the completion of the action.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="function" /> argument is null.
        /// </exception>
        public static Task<TResult> Run<TResult>(Func<Task<TResult>> function, CancellationToken cancellationToken) {
            return Run<Task<TResult>>(function, cancellationToken).Unwrap().AutoManage();
        }

        /// <summary>
        ///     Starts a Task that will complete after the specified due time.
        /// </summary>
        /// <param name="dueTime">The delay in milliseconds before the returned task completes.</param>
        /// <returns>
        ///     The timed Task.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     The <paramref name="dueTime" /> argument must be non-negative or -1 and less than or equal to Int32.MaxValue.
        /// </exception>
        public static Task Delay(int dueTime) {
            return Delay(dueTime, CancellationToken.None).AutoManage();
        }

        /// <summary>
        ///     Starts a Task that will complete after the specified due time.
        /// </summary>
        /// <param name="dueTime">The delay before the returned task completes.</param>
        /// <returns>
        ///     The timed Task.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     The <paramref name="dueTime" /> argument must be non-negative or -1 and less than or equal to Int32.MaxValue.
        /// </exception>
        public static Task Delay(TimeSpan dueTime) {
            return Delay(dueTime, CancellationToken.None).AutoManage();
        }

        /// <summary>
        ///     Starts a Task that will complete after the specified due time.
        /// </summary>
        /// <param name="dueTime">The delay before the returned task completes.</param>
        /// <param name="cancellationToken">A CancellationToken that may be used to cancel the task before the due time occurs.</param>
        /// <returns>
        ///     The timed Task.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     The <paramref name="dueTime" /> argument must be non-negative or -1 and less than or equal to Int32.MaxValue.
        /// </exception>
        public static Task Delay(TimeSpan dueTime, CancellationToken cancellationToken) {
            var num = (long)dueTime.TotalMilliseconds;
            if (num < -1L || num > int.MaxValue) {
                throw new ArgumentOutOfRangeException("dueTime", "The timeout must be non-negative or -1, and it must be less than or equal to Int32.MaxValue.");
            } else {
                return Delay((int)num, cancellationToken);
            }
        }

        /// <summary>
        ///     Starts a Task that will complete after the specified due time.
        /// </summary>
        /// <param name="dueTime">The delay in milliseconds before the returned task completes.</param>
        /// <param name="cancellationToken">A CancellationToken that may be used to cancel the task before the due time occurs.</param>
        /// <returns>
        ///     The timed Task.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     The <paramref name="dueTime" /> argument must be non-negative or -1 and less than or equal to Int32.MaxValue.
        /// </exception>
        public static Task Delay(int dueTime, CancellationToken cancellationToken) {
            if (dueTime < -1) {
                throw new ArgumentOutOfRangeException("dueTime", "The timeout must be non-negative or -1, and it must be less than or equal to Int32.MaxValue.");
            }
            if (cancellationToken.IsCancellationRequested) {
                return s_preCanceledTask;
            }
            if (dueTime == 0) {
                return _sPreCompletedTask;
            }
            var tcs = new TaskCompletionSource<bool>();
            var ctr = new CancellationTokenRegistration();
            var timer = new Timer((self => {
                ctr.Dispose();
                ((Timer)self).Dispose();
                tcs.TrySetResult(true);
            }));
            if (cancellationToken.CanBeCanceled) {
                ctr = cancellationToken.Register((() => {
                    timer.Dispose();
                    tcs.TrySetCanceled();
                }));
            }
            timer.Change(dueTime, -1);
            return tcs.Task.AutoManage();
        }

        /// <summary>
        ///     Creates a Task that will complete only when all of the provided collection of Tasks has completed.
        /// </summary>
        /// <param name="tasks">The Tasks to monitor for completion.</param>
        /// <returns>
        ///     A Task that represents the completion of all of the provided tasks.
        /// </returns>
        /// <remarks>
        ///     If any of the provided Tasks faults, the returned Task will also fault, and its Exception will contain information
        ///     about all of the faulted tasks.  If no Tasks fault but one or more Tasks is canceled, the returned
        ///     Task will also be canceled.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        public static Task WhenAll(params Task[] tasks) {
            return WhenAll((IEnumerable<Task>)tasks).AutoManage();
        }

        /// <summary>
        ///     Creates a Task that will complete only when all of the provided collection of Tasks has completed.
        /// </summary>
        /// <param name="tasks">The Tasks to monitor for completion.</param>
        /// <returns>
        ///     A Task that represents the completion of all of the provided tasks.
        /// </returns>
        /// <remarks>
        ///     If any of the provided Tasks faults, the returned Task will also fault, and its Exception will contain information
        ///     about all of the faulted tasks.  If no Tasks fault but one or more Tasks is canceled, the returned
        ///     Task will also be canceled.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        public static Task<TResult[]> WhenAll<TResult>(params Task<TResult>[] tasks) {
            return WhenAll((IEnumerable<Task<TResult>>)tasks).AutoManage();
        }

        /// <summary>
        ///     Creates a Task that will complete only when all of the provided collection of Tasks has completed.
        /// </summary>
        /// <param name="tasks">The Tasks to monitor for completion.</param>
        /// <returns>
        ///     A Task that represents the completion of all of the provided tasks.
        /// </returns>
        /// <remarks>
        ///     If any of the provided Tasks faults, the returned Task will also fault, and its Exception will contain information
        ///     about all of the faulted tasks.  If no Tasks fault but one or more Tasks is canceled, the returned
        ///     Task will also be canceled.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        public static Task WhenAll(IEnumerable<Task> tasks) {
            return WhenAllCore(tasks, (Action<Task[], TaskCompletionSource<object>>)((completedTasks, tcs) => tcs.TrySetResult(null)));
        }

        /// <summary>
        ///     Creates a Task that will complete only when all of the provided collection of Tasks has completed.
        /// </summary>
        /// <param name="tasks">The Tasks to monitor for completion.</param>
        /// <returns>
        ///     A Task that represents the completion of all of the provided tasks.
        /// </returns>
        /// <remarks>
        ///     If any of the provided Tasks faults, the returned Task will also fault, and its Exception will contain information
        ///     about all of the faulted tasks.  If no Tasks fault but one or more Tasks is canceled, the returned
        ///     Task will also be canceled.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        public static Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks) {
            return WhenAllCore((tasks).Cast<Task>(), (Action<Task[], TaskCompletionSource<TResult[]>>)((completedTasks, tcs) => tcs.TrySetResult((completedTasks).Select((t => ((Task<TResult>)t).Result)).ToArray())));
        }

        /// <summary>
        ///     Creates a Task that will complete only when all of the provided collection of Tasks has completed.
        /// </summary>
        /// <param name="tasks">The Tasks to monitor for completion.</param>
        /// <param name="setResultAction">
        ///     A callback invoked when all of the tasks complete successfully in the RanToCompletion state.
        ///     This callback is responsible for storing the results into the TaskCompletionSource.
        /// </param>
        /// <returns>
        ///     A Task that represents the completion of all of the provided tasks.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        private static Task<TResult> WhenAllCore<TResult>(IEnumerable<Task> tasks, Action<Task[], TaskCompletionSource<TResult>> setResultAction) {
            if (tasks == null) {
                throw new ArgumentNullException("tasks");
            }
            var tcs = new TaskCompletionSource<TResult>();
            Task[] tasks1 = tasks as Task[] ?? tasks.ToArray();
            if (tasks1.Length == 0) {
                setResultAction(tasks1, tcs);
            } else {
                Task.Factory.ContinueWhenAll(tasks1, (completedTasks => {
                    List<Exception> local_0 = null;
                    bool local_1 = false;
                    foreach (Task item_0 in completedTasks) {
                        if (item_0.IsFaulted) {
                            AddPotentiallyUnwrappedExceptions(ref local_0, item_0.Exception);
                        } else if (item_0.IsCanceled) {
                            local_1 = true;
                        }
                    }
                    if (local_0 != null && local_0.Count > 0) {
                        tcs.TrySetException(local_0);
                    } else if (local_1) {
                        tcs.TrySetCanceled();
                    } else {
                        setResultAction(completedTasks, tcs);
                    }
                }), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).AutoManage();
            }
            return tcs.Task.AutoManage();
        }

        /// <summary>
        ///     Creates a Task that will complete when any of the tasks in the provided collection completes.
        /// </summary>
        /// <param name="tasks">The Tasks to be monitored.</param>
        /// <returns>
        ///     A Task that represents the completion of any of the provided Tasks.  The completed Task is this Task's result.
        /// </returns>
        /// <remarks>
        ///     Any Tasks that fault will need to have their exceptions observed elsewhere.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        public static Task<Task> WhenAny(params Task[] tasks) {
            return WhenAny((IEnumerable<Task>)tasks);
        }

        /// <summary>
        ///     Creates a Task that will complete when any of the tasks in the provided collection completes.
        /// </summary>
        /// <param name="tasks">The Tasks to be monitored.</param>
        /// <returns>
        ///     A Task that represents the completion of any of the provided Tasks.  The completed Task is this Task's result.
        /// </returns>
        /// <remarks>
        ///     Any Tasks that fault will need to have their exceptions observed elsewhere.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        public static Task<Task> WhenAny(IEnumerable<Task> tasks) {
            if (tasks == null) {
                throw new ArgumentNullException("tasks");
            }
            var tcs = new TaskCompletionSource<Task>();
            Task.Factory.ContinueWhenAny(tasks as Task[] ?? tasks.ToArray(), (completed => tcs.TrySetResult(completed)), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).AutoManage();
            return tcs.Task;
        }

        /// <summary>
        ///     Creates a Task that will complete when any of the tasks in the provided collection completes.
        /// </summary>
        /// <param name="tasks">The Tasks to be monitored.</param>
        /// <returns>
        ///     A Task that represents the completion of any of the provided Tasks.  The completed Task is this Task's result.
        /// </returns>
        /// <remarks>
        ///     Any Tasks that fault will need to have their exceptions observed elsewhere.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        public static Task<Task<TResult>> WhenAny<TResult>(params Task<TResult>[] tasks) {
            return WhenAny((IEnumerable<Task<TResult>>)tasks);
        }

        /// <summary>
        ///     Creates a Task that will complete when any of the tasks in the provided collection completes.
        /// </summary>
        /// <param name="tasks">The Tasks to be monitored.</param>
        /// <returns>
        ///     A Task that represents the completion of any of the provided Tasks.  The completed Task is this Task's result.
        /// </returns>
        /// <remarks>
        ///     Any Tasks that fault will need to have their exceptions observed elsewhere.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException">
        ///     The <paramref name="tasks" /> argument is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     The <paramref name="tasks" /> argument contains a null reference.
        /// </exception>
        public static Task<Task<TResult>> WhenAny<TResult>(IEnumerable<Task<TResult>> tasks) {
            if (tasks == null) {
                throw new ArgumentNullException("tasks");
            }
            var tcs = new TaskCompletionSource<Task<TResult>>();
            Task.Factory.ContinueWhenAny(tasks as Task<TResult>[] ?? tasks.ToArray(), (completed => tcs.TrySetResult(completed)), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return tcs.Task;
        }

        /// <summary>
        ///     Creates an already completed <see cref="T:System.Threading.Tasks.Task`1" /> from the specified result.
        /// </summary>
        /// <param name="result">The result from which to create the completed task.</param>
        /// <returns>
        ///     The completed task.
        /// </returns>
        public static Task<TResult> FromResult<TResult>(this TResult result) {
            var completionSource = new TaskCompletionSource<TResult>(result);
            completionSource.TrySetResult(result);
            return completionSource.Task;
        }

        /// <summary>
        ///     Adds the target exception to the list, initializing the list if it's null.
        /// </summary>
        /// <param name="targetList">The list to which to add the exception and initialize if the list is null.</param>
        /// <param name="exception">The exception to add, and unwrap if it's an aggregate.</param>
        private static void AddPotentiallyUnwrappedExceptions(ref List<Exception> targetList, Exception exception) {
            var aggregateException = exception as AggregateException;
            if (targetList == null) {
                targetList = new List<Exception>();
            }
            if (aggregateException != null) {
                targetList.Add(aggregateException.InnerExceptions.Count == 1 ? exception.InnerException : exception);
            } else {
                targetList.Add(exception);
            }
        }

#endif

#if FRAMEWORKv45
    /// <summary>
    /// Creates a task that runs the specified action.
    /// </summary>
    /// <param name="action">The action to execute asynchronously.</param>
    /// <returns>
    /// A task that represents the completion of the action.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="action"/> argument is null.</exception>
    public static Task Run(Action action)
    {
      return Task.Run(action);
    }

    /// <summary>
    /// Creates a task that runs the specified action.
    /// </summary>
    /// <param name="action">The action to execute.</param><param name="cancellationToken">The CancellationToken to use to request cancellation of this task.</param>
    /// <returns>
    /// A task that represents the completion of the action.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="action"/> argument is null.</exception>
    public static Task Run(Action action, CancellationToken cancellationToken)
    {
      return Task.Run(action,cancellationToken);
    }

    /// <summary>
    /// Creates a task that runs the specified function.
    /// </summary>
    /// <param name="function">The function to execute asynchronously.</param>
    /// <returns>
    /// A task that represents the completion of the action.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="function"/> argument is null.</exception>
    public static Task<TResult> Run<TResult>(Func<TResult> function)
    {
      return Task.Run<TResult>(function, CancellationToken.None);
    }

    /// <summary>
    /// Creates a task that runs the specified function.
    /// </summary>
    /// <param name="function">The action to execute.</param><param name="cancellationToken">The CancellationToken to use to cancel the task.</param>
    /// <returns>
    /// A task that represents the completion of the action.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="function"/> argument is null.</exception>
    public static Task<TResult> Run<TResult>(Func<TResult> function, CancellationToken cancellationToken)
    {
      return Task.Run(function, cancellationToken);
    }

    /// <summary>
    /// Creates a task that runs the specified function.
    /// </summary>
    /// <param name="function">The action to execute asynchronously.</param>
    /// <returns>
    /// A task that represents the completion of the action.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="function"/> argument is null.</exception>
    public static Task Run(Func<Task> function)
    {
      return Task.Run(function, CancellationToken.None);
    }

    /// <summary>
    /// Creates a task that runs the specified function.
    /// </summary>
    /// <param name="function">The function to execute.</param><param name="cancellationToken">The CancellationToken to use to request cancellation of this task.</param>
    /// <returns>
    /// A task that represents the completion of the function.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="function"/> argument is null.</exception>
    public static Task Run(Func<Task> function, CancellationToken cancellationToken)
    {
      return TaskExtensions.Unwrap(Task.Run<Task>(function, cancellationToken));
    }

    /// <summary>
    /// Creates a task that runs the specified function.
    /// </summary>
    /// <param name="function">The function to execute asynchronously.</param>
    /// <returns>
    /// A task that represents the completion of the action.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="function"/> argument is null.</exception>
    public static Task<TResult> Run<TResult>(Func<Task<TResult>> function)
    {
      return Task.Run<TResult>(function, CancellationToken.None);
    }

    /// <summary>
    /// Creates a task that runs the specified function.
    /// </summary>
    /// <param name="function">The action to execute.</param><param name="cancellationToken">The CancellationToken to use to cancel the task.</param>
    /// <returns>
    /// A task that represents the completion of the action.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="function"/> argument is null.</exception>
    public static Task<TResult> Run<TResult>(Func<Task<TResult>> function, CancellationToken cancellationToken)
    {
      return Task.Run(function, cancellationToken);
    }

    /// <summary>
    /// Starts a Task that will complete after the specified due time.
    /// </summary>
    /// <param name="dueTime">The delay in milliseconds before the returned task completes.</param>
    /// <returns>
    /// The timed Task.
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="dueTime"/> argument must be non-negative or -1 and less than or equal to Int32.MaxValue.
    ///             </exception>
    public static Task Delay(int dueTime)
    {
      return Task.Delay(dueTime, CancellationToken.None);
    }

    /// <summary>
    /// Starts a Task that will complete after the specified due time.
    /// </summary>
    /// <param name="dueTime">The delay before the returned task completes.</param>
    /// <returns>
    /// The timed Task.
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="dueTime"/> argument must be non-negative or -1 and less than or equal to Int32.MaxValue.
    ///             </exception>
    public static Task Delay(TimeSpan dueTime)
    {
      return Task.Delay(dueTime, CancellationToken.None);
    }

    /// <summary>
    /// Starts a Task that will complete after the specified due time.
    /// </summary>
    /// <param name="dueTime">The delay before the returned task completes.</param><param name="cancellationToken">A CancellationToken that may be used to cancel the task before the due time occurs.</param>
    /// <returns>
    /// The timed Task.
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="dueTime"/> argument must be non-negative or -1 and less than or equal to Int32.MaxValue.
    ///             </exception>
    public static Task Delay(TimeSpan dueTime, CancellationToken cancellationToken)
    {
      return Task.Delay(dueTime, cancellationToken);
    }

    /// <summary>
    /// Starts a Task that will complete after the specified due time.
    /// </summary>
    /// <param name="dueTime">The delay in milliseconds before the returned task completes.</param><param name="cancellationToken">A CancellationToken that may be used to cancel the task before the due time occurs.</param>
    /// <returns>
    /// The timed Task.
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The <paramref name="dueTime"/> argument must be non-negative or -1 and less than or equal to Int32.MaxValue.
    ///             </exception>
    public static Task Delay(int dueTime, CancellationToken cancellationToken)
    {
        return Task.Delay(dueTime, cancellationToken);
    }

    /// <summary>
    /// Creates a Task that will complete only when all of the provided collection of Tasks has completed.
    /// </summary>
    /// <param name="tasks">The Tasks to monitor for completion.</param>
    /// <returns>
    /// A Task that represents the completion of all of the provided tasks.
    /// </returns>
    /// 
    /// <remarks>
    /// If any of the provided Tasks faults, the returned Task will also fault, and its Exception will contain information
    ///             about all of the faulted tasks.  If no Tasks fault but one or more Tasks is canceled, the returned
    ///             Task will also be canceled.
    /// 
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="tasks"/> argument is null.</exception><exception cref="T:System.ArgumentException">The <paramref name="tasks"/> argument contains a null reference.</exception>
    public static Task WhenAll(params Task[] tasks)
    {
      return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Creates a Task that will complete only when all of the provided collection of Tasks has completed.
    /// </summary>
    /// <param name="tasks">The Tasks to monitor for completion.</param>
    /// <returns>
    /// A Task that represents the completion of all of the provided tasks.
    /// </returns>
    /// 
    /// <remarks>
    /// If any of the provided Tasks faults, the returned Task will also fault, and its Exception will contain information
    ///             about all of the faulted tasks.  If no Tasks fault but one or more Tasks is canceled, the returned
    ///             Task will also be canceled.
    /// 
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="tasks"/> argument is null.</exception><exception cref="T:System.ArgumentException">The <paramref name="tasks"/> argument contains a null reference.</exception>
    public static Task<TResult[]> WhenAll<TResult>(params Task<TResult>[] tasks)
    {
      return Task.WhenAll<TResult>(tasks);
    }

    /// <summary>
    /// Creates a Task that will complete only when all of the provided collection of Tasks has completed.
    /// </summary>
    /// <param name="tasks">The Tasks to monitor for completion.</param>
    /// <returns>
    /// A Task that represents the completion of all of the provided tasks.
    /// </returns>
    /// 
    /// <remarks>
    /// If any of the provided Tasks faults, the returned Task will also fault, and its Exception will contain information
    ///             about all of the faulted tasks.  If no Tasks fault but one or more Tasks is canceled, the returned
    ///             Task will also be canceled.
    /// 
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="tasks"/> argument is null.</exception><exception cref="T:System.ArgumentException">The <paramref name="tasks"/> argument contains a null reference.</exception>
    public static Task WhenAll(IEnumerable<Task> tasks)
    {
      return (Task) Task.WhenAll(tasks);
    }

    /// <summary>
    /// Creates a Task that will complete only when all of the provided collection of Tasks has completed.
    /// </summary>
    /// <param name="tasks">The Tasks to monitor for completion.</param>
    /// <returns>
    /// A Task that represents the completion of all of the provided tasks.
    /// </returns>
    /// 
    /// <remarks>
    /// If any of the provided Tasks faults, the returned Task will also fault, and its Exception will contain information
    ///             about all of the faulted tasks.  If no Tasks fault but one or more Tasks is canceled, the returned
    ///             Task will also be canceled.
    /// 
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="tasks"/> argument is null.</exception><exception cref="T:System.ArgumentException">The <paramref name="tasks"/> argument contains a null reference.</exception>
    public static Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks)
    {
      return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Creates a Task that will complete when any of the tasks in the provided collection completes.
    /// </summary>
    /// <param name="tasks">The Tasks to be monitored.</param>
    /// <returns>
    /// A Task that represents the completion of any of the provided Tasks.  The completed Task is this Task's result.
    /// 
    /// </returns>
    /// 
    /// <remarks>
    /// Any Tasks that fault will need to have their exceptions observed elsewhere.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="tasks"/> argument is null.</exception><exception cref="T:System.ArgumentException">The <paramref name="tasks"/> argument contains a null reference.</exception>
    public static Task<Task> WhenAny(params Task[] tasks)
    {
      return Task.WhenAny(tasks);
    }

    /// <summary>
    /// Creates a Task that will complete when any of the tasks in the provided collection completes.
    /// </summary>
    /// <param name="tasks">The Tasks to be monitored.</param>
    /// <returns>
    /// A Task that represents the completion of any of the provided Tasks.  The completed Task is this Task's result.
    /// 
    /// </returns>
    /// 
    /// <remarks>
    /// Any Tasks that fault will need to have their exceptions observed elsewhere.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="tasks"/> argument is null.</exception><exception cref="T:System.ArgumentException">The <paramref name="tasks"/> argument contains a null reference.</exception>
    public static Task<Task> WhenAny(IEnumerable<Task> tasks)
    {
        return Task.WhenAny(tasks);
    }

    /// <summary>
    /// Creates a Task that will complete when any of the tasks in the provided collection completes.
    /// </summary>
    /// <param name="tasks">The Tasks to be monitored.</param>
    /// <returns>
    /// A Task that represents the completion of any of the provided Tasks.  The completed Task is this Task's result.
    /// 
    /// </returns>
    /// 
    /// <remarks>
    /// Any Tasks that fault will need to have their exceptions observed elsewhere.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="tasks"/> argument is null.</exception><exception cref="T:System.ArgumentException">The <paramref name="tasks"/> argument contains a null reference.</exception>
    public static Task<Task<TResult>> WhenAny<TResult>(params Task<TResult>[] tasks)
    {
      return Task.WhenAny<TResult>(tasks);
    }

    /// <summary>
    /// Creates a Task that will complete when any of the tasks in the provided collection completes.
    /// </summary>
    /// <param name="tasks">The Tasks to be monitored.</param>
    /// <returns>
    /// A Task that represents the completion of any of the provided Tasks.  The completed Task is this Task's result.
    /// 
    /// </returns>
    /// 
    /// <remarks>
    /// Any Tasks that fault will need to have their exceptions observed elsewhere.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="tasks"/> argument is null.</exception><exception cref="T:System.ArgumentException">The <paramref name="tasks"/> argument contains a null reference.</exception>
    public static Task<Task<TResult>> WhenAny<TResult>(IEnumerable<Task<TResult>> tasks)
    {
      return Task.WhenAny<TResult>(tasks);
    }

    /// <summary>
    /// Creates an already completed <see cref="T:System.Threading.Tasks.Task`1"/> from the specified result.
    /// </summary>
    /// <param name="result">The result from which to create the completed task.</param>
    /// <returns>
    /// The completed task.
    /// </returns>
    public static Task<TResult> FromResult<TResult>(TResult result)
    {
      return Task.FromResult<TResult>(result);
    }
 
#endif
    }
}