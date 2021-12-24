// ReSharper disable NotAccessedField.Local
// ReSharper disable AnnotateNotNullTypeMember
// ReSharper disable UnusedMember.Local
// ReSharper disable NullableWarningSuppressionIsUsed

namespace SingleFileCSharp;

[CheckBuildProjectConfigurations, ShutdownDotNetAfterServerBuild,]
internal class Build : NukeBuild
{
    private AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    private Target Clean => _ => _
        .Before(Restore)
        .Executes(() => FileSystemTasks.EnsureCleanDirectory(ArtifactsDirectory));

    private Target Restore => _ => _
        .Executes(() =>
            DotNetTasks.DotNetRestore(s =>
                s.SetProjectFile(Solution)
            )
        );

    private Target Compile => _ => _
        .DependsOn(Expand)
        .DependsOn(Restore)
        .Executes(() =>
            DotNetTasks.DotNetBuild(s =>
            s.SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore()
        ));

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
                    new Regex(@"\b(build|obj)\b", REGEX_OPTIONS),
                };

                int expandedCount=0;
                bool expanded = false;

                foreach (string file in files)
                {
                    string dirPath = Path.GetDirectoryName(file);

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
                                    Console.WriteLine($"[Expand] [{r}] does not match [{dirPath}]");
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
                                    Console.WriteLine($"[Expand] [{s}] does not match [{dirPath}]");
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
                    Console.WriteLine($"[Expand] Expanded {expandedCount} files.");

                    GitRepository.Apply(g => g);
                }
            }
        );

    public static int Main()
        => Execute<Build>(static x => x.Compile);

    [ Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)") ]
    private readonly Configuration Configuration = IsLocalBuild
        ? Configuration.Debug
        : Configuration.Release;

    [ GitRepository ] private readonly GitRepository GitRepository;
    [ GitVersion ] private readonly GitVersion GitVersion;

    private bool ProcessFile([NotNull] string file)
    {
        const string RESULT_TEMPLATE = "[ProcessFile] {0}";
        string result = null;

        try
        {
            Console.WriteLine($"[ProcessFile] Processing {file}");

            FileInfo fileInfo = new(file);

            FileStream fileStream = fileInfo.OpenRead();

            using StreamReader reader = new(fileStream);
            string programText = reader.ReadToEnd();

            fileStream.Close();

            if (programText is null or "")
            {
                result = string.Format(RESULT_TEMPLATE , "programText is null or empty.");
                return false;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            foreach (SyntaxTrivia item in root.GetLeadingTrivia())
            {
                bool processed = item.Kind() switch
                {
                    SyntaxKind.SingleLineDocumentationCommentTrivia =>
                        ProcessToken(fileInfo , item) ,
                    _ => false ,
                };

                if (processed)
                {
                    result = string.Format(RESULT_TEMPLATE , $"Processed {file}");
                    return true;
                }

                break;
            }

            result = string.Format(RESULT_TEMPLATE , $"Nothing to do in {file}");
            return false;
        }
        finally
        {
            Console.WriteLine(result ?? string.Format(RESULT_TEMPLATE, $"No result specified in {file}."));
        }
    }

    private bool ProcessToken(FileInfo fileInfo , SyntaxTrivia trivia)
    {
        string triviaText = trivia.ToFullString();
        IEnumerable<string> lines = triviaText
            .Split('\n', StringSplitOptions.TrimEntries)
            .Select(static l => l.Replace("///", "", Ordinal)
                .Trim()
            );

        triviaText = string.Join(Environment.NewLine , lines);

        XDocument xml = XDocument.Parse(triviaText);

        if (xml.Root?.Name.LocalName is not "Project")
        {
            return false;
        }

        Console.WriteLine($"[ProcessToken] {fileInfo.Name} has valid Project xml.");

        string projectDirectoryPath =
            Path.Combine(
                fileInfo.Directory!.FullName,
                fileInfo.Name.Replace(fileInfo.Extension, "", InvariantCultureIgnoreCase)
            );

        DirectoryInfo projectDirectory = new(projectDirectoryPath);

        Console.WriteLine($"[ProcessToken] Expanding Project Directory: {projectDirectory}");

        string projectName = projectDirectory.Name;

        Project oldProject = Solution.GetProject(projectName);

        if (oldProject is not null)
        {
            Console.WriteLine(
                $"[ProcessToken] Removing {projectName} from Solution ({Solution.FileName})"
            );
            Solution.RemoveProject(oldProject);
            Solution.Save();
        }

        if (projectDirectory.Exists)
        {
            projectDirectory.Delete(true);
        }

        projectDirectory.Create();

        string csprojFilename =
            Path.Combine(
                projectDirectory.FullName,
                fileInfo.Name.Replace(fileInfo.Extension, ".csproj", InvariantCultureIgnoreCase)
            );

        xml.Save(csprojFilename);

        FileInfo projectFile = new(csprojFilename);

        if (!projectFile.Exists)
        {
            return false;
        }

        Console.WriteLine($"[ProcessToken] Created project file: {csprojFilename}");

        string newFileName = Path.Combine(projectFile.DirectoryName!, fileInfo.Name);
        string source = File.ReadAllText(fileInfo.FullName);

        source = source.Replace(trivia.ToFullString() , "" , InvariantCultureIgnoreCase);

        File.WriteAllText(newFileName , source);

        FileInfo newFile = new(newFileName);

        if (!newFile.Exists)
        {
            return false;
        }

        Console.WriteLine($"[ProcessToken] Wrote Source to {newFile.FullName}");

        Project project = Solution.AddProject(
            projectFile.Name.Replace(projectFile.Extension, "" , InvariantCultureIgnoreCase),
            ProjectType.CSharpProject.FirstGuid,
            projectFile.FullName,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                {
                    "Debug|Any CPU.ActiveCfg", "Debug|Any CPU"
                },
                {
                    "Debug|Any CPU.Build.0", "Debug|Any CPU"
                },
                {
                    "Release|Any CPU.ActiveCfg", "Release|Any CPU"
                },
                {
                    "Release|Any CPU.Build.0", "Release|Any CPU"
                },
            }
        );

        if (project is null)
        {
            return true;
        }

        Solution.Save();
        Microsoft.Build.Evaluation.Project msbuildProject = project.GetMSBuildProject();
        Console.WriteLine($"[ProcessToken] Added new project to Solution: {msbuildProject.FullPath}"
        );
        Console.WriteLine(
            $"[ProcessToken] msbuildProject.AllEvaluatedItems.Count: {msbuildProject.AllEvaluatedItems.Count}"
        );
        Console.WriteLine(
            $"[ProcessToken] msbuildProject.AllEvaluatedProperties.Count: {msbuildProject.AllEvaluatedProperties.Count}"
        );

        return true;
    }

    [ Solution ] private readonly Solution Solution;
}
