﻿global using System.ComponentModel;
global using System.Diagnostics;
global using System.Reflection;
global using System.Text.RegularExpressions;
global using System.Xml.Linq;

global using JetBrains.Annotations;

global using Microsoft.CodeAnalysis;
global using Microsoft.CodeAnalysis.CSharp;
global using Microsoft.CodeAnalysis.CSharp.Syntax;

global using Nuke.Common;
global using Nuke.Common.CI;
global using Nuke.Common.Execution;
global using Nuke.Common.Git;
global using Nuke.Common.IO;
global using Nuke.Common.ProjectModel;
global using Nuke.Common.Tooling;
global using Nuke.Common.Tools.DotNet;
global using Nuke.Common.Tools.GitVersion;

global using static Nuke.Common.NukeBuild;

