using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("HivePlugin")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Exit Games GmbH")]
[assembly: AssemblyProduct("Exit Games Hive Plugin")]
[assembly: AssemblyCopyright("(c) Exit Games GmbH, http://www.exitgames.com")]
[assembly: AssemblyTrademark("Exit Games Photon")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("c8552b7c-b3f0-49ac-8a40-5ba811ed6fce")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.14.14962")]

#if PLUGINS_0_9
    [assembly: AssemblyVersion("0.9.14.14962")]
    [assembly: AssemblyFileVersion("0.9.14.14962")]
#else
    [assembly: AssemblyVersion("1.0.16.14962")]
    [assembly: AssemblyFileVersion("1.0.16.14962")]
#endif

[assembly: AssemblyInformationalVersion("4.1.37.14962")]
