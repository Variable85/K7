using System.Runtime.Versioning;

// The net10.0 TFM is the Linux GTK4 head. The labs packages are annotated
// [SupportedOSPlatform("linux")]. Without this the platform-compat analyzer (CA1416)
// treats every call as cross-platform.
[assembly: SupportedOSPlatform("linux")]
