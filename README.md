# DDSGenNF
## Description
Generates DDS files for ACC custom skins. Requires Compressonator CLI to run. Requires the following NuGet packages to build
- https://www.nuget.org/packages/Extended.Wpf.Toolkit/
- https://www.nuget.org/packages/Microsoft-Windows10-APICodePack-Core
- https://www.nuget.org/packages/Microsoft-Windows10-APICodePack-Shell
- https://www.nuget.org/packages/Microsoft-Windows10-APICodePack-ShellExtensions
Compressonator CLI:
https://github.com/GPUOpen-Tools/Compressonator
## TODO
### Functionality
1. Add ability to browse for Compressonator CLI
2. Add option to overwrite existing DDS files
3. Add verbosity levels
4. Add ability to handle non-standard PNG files
### Performance
1. Better memory management
2. Limit amound of spawned subprocesses