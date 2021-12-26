// ReSharper disable NotAccessedField.Local
// ReSharper disable AnnotateNotNullTypeMember
// ReSharper disable UnusedMember.Local
// ReSharper disable NullableWarningSuppressionIsUsed
// ReSharper disable TemplateIsNotCompileTimeConstantProblem
// ReSharper disable EmptyNamespace

namespace SingleFileCSharp;

using Serilog;

[CheckBuildProjectConfigurations , ShutdownDotNetAfterServerBuild ,]
internal class Build : Parser
{
    private const RegexOptions REGEX_OPTIONS =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline;

    private Target Clean
        => _ => _
               .Before(Restore)
               .Executes(() => FileSystemTasks.EnsureCleanDirectory(Parser.ArtifactsDirectory));

    private Target Restore
        => _ =>
           {
               Log.Information($"[Restore] PWD: {solution?.Directory}");

               IReadOnlyCollection<Output> Actions()
                   => DotNetTasks.DotNetRestore(s =>
                                                {
                                                    s = s.SetProjectFile(solution)
                                                         .SetProcessWorkingDirectory(solution!
                                                             .Directory);

                                                    if (File.Exists(solution!.Directory
                                                          / "nuget.config"))
                                                    {
                                                        s = s.SetConfigFile(solution.Directory
                                                          / "nuget.config");
                                                    }

                                                    return s;
                                                });

               return _
                  .Executes(Actions);
           };

    private Target Compile
        => _ => _
               .DependsOn(Expand)
               .DependsOn(Restore)
               .Executes(() =>
                             DotNetTasks.DotNetBuild(s =>
                                                     {
                                                         s = s.SetProjectFile(solution)
                                                              .SetConfiguration(configuration)
                                                              .SetProcessWorkingDirectory(solution!
                                                                  .Directory)
                                                              .EnableNoRestore();

                                                         if (gitVersion is not null)
                                                         {
                                                             s = s.SetAssemblyVersion(gitVersion
                                                                   ?.AssemblySemVer)
                                                                .SetFileVersion(gitVersion
                                                                   ?.AssemblySemFileVer)
                                                                .SetInformationalVersion(gitVersion
                                                                   ?.InformationalVersion);
                                                         }

                                                         return s;
                                                     }));

    private Target Run
        => _ => _
               .DependsOn(Compile)
               .Executes(() =>
                         {
                             IReadOnlyCollection<AbsolutePath> files = NukeBuild.RootDirectory
                                .GlobFiles("**/bin/**/*.exe");

                             foreach (AbsolutePath path in files)
                             {
                                 if (path.Contains("build.exe"))
                                 {
                                     continue;
                                 }

                                 ProcessStartInfo info = new()
                                                         {
                                                             FileName = path ,
                                                             WorkingDirectory
                                                                 = Path.GetDirectoryName(path) ,
                                                             RedirectStandardOutput = true ,
                                                             RedirectStandardError = true
                                                         };

                                 Process? process = Process.Start(info);

                                 process?.WaitForExit();

                                 Log.Information(process?.StandardOutput.ReadToEnd());

                                 if (process?.ExitCode != 0)
                                 {
                                     Log.Error(process?.StandardError.ReadToEnd());
                                 }
                             }
                         });

    private Target Push
        => _ => _
               .DependsOn(Expand)
               .Executes(() =>
                         {
                             ProcessStartInfo info = new()
                                                     {
                                                         FileName = "git" ,
                                                         Arguments =
                                                             $"push https://sharpninja:{GithubToken}@github.com/sharpninja/CSharpExploration HEAD:main" ,
                                                         WorkingDirectory
                                                             = NukeBuild.RootDirectory ,
                                                         RedirectStandardOutput = true ,
                                                         RedirectStandardError = true
                                                     };

                             Process? process = Process.Start(info);

                             process?.WaitForExit();

                             Log.Information(process?.StandardOutput.ReadToEnd());

                             if ((process?.ExitCode ?? -1) != 0)
                             {
                                 Console.Error.WriteLine(process?.StandardError.ReadToEnd());
                             }
                         });

    private Target Expand
        => _ =>
           {
               return _
                  .Executes(() =>
                            {
                                IOrderedEnumerable<AbsolutePath> files = NukeBuild.RootDirectory
                                   .GlobFiles("**/*.cs")
                                   .OrderBy(static f => f.ToString());

                                // Pattern can be either regular RegEx
                                // or plain string.  Both are executed
                                // case-insensitive.
                                List<object> skipPatterns = new()
                                                            {
                                                                new Regex(@"\b(build|obj)\b" ,
                                                                    Build.REGEX_OPTIONS)
                                                            };

                                int expandedCount = 0;
                                bool expanded = false;

                                foreach (string file in files)
                                {
                                    string? dirPath = Path.GetDirectoryName(file);

                                    if (dirPath is null or "")
                                    {
                                        Log.Warning($"Could not get directory name for [{file}]");
                                        continue;
                                    }

                                    bool toBreak = false;

                                    foreach (object pattern in skipPatterns)
                                    {
                                        switch (pattern)
                                        {
                                            case Regex r:
                                                if (r.IsMatch(dirPath!))
                                                {
                                                    toBreak = true;
                                                }
                                                else
                                                {
                                                    Log
                                                       .Information($"[Expand] [{r}] does not match [{dirPath}]");
                                                }

                                                break;

                                            case string s:
                                                //Debugger.Launch();
                                                if (dirPath?.Contains(s ,
                                                        StringComparison
                                                           .InvariantCultureIgnoreCase)
                                                 == true)
                                                {
                                                    toBreak = true;
                                                }
                                                else
                                                {
                                                    Log
                                                       .Information($"[Expand] [{s}] does not match [{dirPath}]");
                                                }

                                                break;

                                            default:
                                                throw new
                                                    InvalidCastException($"[Expand] Pattern is wrong type: {pattern.GetType().Name}");
                                        }

                                        if (toBreak)
                                        {
                                            break;
                                        }
                                    }

                                    if (toBreak)
                                    {
                                        continue;
                                    }

                                    bool didExpand = ProcessFile(file);

                                    if (didExpand)
                                    {
                                        expandedCount++;
                                    }

                                    expanded |= didExpand;
                                }

                                if (expanded)
                                {
                                    Log.Information($"[Expand] Expanded {expandedCount} files.");

                                    ProcessStartInfo info = new()
                                                            {
                                                                FileName = "git" ,
                                                                Arguments
                                                                    = $"commit -a -m \"Expanded {expandedCount} files.\"" ,
                                                                WorkingDirectory
                                                                    = NukeBuild.RootDirectory ,
                                                                RedirectStandardOutput = true ,
                                                                RedirectStandardError = true
                                                            };

                                    Process? process = Process.Start(info);

                                    process?.WaitForExit();

                                    Log.Information(process?.StandardOutput.ReadToEnd());

                                    if ((process?.ExitCode ?? -1) != 0)
                                    {
                                        Console.Error.WriteLine(process?.StandardError.ReadToEnd());
                                    }
                                }
                            });
           };

    public static int Main()
        => NukeBuild.Execute<Build>(static x => x.Run);
}
