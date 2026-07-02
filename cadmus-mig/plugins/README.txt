Cadmus CLI Tool Plugins Root Folder
-----------------------------------

The plugins found here are used to get Cadmus factory providers for the CLI tool. A Cadmus factory provider plugin acts as a hub entry point for all the components to be packed in the CLI tool for a specific project.

To add a plugin:

1. create a subfolder of this folder, named after the DLL plugin filename (usually Cadmus.PRJ.Services, where PRJ is your project name). For instance, the plugin Cadmus.Tgr.Services.dll should be placed in a subfolder of this folder named Cadmus.Tgr.Services.
2. copy the plugin files including all its dependencies in this folder.
3. it is also useful to copy the project configuration file (seed-profile.json) in this folder, so you can have it at hand when required.
