using Newtonsoft.Json;

using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// ReSharper disable EmptyNamespace

namespace SingleFileCSharp.SingFile;

using System.Reflection;

using Project = Nuke.Common.ProjectModel.Project;
using Solution = Nuke.Common.ProjectModel.Solution;

internal class Parser: NukeBuild
{
    private const string DEFAULT_XML = "<Project />";

    internal bool ProcessFile(string file)
    {
        const string RESULT_TEMPLATE = "[ProcessFile] {0}";
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
                result = string.Format(RESULT_TEMPLATE , "programText is null or empty.");
                return false;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            bool fileExpanded = false;
            foreach (SyntaxTrivia item in root.GetLeadingTrivia())
            {
                var kind = item.Kind();
                Log.Information($"kind: {kind}");
                bool processed = kind switch
                {
                    SyntaxKind.SingleLineDocumentationCommentTrivia =>
                        ProcessToken(fileInfo , item, root) ,
                    _ => false ,
                };

                if (processed)
                {
                    result = string.Format(RESULT_TEMPLATE , $"Processed {file}");
                    fileExpanded = true;
                }
            }

            result = string.Format(RESULT_TEMPLATE , $"Nothing to do in {file}");
            return fileExpanded;
        }
        finally
        {
            Log.Information(result ?? string.Format(RESULT_TEMPLATE , $"No result specified in {file}."));
        }
    }

    private List<SyntaxNode> GetUsingDirectives(FileInfo fileInfo , CompilationUnitSyntax root)
    {
        List<SyntaxNode> usingDirectives = new();
        if (root is null)
        {
            return usingDirectives;
        }

        var directive = root.GetFirstToken(includeDirectives: true);

        if (directive.Parent is null)
        {
            directive = directive.GetNextToken(includeDirectives: true);
        }

        while ((directive.Parent?.Kind() ?? SyntaxKind.None) != SyntaxKind.None)
        {
            var kind = directive.Parent?.Kind();

            Serilog.Log.Information($"kind: {kind}");

            bool found = kind switch
            {
                SyntaxKind.UsingDirective => true ,
                _ => false
            };

            if (found)
            {
                var node = directive.Parent;
                string? code = node?.ToFullString();
                Log.Information($"Using Directive: [{code}]");
                if (node != null)
                {
                    usingDirectives.Add(node);
                }
            }

            directive = directive.GetNextToken(includeDirectives: true);
        }

        return usingDirectives.Distinct().ToList();
    }

    private bool ProcessToken(
        FileInfo fileInfo ,
        SyntaxTrivia trivia,
        CompilationUnitSyntax root)
    {
        SyntaxNode? structure = trivia.GetStructure();
        if (structure == null)
        {
            return false;
        }

        root = root.RemoveNode(structure , SyntaxRemoveOptions.KeepNoTrivia) ?? root;

        if (Solution is null)
        {
            throw new NullReferenceException("No Solution is assigned.");
        }

        string triviaText = trivia.ToFullString();
        IEnumerable<string> lines = triviaText
            .Split('\n' , StringSplitOptions.TrimEntries)
            .Select(static l => l.Replace("///" , "" , Ordinal));

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

        projectXml ??= YamlToXml(triviaText);

        string projectDirectoryPath =
            Path.Combine(
                fileInfo.Directory!.FullName ,
                fileInfo.Name.Replace(fileInfo.Extension , "" , InvariantCultureIgnoreCase)
            );

        DirectoryInfo projectDirectory = new(projectDirectoryPath);

        Log.Information($"[ProcessToken] Expanding Project Directory: {projectDirectory}");

        string projectName = projectDirectory.Name;

        Project oldProject = Solution.GetProject(projectName);

        if (oldProject is not null)
        {
            Log.Information(
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
                projectDirectory.FullName ,
                fileInfo.Name.Replace(fileInfo.Extension , ".csproj" , InvariantCultureIgnoreCase)
            );

        FileInfo projectFile = new(csprojFilename);

        using StreamWriter writer = new (projectFile.OpenWrite());

        writer.Write(projectXml);

        writer.Close();

        if (!projectFile.Exists)
        {
            return false;
        }

        Log.Information($"[ProcessToken] Created project file: {csprojFilename}");

        List<SyntaxNode>? usingDirectives = GetUsingDirectives(fileInfo , root!);

        if (usingDirectives is { Count: > 0 })
        {
            string globalUsingsFileName = Path.Combine(projectDirectory.FullName , "GlobalUsings.cs");
            string code = string.Join(
                Environment.NewLine ,
                usingDirectives.Select(d => $"global {d.ToFullString().Trim()}"));
            File.WriteAllText(globalUsingsFileName, code);

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

        Project project = Solution.AddProject(
            projectFile.Name.Replace(projectFile.Extension , "" , InvariantCultureIgnoreCase) ,
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
                } ,
            }
        );

        if (project is null)
        {
            return true;
        }

        Solution.Save();

        Microsoft.Build.Evaluation.Project msbuildProject = project.GetMSBuildProject();
        Log.Information($"[ProcessToken] Added new project to Solution: {msbuildProject.FullPath}"
        );
        Log.Information(
            $"[ProcessToken] msbuildProject.AllEvaluatedItems.Count: {msbuildProject.AllEvaluatedItems.Count}"
        );
        Log.Information(
            $"[ProcessToken] msbuildProject.AllEvaluatedProperties.Count: {msbuildProject.AllEvaluatedProperties.Count}"
        );

        ProcessStartInfo info = new()
        {
            FileName = "git" ,
            Arguments = "add ." ,
            WorkingDirectory = projectDirectoryPath ,
            RedirectStandardOutput = true ,
            RedirectStandardError = true ,
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

    [ Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)") ]
    protected readonly Configuration Configuration = IsLocalBuild
        ? Configuration.Debug
        : Configuration.Release;

    [ GitRepository ] protected readonly GitRepository? GitRepository;
    [ GitVersion ] protected readonly GitVersion? GitVersion;

    [ Solution ] protected Solution? Solution { get; }
    private string? _githubToken;
    static Parser _instance;

    public Parser()
        : base()
        => Parser._instance = this;

    public RelativePath GetSolution()
    {
        RelativePath newSln =
            (RelativePath)(string)(SolutionDirectory
                .GlobFiles("*.sln")
                .FirstOrDefault());

        if (newSln is not null)
        {
            return newSln;
        }

        return (RelativePath)"./sfcs.sln";
    }

    protected static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    [PropertyTab("Folder to locate or create the solution file.")]
    protected static AbsolutePath SolutionDirectory { get; set; } = (AbsolutePath)Environment.CurrentDirectory;

    [Parameter("Github Personal Access Token") ]
    protected string? GithubToken
    {
        get => _githubToken;
        set
        {
            Log.Information($"GithubToken Length: {value?.Length ?? -1}");
            _githubToken = value;
        }
    }

    public static Parser Current
    {
        get => _instance;
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
            return DEFAULT_XML;
        }

        ISerializer serializer = new SerializerBuilder()
            .JsonCompatible()
            .Build();
        string json = serializer.Serialize(project);

        while (json.Contains("\"_", Ordinal))
        {
            json = json.Replace("\"_", "\"@", Ordinal);
        }

        XDocument? xml = JsonConvert.DeserializeXNode(json);

        return xml?.ToString() ?? DEFAULT_XML;
    }
}

public class SfcsSolutionAttribute : SolutionAttribute
{
    public SfcsSolutionAttribute()
        : base()
    {
        var parser = Parser.Current;

        MemberInfo[] memberInfos = typeof(SolutionAttribute)
            .GetMember("_relativePath", BindingFlags.NonPublic | BindingFlags.Instance);

        if (memberInfos is not
            {
                Length: > 0,
            })
        {
            return;
        }

        MemberInfo? mi = memberInfos.FirstOrDefault(static m
            => m.MemberType is MemberTypes.Field or MemberTypes.Property
        );

        if (mi is null)
        {
            return;
        }

        var sln = (string)parser.GetSolution();

        switch (mi)
        {
            case FieldInfo fi:
                fi.SetValue(this, sln);

                break;

            case PropertyInfo pi:
                pi.SetValue(this, sln);

                break;
        }
    }
}
