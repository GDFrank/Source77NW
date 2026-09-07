# Source/ folder conventions and contents

Folders contain either:

- App projects - app specific source code, and project code
- DLL projects - project only code
- Source code - source code (*.cs) only

The namespace is Source77NW for all *.cs contained
in the Source code folders. There are no inner namespaces.

Source code folders do not have subfolders.

## App Projects

Projects are named as App.<assemblyName> and contain
project specific source as well as the csproj and any 
other project management files. Note that the target
assembly name is not "App.name.exe" ... but "name.exe".

Currently there are no App.* projects.

## DLL Projects

Projects and the assemblies are named Source77NW.<name> 
where name indicates what Core or UI modules are contained
in the assembly and what environment it is targeted to.

Source77NW.Core.Base contains Core.Base only.

Source77NW.Core.NT contains Core.Base and Core.NT.

Source77NW.NT.Forms contains Core.Base, Core.NT,
UI.Base, UI.NT.Base, UI.NT.Forms

## Source Code Classification

Core.* folders contain code with no UI dependency at all. 

UI.* folders contain UI platform dependent source code.

Core.Base/ - modules used widely by other modules or apps.

Core.NT/ - Core modules for Windows NT

UI.Base/ - Generic UI modules used by all other UI modules.

UI.NT.Base/ - NT UI core modules used by NT UI platforms.

UI.NT.Forms/ - NT Forms modules

Future folders might contain addons such as
- NT.WPF/ - WPF modules
- Core.NT.DEV/ - modules targeted to development on NT
- who knows what

Copyright (c) GDFrank - 77NW.net. All rights reserved.

