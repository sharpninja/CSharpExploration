using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
internal class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() => EnsureCleanDirectory(ArtifactsDirectory));

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target Expand => _ => _
        .Executes(() =>
        {
            var files = GlobFiles(RootDirectory, "**/*.cs");
            var skipDirectories = new List<string>();
            foreach (var file in files)
            {
                var dirPath = Path.GetDirectoryName(file);

                if (!skipDirectories.Contains(dirPath))
                {
                    var dirInfo = new DirectoryInfo(dirPath);
                    var csproj = dirInfo.GetFiles("*.csproj");

                    if (csproj.Any())
                    {
                        skipDirectories.Add(dirPath);
                        continue;
                    }

                    var fileInfo = new FileInfo(file);

                    var fileStream = fileInfo.OpenRead();

                    var programText = new StreamReader(fileStream).ReadToEnd();

                    fileStream.Close();

                    if (programText is null or "")
                    {
                        continue;
                    }

                    SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);

                    CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                    var trivia = root.GetLeadingTrivia();

                    foreach (var item in trivia)
                    {
                        var processed = item.Kind() switch
                        {
                            SyntaxKind.XmlComment => ProcessToken(item),
                            _ => false
                        };

                        if (processed) break;
                    }
                }
                else
                {
                    continue;
                }
            }
        });

    private bool ProcessToken(SyntaxTrivia trivia)
    {
        bool result = false;

        var triviaText = trivia.ToFullString();

        Console.WriteLine(triviaText);

        return result;
    }
}