namespace ClrPlus.Windows.PeBinary.Utility {
    using Core.Exceptions;
    using Core.Extensions;

    public class DigitalSignFailure : ClrPlusException {
        public uint Win32Code;

        public DigitalSignFailure(string filename, uint win32Code)
            : base("Failed to digitally sign '{0}' Win32 RC: '{1:x}'".format(filename, win32Code)) {
            Win32Code = win32Code;
        }
    }
}