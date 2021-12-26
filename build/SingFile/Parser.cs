

// ReSharper disable EmptyNamespace

namespace SingleFileCSharp.SingFile;

internal class Parser : NukeBuild
{
    private const string DEFAULT_XML = "<Project />";
    private readonly string? _githubToken;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    protected readonly Configuration configuration = NukeBuild.IsLocalBuild
                                                         ? Configuration.Debug
                                                         : Configuration.Release;

    [GitRepository] protected readonly GitRepository? gitRepository;
    [GitVersion] protected readonly GitVersion? gitVersion;

    [Solution] protected readonly Solution? solution;

    protected static AbsolutePath ArtifactsDirectory
        => NukeBuild.RootDirectory / "artifacts";

    [Parameter("Github Personal Access Token")]
    protected string? GithubToken
    {
        get
            => _githubToken;
        set
        {
            Log.Information($"GithubToken Length: {value?.Length ?? -1}");
            _githubToken = value;
        }
    }

    internal bool ProcessFile([NotNull] string file)
    {
        const string resultTemplate = "[ProcessFile] {0}";
        string? result = null;

        try
        {
            Log.Information($"[ProcessFile] Processing {file}");

            FileInfo fileInfo = new(file);

            FileStream fileStream = fileInfo.OpenRead();

            using StreamReader reader = new(fileStream);
            string programText = reader.ReadToEnd();

            fileStream.Close();

            if (programText is null or "")
            {
                result = string.Format(resultTemplate , "programText is null or empty.");
                return false;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            bool fileExpanded = false;

            foreach (SyntaxTrivia item in root.GetLeadingTrivia())
            {
                SyntaxKind kind = item.Kind();
                Log.Information($"kind: {kind}");

                bool processed = kind switch
                                 {
                                     SyntaxKind.SingleLineDocumentationCommentTrivia =>
                                         ProcessToken(fileInfo , item , root) ,
                                     _ => false
                                 };

                if (processed)
                {
                    result = string.Format(resultTemplate , $"Processed {file}");
                    fileExpanded = true;
                }
            }

            result = string.Format(resultTemplate , $"Nothing to do in {file}");
            return fileExpanded;
        }
        finally
        {
            Log.Information(result
                         ?? string.Format(resultTemplate , $"No result specified in {file}."));
        }
    }

    private List<SyntaxNode> GetUsingDirectives(FileInfo fileInfo ,
                                                CompilationUnitSyntax root)
    {
        List<SyntaxNode> usingDirectives = new();

        if (root is null)
        {
            return usingDirectives;
        }

        SyntaxToken directive = root.GetFirstToken(includeDirectives: true);

        if (directive.Parent is null)
        {
            directive = directive.GetNextToken(includeDirectives: true);
        }

        while ((directive.Parent?.Kind() ?? SyntaxKind.None) != SyntaxKind.None)
        {
            SyntaxKind? kind = directive.Parent?.Kind();

            Log.Information($"kind: {kind}");

            bool found = kind switch
                         {
                             SyntaxKind.UsingDirective => true ,
                             _ => false
                         };

            if (found)
            {
                SyntaxNode? node = directive.Parent;
                string? code = node?.ToFullString();
                Log.Information($"Using Directive: [{code}]");

                if (node != null)
                {
                    usingDirectives.Add(node);
                }
            }

            directive = directive.GetNextToken(includeDirectives: true);
        }

        return usingDirectives.Distinct()
                              .ToList();
    }

    private bool ProcessToken(FileInfo fileInfo ,
                              SyntaxTrivia trivia ,
                              CompilationUnitSyntax root)
    {
        SyntaxNode? structure = trivia.GetStructure();

        if (structure == null)
        {
            return false;
        }

        root = root.RemoveNode(structure , SyntaxRemoveOptions.KeepNoTrivia) ?? root;

        if (solution is null)
        {
            throw new NullReferenceException("No Solution is assigned.");
        }

        string triviaText = trivia.ToFullString();

        IEnumerable<string> lines = triviaText
                                   .Split('\n' , StringSplitOptions.TrimEntries)
                                   .Select(static l => l.Replace("///" , "" ,
                                                                 StringComparison.Ordinal));

        triviaText = string.Join(Environment.NewLine , lines);

        string? projectXml = null;

        try
        {
            XDocument xml = XDocument.Parse(triviaText);

            if (xml.Root?.Name.LocalName is not "Project")
            {
                return false;
            }

            Log.Information($"[ProcessToken] {fileInfo.Name} has valid Project xml.");

            projectXml = xml.ToString();
        }
        catch
        {
            // Ignore
        }

        if (projectXml is null)
        {
            projectXml = YamlToXml(triviaText);
        }

        string projectDirectoryPath =
            Path.Combine(fileInfo.Directory!.FullName ,
                         fileInfo.Name.Replace(fileInfo.Extension , "" ,
                                               StringComparison.InvariantCultureIgnoreCase));

        DirectoryInfo projectDirectory = new(projectDirectoryPath);

        Log.Information($"[ProcessToken] Expanding Project Directory: {projectDirectory}");

        string projectName = projectDirectory.Name;

        Project oldProject = solution.GetProject(projectName);

        if (oldProject is not null)
        {
            Log.Information($"[ProcessToken] Removing {projectName} from Solution ({solution.FileName})");
            solution.RemoveProject(oldProject);
            solution.Save();
        }

        if (projectDirectory.Exists)
        {
            projectDirectory.Delete(true);
        }

        projectDirectory.Create();

        string csprojFilename =
            Path.Combine(projectDirectory.FullName ,
                         fileInfo.Name.Replace(fileInfo.Extension , ".csproj" ,
                                               StringComparison.InvariantCultureIgnoreCase));

        FileInfo projectFile = new(csprojFilename);

        using StreamWriter writer = new(projectFile.OpenWrite());

        writer.Write(projectXml);

        writer.Close();

        if (!projectFile.Exists)
        {
            return false;
        }

        Log.Information($"[ProcessToken] Created project file: {csprojFilename}");

        List<SyntaxNode>? usingDirectives = GetUsingDirectives(fileInfo , root!);

        if (usingDirectives is
            {
                Count: > 0
            })
        {
            string globalUsingsFileName
                = Path.Combine(projectDirectory.FullName , "GlobalUsings.cs");

            string code = string.Join(Environment.NewLine ,
                                      usingDirectives
                                         .Select(d => $"global {d.ToFullString().Trim()}"));

            File.WriteAllText(globalUsingsFileName , code);

            root = root.RemoveNodes(usingDirectives , SyntaxRemoveOptions.KeepNoTrivia) ?? root;
        }

        string newFileName = Path.Combine(projectFile.DirectoryName! , fileInfo.Name);
        string source = root.ToFullString(); //File.ReadAllText(fileInfo.FullName);

        //source = source.Replace(trivia.ToFullString() , "" , InvariantCultureIgnoreCase);

        File.WriteAllText(newFileName , source);

        FileInfo newFile = new(newFileName);

        if (!newFile.Exists)
        {
            return false;
        }

        Log.Information($"[ProcessToken] Wrote Source to {newFile.FullName}");

        Project project
            = solution
               .AddProject(projectFile.Name.Replace(projectFile.Extension , "" , StringComparison.InvariantCultureIgnoreCase) ,
                           ProjectType.CSharpProject.FirstGuid ,
                           projectFile.FullName ,
                           Guid.NewGuid() ,
                           new Dictionary<string , string>
                           {
                               {
                                   "Debug|Any CPU.ActiveCfg" , "Debug|Any CPU"
                               } ,
                               {
                                   "Debug|Any CPU.Build.0" , "Debug|Any CPU"
                               } ,
                               {
                                   "Release|Any CPU.ActiveCfg" , "Release|Any CPU"
                               } ,
                               {
                                   "Release|Any CPU.Build.0" , "Release|Any CPU"
                               }
                           });

        if (project is null)
        {
            return true;
        }

        solution.Save();

        Microsoft.Build.Evaluation.Project msbuildProject = project.GetMSBuildProject();
        Log.Information($"[ProcessToken] Added new project to Solution: {msbuildProject.FullPath}");
        Log.Information($"[ProcessToken] msbuildProject.AllEvaluatedItems.Count: {msbuildProject.AllEvaluatedItems.Count}");
        Log.Information($"[ProcessToken] msbuildProject.AllEvaluatedProperties.Count: {msbuildProject.AllEvaluatedProperties.Count}");

        ProcessStartInfo info = new()
                                {
                                    FileName = "git" ,
                                    Arguments = "add ." ,
                                    WorkingDirectory = projectDirectoryPath ,
                                    RedirectStandardOutput = true ,
                                    RedirectStandardError = true
                                };

        Process? process = Process.Start(info);

        process?.WaitForExit();

        Log.Information(process?.StandardOutput.ReadToEnd());

        if ((process?.ExitCode ?? -1) == 0)
        {
            return true;
        }

        Console.Error.WriteLine(process?.StandardError.ReadToEnd());

        return false;
    }

    public string YamlToXml(string yaml)
    {
        IDeserializer deserializer = new DeserializerBuilder()
                                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                    .Build();

        object? project = deserializer
           .Deserialize(new StringReader(yaml));

        if (project is null or "")
        {
            return Parser.DEFAULT_XML;
        }

        ISerializer serializer = new SerializerBuilder()
                                .JsonCompatible()
                                .Build();

        string json = serializer.Serialize(project);

        while (json.Contains("\"_" , StringComparison.Ordinal))
        {
            json = json.Replace("\"_" , "\"@" , StringComparison.Ordinal);
        }

        XDocument? xml = JsonConvert.DeserializeXNode(json);

        return xml?.ToString() ?? Parser.DEFAULT_XML;
    }
}
