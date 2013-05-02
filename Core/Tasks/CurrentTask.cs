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
    using System.Threading.Tasks;
    using Extensions;

    public delegate bool Warning(string messageCode, string message, params object[] args);
    public delegate bool Message(string messageCode, string message, params object[] args);
    public delegate bool Error(string messageCode, string message, params object[] args);
    public delegate bool Debug(string messageCode, string message, params object[] args);
    public delegate bool Verbose(string messageCode, string message, params object[] args);
    public delegate bool Progress(string messageCode, int progress, string message, params object[] args);

    public class EventSource {
        public static EventSource Instance = new EventSource();
        protected internal EventSource() {
        }

        /// <summary>
        ///     Adds an event handler delegate to the current tasktask
        /// </summary>
        /// <param name="eventSource"> </param>
        /// <param name="eventHandlerDelegate"> </param>
        /// <returns> </returns>
        public static EventSource operator +(EventSource eventSource, Delegate eventHandlerDelegate) {
            XTask.CurrentExecutingTask.AddEventHandler(eventHandlerDelegate);
            return eventSource;
        }

        public static EventSource operator -(EventSource eventSource, Delegate eventHandlerDelegate) {
            XTask.CurrentExecutingTask.RemoveEventHandler(eventHandlerDelegate);
            return eventSource;
        }
    }

    public class LocalEventSource : EventSource, IDisposable {
        protected internal List<Delegate> Delegates = new List<Delegate>();
        protected internal LocalEventSource() {
        }
        public static LocalEventSource operator +(LocalEventSource eventSource, Delegate eventHandlerDelegate) {
            XTask.CurrentExecutingTask.AddEventHandler(eventHandlerDelegate);
            eventSource.Delegates.Add(eventHandlerDelegate);
            return eventSource;
        }

        public static LocalEventSource operator -(LocalEventSource eventSource, Delegate eventHandlerDelegate) {
            XTask.CurrentExecutingTask.RemoveEventHandler(eventHandlerDelegate);
            eventSource.Delegates.Remove(eventHandlerDelegate);
            return eventSource;
        }

        public void Dispose() {
            if (Delegates != null) {
                foreach (var i in Delegates) {
                    XTask.CurrentExecutingTask.RemoveEventHandler(i);
                }
                Delegates = null;
                GC.SuppressFinalize(this);

                // encourage a bit of cleanup
                Task.Factory.StartNew(XTask.Collect);
            }
        }

        ~LocalEventSource() {
            if(!Delegates.IsNullOrEmpty()) {
                Dispose();
            }
        }

        public LocalEventSource Events {
            get {
                return this;
            }
            set {
                return;
            }
        }
    }

    public static class CurrentTask {
        public static LocalEventSource Local {
            get {
                return new LocalEventSource();
            }
        }

        public static EventSource Events = EventSource.Instance;
    }
}