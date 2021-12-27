// ReSharper disable NotAccessedField.Local
// ReSharper disable AnnotateNotNullTypeMember
// ReSharper disable UnusedMember.Local
// ReSharper disable NullableWarningSuppressionIsUsed
// ReSharper disable TemplateIsNotCompileTimeConstantProblem
// ReSharper disable EmptyNamespace

namespace SingleFileCSharp;

[CheckBuildProjectConfigurations , ShutdownDotNetAfterServerBuild ,]
internal class Build : Parser
{
    public static int Main()
        => Execute<Build>(static x => x.Run);

    protected override void OnBuildInitialized()
    {
        IReadOnlyCollection<AbsolutePath> sln = RootDirectory
            .GlobFiles("*.sln");

        if (!sln.Any())
        {
            ProcessStartInfo info = new()
            {
                FileName = "dotnet",
                Arguments = "new sln",
                WorkingDirectory = RootDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Process? process = Process.Start(info);

            process?.WaitForExit();

            Serilog.Log.Information(process?.StandardOutput.ReadToEnd());

            if (process?.ExitCode != 0)
            {
                Serilog.Log.Error(process?.StandardError.ReadToEnd());
            }
        }

        base.OnBuildInitialized();
    }

    private Target Clean => _ => _
        .Before(Restore)
        .Executes(() => FileSystemTasks.EnsureCleanDirectory(ArtifactsDirectory));
    

    private Target Restore => _ =>
    {
        Serilog.Log.Information($"[Restore] PWD: {Solution?.Directory}");
        IReadOnlyCollection<Output> Actions() =>
            DotNetTasks.DotNetRestore(s =>
            {
                s = s.SetProjectFile(Solution)
                    .SetProcessWorkingDirectory(Solution!.Directory);
                if (File.Exists(Solution!.Directory / "nuget.config"))
                {
                    s = s.SetConfigFile(Solution.Directory / "nuget.config");
                }

                return s;
            });

        return _
            .Executes(Actions);
    };

    private Target Compile => _ => _
        .DependsOn(Expand)
        .DependsOn(Restore)
        .Executes(() =>
            DotNetTasks.DotNetBuild(s =>
            {
                s = s.SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    .SetProcessWorkingDirectory(Solution!.Directory)
                    .EnableNoRestore();

                if (GitVersion is not null)
                {
                    s = s.SetAssemblyVersion(GitVersion?.AssemblySemVer)
                        .SetFileVersion(GitVersion?.AssemblySemFileVer)
                        .SetInformationalVersion(GitVersion?.InformationalVersion);
                }

                return s;
            }));

    private Target Run => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            IReadOnlyCollection<AbsolutePath> files = RootDirectory
                .GlobFiles("**/bin/**/*.exe");

            foreach (var path in files)
            {
                if (!path.Contains("build.exe"))
                {
                    ProcessStartInfo info = new()
                    {
                        FileName = path,
                        WorkingDirectory = Path.GetDirectoryName(path) ,
                        RedirectStandardOutput = true ,
                        RedirectStandardError = true ,
                    };

                    Process? process = Process.Start(info);

                    process?.WaitForExit();

                    Serilog.Log.Information(process?.StandardOutput.ReadToEnd());
                    
                    if(process?.ExitCode != 0)
                    {
                        Serilog.Log.Error(process?.StandardError.ReadToEnd());
                    }
                }
            }
        });
    
    private Target Push => _ => _
        .DependsOn(Expand)
        .Executes(() =>
            {
                ProcessStartInfo info = new()
                {
                    FileName = "git" ,
                    Arguments =
                        $"push https://sharpninja:{GithubToken}@github.com/sharpninja/CSharpExploration HEAD:main" ,
                    WorkingDirectory = RootDirectory ,
                    RedirectStandardOutput = true ,
                    RedirectStandardError = true ,
                };

                Process? process = Process.Start(info);

                process?.WaitForExit();

                Serilog.Log.Information(process?.StandardOutput.ReadToEnd());

                if ((process?.ExitCode ?? -1) != 0)
                {
                    Console.Error.WriteLine(process?.StandardError.ReadToEnd());
                }
            }
        );

    private Target Expand => _ => _
        .Executes(() =>
            {
                const RegexOptions REGEX_OPTIONS =
                    RegexOptions.IgnoreCase |
                    RegexOptions.Compiled |
                    RegexOptions.Singleline;

                IOrderedEnumerable<AbsolutePath> files = RootDirectory
                    .GlobFiles("**/*.cs")
                    .OrderBy(static f => f.ToString());

                // Pattern can be either regular RegEx
                // or plain string.  Both are executed
                // case-insensitive.
                List<object> skipPatterns = new()
                {
                    new Regex(@"\b(build|obj)\b" , REGEX_OPTIONS) ,
                };

                int expandedCount = 0;
                bool expanded = false;

                foreach (string file in files)
                {
                    string? dirPath = Path.GetDirectoryName(file);

                    if (dirPath is null or "")
                    {
                        Serilog.Log.Warning($"Could not get directory name for [{file}]");
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
                                    Serilog.Log.Information($"[Expand] [{r}] does not match [{dirPath}]");
                                }

                                break;

                            case string s:
                                //Debugger.Launch();
                                if (dirPath?.Contains(s ,
                                    InvariantCultureIgnoreCase
                                ) == true)
                                {
                                    toBreak = true;
                                }
                                else
                                {
                                    Serilog.Log.Information($"[Expand] [{s}] does not match [{dirPath}]");
                                }

                                break;

                            default:
                                throw new InvalidCastException(
                                    $"[Expand] Pattern is wrong type: {pattern.GetType().Name}"
                                );
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
                    Serilog.Log.Information($"[Expand] Expanded {expandedCount} files.");

                    ProcessStartInfo info = new()
                    {
                        FileName = "git" ,
                        Arguments = $"commit -a -m \"Expanded {expandedCount} files.\"" ,
                        WorkingDirectory = RootDirectory ,
                        RedirectStandardOutput = true ,
                        RedirectStandardError = true ,
                    };

                    Process? process = Process.Start(info);

                    process?.WaitForExit();

                    Serilog.Log.Information(process?.StandardOutput.ReadToEnd());

                    if ((process?.ExitCode ?? -1) != 0)
                    {
                        Console.Error.WriteLine(process?.StandardError.ReadToEnd());
                    }
                }
            }
        );
}
