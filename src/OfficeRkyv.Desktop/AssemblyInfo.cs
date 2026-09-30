using System.Runtime.Versioning;

// The GTK4 backend exists only for Linux. Declared once for the whole assembly, so the platform-compatibility
// analyzer (an error in Release) does not flag every call into it.
[assembly: SupportedOSPlatform("linux")]
