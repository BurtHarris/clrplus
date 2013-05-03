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

namespace ClrPlus.Platform {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using Core.Exceptions;
    using Windows.Api;
    using Windows.Api.Structures;

    public static class CurrentProcess {
        public static string CorrectedExeName {
            get {
                var src = Assembly.GetEntryAssembly().Location;
                // if this process was started with a file that doesn't actually end in a '.exe' we have to 
                // make a temporary copy and execute *that* (because we need to to actually be an EXE in order
                // to use shellexecute (which is needed to actually elevate)
                if (!src.EndsWith(".exe", StringComparison.CurrentCultureIgnoreCase)) {
                    var target = (src + ".exe").GenerateTemporaryFilename();
                    File.Copy(src, target);
                    return target;
                }
                return src;
            }
        }

        public static void RestartDetached() {
            Kernel32.FreeConsole();
            Kernel32.AttachConsole(-1);

            if (Marshal.GetLastWin32Error() != 87) {
            Kernel32.FreeConsole();
                var rc = 0;
                var process = new System.Diagnostics.Process {
                    StartInfo = {
                        UseShellExecute = false,
                        WorkingDirectory = Environment.CurrentDirectory,
                        CreateNoWindow = true,
                        FileName = Assembly.GetEntryAssembly().Location,
                        Arguments = EnvironmentUtility.CommandLineArguments, // just the arguments, not the exe itself
                        WindowStyle = ProcessWindowStyle.Hidden,
                    }
                }.Start();
                Environment.Exit(0);
            }
        }

        public static void RestartWithNewExe(string newExePath, string replacementCmdLineArguments = null, bool waitForParentAndChild = false) {
             // if (System.Console.BufferWidth != 0) {
                 // this is a console process.
             // }

            var process = new System.Diagnostics.Process {
                 StartInfo = {
                     UseShellExecute = true,
                     WorkingDirectory = Environment.CurrentDirectory,
                     FileName = newExePath,
                     Verb = "runas",
                     Arguments = replacementCmdLineArguments ?? EnvironmentUtility.CommandLineArguments,
                     ErrorDialog = true,
                     ErrorDialogParentHandle = User32.GetForegroundWindow(),
                     // WindowStyle = ProcessWindowStyle.Maximized, // TODO: uh, what was this here for?
                 }
             };

             if(!process.Start()) {
                 throw new ClrPlusException("Unable to start process for elevation.");
             }

             while((waitForParentAndChild) && ParentIsRunning && !process.WaitForExit(50)) {
             }
        }

        public static void ElevateSelf(bool waitForParentAndChild = false, string replacementCmdLineArguments = null, bool rejoinConsole = false, string replacementExeName = null) {
            try {
                var ntAuth = new SidIdentifierAuthority {
                    Value = new byte[] {
                        0, 0, 0, 0, 0, 5
                    }
                };

                var psid = IntPtr.Zero;
                bool isAdmin;
                if (Advapi32.AllocateAndInitializeSid(ref ntAuth, 2, 0x00000020, 0x00000220, 0, 0, 0, 0, 0, 0, out psid) && Advapi32.CheckTokenMembership(IntPtr.Zero, psid, out isAdmin) && isAdmin) {
                    return; // yes, we're an elevated admin
                }
            } catch {
                // :) Seems that we need to elevate?
            }

            var process = new System.Diagnostics.Process {
                StartInfo = {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = replacementExeName ?? CorrectedExeName,
                    Verb = "runas",
                    Arguments = replacementCmdLineArguments ?? EnvironmentUtility.CommandLineArguments,
                    ErrorDialog = true,
                    ErrorDialogParentHandle = User32.GetForegroundWindow(),
                    // WindowStyle = ProcessWindowStyle.Maximized, // TODO: uh, what was this here for?
                }
            };

            if (!process.Start()) {
                throw new ClrPlusException("Unable to start process for elevation.");
            }

            while ((waitForParentAndChild || rejoinConsole) && ParentIsRunning && !process.WaitForExit(50)) {
            }

            // we should have elevated, or failed to. either way, we're done here..
            Environment.Exit(0);
        }

        private static System.Diagnostics.Process _parentProcess;

        public static System.Diagnostics.Process Parent {
            get {
                return _parentProcess ?? (_parentProcess = ParentProcess.GetParentProcess());
            }
        }

        public static bool ParentIsRunning {
            get {
                try {
                    return !Parent.HasExited;
                } catch {
                }
                return false;
            }
        }
    }
}