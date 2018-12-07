using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if (DEBUG || TEST)
[assembly: InternalsVisibleTo("SQLFactory.Tests")] 
#endif
[assembly: ComVisible(false)]
[assembly: CLSCompliantAttribute(true)]
[assembly: AssemblyTitle("SQLFactory")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyCompanyAttribute("AnubisWorks")]
[assembly: AssemblyProductAttribute("SQLFactory")]
[assembly: AssemblyCopyright("(c) 2013-2018 Michael Agata")]
[assembly: AssemblyTrademarkAttribute("All Rights Reserved!")]
[assembly: AssemblyVersion("2.1.12.17")]
[assembly: AssemblyFileVersion("2.1.12.17")]
[assembly: GuidAttribute("16C4BF94-695E-4846-B3EE-0A68FDF92087")]
