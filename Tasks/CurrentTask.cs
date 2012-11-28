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

    public static class CurrentTask {
        public class TaskBoundEvents {
            public static TaskBoundEvents Instance = new TaskBoundEvents();

            private TaskBoundEvents() {
            }

            /// <summary>
            ///     Adds an event handler delegate to the current tasktask
            /// </summary>
            /// <param name="taskBoundEvents"> </param>
            /// <param name="eventHandlerDelegate"> </param>
            /// <returns> </returns>
            public static TaskBoundEvents operator +(TaskBoundEvents taskBoundEvents, Delegate eventHandlerDelegate) {
                CoTask.CurrentTask.AddEventHandler(eventHandlerDelegate);
                return Instance;
            }

            public static TaskBoundEvents operator -(TaskBoundEvents taskBoundEvents, Delegate eventHandlerDelegate) {
                CoTask.CurrentTask.RemoveEventHandler(eventHandlerDelegate);
                return Instance;
            }
        }

        public static TaskBoundEvents Events = TaskBoundEvents.Instance;
    }
}