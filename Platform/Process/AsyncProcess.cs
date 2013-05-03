
namespace ClrPlus.Platform.Process {
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System;
    using System.Diagnostics;
    using System.Security;
    using System.Text;
    using System.Threading;
    using ClrPlus.Windows.Api;
    using Core.Collections;
    using System.Linq;
    using Core.Extensions;

    public class ProcessStartInfo {
        internal readonly System.Diagnostics.ProcessStartInfo _processStartInfo = new System.Diagnostics.ProcessStartInfo();
        private XDictionary<string, string> _environmentVariables;

        internal ProcessStartInfo(System.Diagnostics.ProcessStartInfo psi) {
            _processStartInfo = psi;
            _environmentVariables = new XDictionary<string, string>();
            foreach (var i in psi.EnvironmentVariables.Keys) {
                _environmentVariables.Add(i.ToString(), psi.EnvironmentVariables[(string)i]);
            }
            _processStartInfo.RedirectStandardError = true;
            _processStartInfo.RedirectStandardOutput = true;
            SyncEnvironment();
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Diagnostics.ProcessStartInfo"/> class without specifying a file name with which to start the process.
        /// 
        /// </summary>
        public ProcessStartInfo() {
            _processStartInfo.UseShellExecute = false;
            _processStartInfo.RedirectStandardError = true;
            _processStartInfo.RedirectStandardOutput = true;
            SyncEnvironment();
        }

        private void SyncEnvironment() {
            _processStartInfo.EnvironmentVariables.Clear();
            foreach(var key in EnvironmentVariables.Keys) {
                _processStartInfo.EnvironmentVariables.Add(key, EnvironmentVariables[key]);
            }
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Diagnostics.ProcessStartInfo"/> class and specifies a file name such as an application or document with which to start the process.
        /// 
        /// </summary>
        /// <param name="fileName">An application or document with which to start a process.
        ///                 </param>
        public ProcessStartInfo(string fileName) : this() {
            FileName = fileName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Diagnostics.ProcessStartInfo"/> class, specifies an application file name with which to start the process, and specifies a set of command-line arguments to pass to the application.
        /// 
        /// </summary>
        /// <param name="fileName">An application with which to start a process.
        ///                 </param><param name="arguments">Command-line arguments to pass to the application when the process starts.
        ///                 </param>
        public ProcessStartInfo(string fileName, string arguments)
            : this() {
            FileName = fileName;
            Arguments = arguments;
        }

        public string Arguments {
            get {
                return _processStartInfo.Arguments;
            }
            set {
                _processStartInfo.Arguments = value;
            }
        }
        public string FileName {
            get {
                return _processStartInfo.FileName;
            }
            set {
                _processStartInfo.FileName = value;
            }
        }
        public string WorkingDirectory {
            get {
                return _processStartInfo.WorkingDirectory;
            }
            set {
                _processStartInfo.WorkingDirectory = value;
            }
        }

        public bool CreateNoWindow {
            get {
                return _processStartInfo.CreateNoWindow;
            }
            set {
                _processStartInfo.CreateNoWindow = value;
            }
        }

        public ProcessWindowStyle WindowStyle {
            get {
                return _processStartInfo.WindowStyle;
            }
            set {
                _processStartInfo.WindowStyle = value;
            }
        }
        public bool ErrorDialog {
            get {
                return _processStartInfo.ErrorDialog;
            }
            set {
                _processStartInfo.ErrorDialog = value;
            }
        }
        public IntPtr ErrorDialogParentHandle {
            get {
                return _processStartInfo.ErrorDialogParentHandle;
            }
            set {
                _processStartInfo.ErrorDialogParentHandle = value;
            }
        }
        public bool RedirectStandardInput {
            get {
                return _processStartInfo.RedirectStandardInput;
            }
            set {
                _processStartInfo.RedirectStandardInput = value;
            }
        }
        public string UserName {
            get {
                return _processStartInfo.UserName;
            }
            set {
                _processStartInfo.UserName = value;
            }
        }
        public SecureString Password{
            get {
                return _processStartInfo.Password;
            }
            set {
                _processStartInfo.Password = value;
            }
        }
        
        public IDictionary<string,string> EnvironmentVariables {
            get {
                if (_environmentVariables == null) {
                    _environmentVariables = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToXDictionary(dictionaryEntry => (string)dictionaryEntry.Key, dictionaryEntry => (string)dictionaryEntry.Value);
                    _environmentVariables.Changed += source => SyncEnvironment();
                }
                return _environmentVariables;
            } set {
                _environmentVariables = value != null ? value.Keys.ToXDictionary(each => each, each => value[each]) : new XDictionary<string, string>();
                _environmentVariables.Changed += source => SyncEnvironment();
            }
        }
    }

    public class AsyncProcess {
        protected Process _process;
        private AsynchronouslyEnumerableList<string> _stdError = new AsynchronouslyEnumerableList<string>();
        private AsynchronouslyEnumerableList<string> _stdOut = new AsynchronouslyEnumerableList<string>();

        protected AsyncProcess(Process process) {
            _process = process;
        }

        public static AsyncProcess Start(ProcessStartInfo startInfo) {
            var process = new Process {
                StartInfo = startInfo._processStartInfo
            };

            var result =new  AsyncProcess(process);
            
            process.EnableRaisingEvents = true;

            // set up std* access
            process.ErrorDataReceived += (sender, args) => result._stdError.Add(args.Data);
            process.OutputDataReceived += (sender, args) => result._stdOut.Add(args.Data);
            process.Exited +=(sender, args) => {
                
                result._stdError.Completed(); result._stdOut.Completed();
            };

            process.Start();

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            return result;
        }
            
        public IEnumerable<string> StandardError {
            get {
                return _stdError;
            }
        }

        public IEnumerable<string> StandardOutput {
            get {
                return _stdOut;     
            }
        }
        public static AsyncProcess Start(System.Diagnostics.ProcessStartInfo startInfo) {
            return Start(new ProcessStartInfo(startInfo));
        }

        public static AsyncProcess Start(string fileName) {
            return Start(new ProcessStartInfo {
                FileName = fileName
            });
        }

        public static AsyncProcess Start(string fileName, string parameters) {
            return Start(new ProcessStartInfo {
                FileName = fileName,
                Arguments = parameters
            });
        }
        

        public void Kill() {
            _process.Kill();
        }

        public bool WaitForExit(int milliseconds) {
            return _process.WaitForExit(milliseconds);
        }

        public void WaitForExit() {
            WaitForExit(-1);
        }

        public bool WaitForInputIdle(int milliseconds) {
            return _process.WaitForExit(milliseconds);
        }

        public bool WaitForInputIdle() {
            return WaitForInputIdle(-1);
        }

        public int ExitCode {
            get {
                return _process.ExitCode;
            }
        }

        public bool HasExited {
            get {
                return _process.HasExited;
            }
        }

        public DateTime ExitTime {
            get {
                return _process.ExitTime;
            }
        }

        public IntPtr Handle {
            get {
                return _process.Handle;
            }
        }

        public int HandleCount {
            get {
                return _process.HandleCount;
            }
        }

        public int Id {
            get {
                return _process.Id;
            }
        }

        public string MachineName {
            get {
                return _process.MachineName;
            }
        }

        public IntPtr MainWindowHandle {
            get {
                return _process.MainWindowHandle;
            }
        }

        public string MainWindowTitle {
            get {
                return _process.MainWindowTitle;
            }
        }

        public ProcessModule MainModule {
            get {
                return _process.MainModule;
            }
        }

        public IntPtr MaxWorkingSet {
            get {
                return _process.MaxWorkingSet;
            }
        }

        public IntPtr MinWorkingSet {
            get {
                return _process.MinWorkingSet;
            }
        }

        public ProcessModuleCollection Modules {
            get {
                return _process.Modules;
            }
        }

        public long NonpagedSystemMemorySize64 {
            get {
                return _process.NonpagedSystemMemorySize64;
            }
        }

        public long PagedMemorySize64 {
            get {
                return _process.PagedMemorySize64;
            }
        }

        public long PagedSystemMemorySize64 {
            get {
                return _process.PagedSystemMemorySize64;
            }
        }

        public long PeakPagedMemorySize64 {
            get {
                return _process.PeakPagedMemorySize64;
            }
        }

        public long PeakWorkingSet64 {
            get {
                return _process.PeakWorkingSet64;
            }
        }

        public long PeakVirtualMemorySize64 {
            get {
                return _process.PeakVirtualMemorySize64;
            }
        }

        public bool PriorityBoostEnabled {
            get {
                return _process.PriorityBoostEnabled;
            }
            set {
                _process.PriorityBoostEnabled = value;
            }
        }

        public ProcessPriorityClass PriorityClass{
            get {
                return _process.PriorityClass;
            }
            set {
                _process.PriorityClass = value;
            }
        }

        public long PrivateMemorySize64 {
            get {
                return _process.PrivateMemorySize64;
            }
        }

        public TimeSpan PrivilegedProcessorTime {
            get {
                return _process.PrivilegedProcessorTime;
            }
        }

        public string ProcessName {
            get {
                return _process.ProcessName;
            }
        }

        public IntPtr ProcessorAffinity {
            get {
                return _process.ProcessorAffinity;
            }
            set {
                _process.ProcessorAffinity = value;
            }
        }

        public bool Responding {
            get {
                return _process.Responding;
            }
        }

        public int SessionId {
            get {
                return _process.SessionId;
            }
        }

        
        public DateTime StartTime {
            get {
                return _process.StartTime;
            }
        }

        public ISynchronizeInvoke SynchronizingObject {
            get {
                return _process.SynchronizingObject;
            }
        }

        public ProcessThreadCollection Threads {
            get {
                return _process.Threads;
            }
        }

        public TimeSpan TotalProcessorTime {
            get {
                return _process.TotalProcessorTime;
            }
        }

        public TimeSpan UserProcessorTime {
            get {
                return _process.UserProcessorTime;
            }
        }

        public long VirtualMemorySize64 {
            get {
                return _process.VirtualMemorySize64;
            }
        }

        public bool EnableRaisingEvents {
            get {
                return _process.EnableRaisingEvents;
            }
            set {
                _process.EnableRaisingEvents = value;
            }
        }

        public long WorkingSet64 {
            get {
                return _process.WorkingSet64;
            }
        }

        public event EventHandler Exited {
            add {
                _process.Exited += value;
            }
            remove {
                _process.Exited -= value;
            }
        }

        public bool CloseMainWindow() {
            return _process.CloseMainWindow();
        }

        protected void Dispose(bool disposing) {
            if (_process != null) {
                _process.Dispose();
            }
            _process = null;
        }

        public void Close() {
            _process.Close();

        }

        public static void EnterDebugMode() {
            Process.EnterDebugMode();
        }

        public static void LeaveDebugMode() {
            Process.LeaveDebugMode();
        }

        public static AsyncProcess GetProcessById(int processId, string machineName) {
            return new AsyncProcess(Process.GetProcessById(processId, machineName));
        }

        public static AsyncProcess GetProcessById(int processId) {
            return new AsyncProcess(Process.GetProcessById(processId));
        }

        public static AsyncProcess[] GetProcessesByName(string processName) {
            return Process.GetProcessesByName(processName).Select(each => new AsyncProcess(each)).ToArray();
        }

        public static AsyncProcess[] GetProcessesByName(string processName, string machineName) {
            return Process.GetProcessesByName(processName, machineName).Select(each => new AsyncProcess(each)).ToArray();
        }

        public static AsyncProcess[] GetProcesses() {
            return Process.GetProcesses().Select(each => new AsyncProcess(each)).ToArray();
        }

        public static AsyncProcess[] GetProcesses(string machineName) {
            return Process.GetProcesses(machineName).Select(each => new AsyncProcess(each)).ToArray();
        }

        public static AsyncProcess GetCurrentProcess() {
            return new AsyncProcess(Process.GetCurrentProcess());
        }

        public void Refresh() {
            _process.Refresh();
        }

    }
}
