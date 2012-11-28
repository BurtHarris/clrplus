namespace ClrPlus.Windows.PeBinary.Utility {
    using Core.Exceptions;
    using Core.Extensions;

    public class AssemblyNotFoundException : ClrPlusException {
        public AssemblyNotFoundException(string filename, string version)
            : base("Failed to find assembly '{0}' version: '{1}'".format(filename, version)) {
        }
    }
}