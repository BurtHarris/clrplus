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

namespace ClrPlus.Powershell.Provider.Utility {
    /// <summary>
    ///     The delegate for handling a ProgressStream Report event.
    /// </summary>
    /// <param name="sender"> The object that raised the event, should be a ProgressStream. </param>
    /// <param name="args"> The arguments raised with the event. </param>
    public delegate void ProgressStreamReportDelegate(object sender, ProgressStreamReportEventArgs args);
}