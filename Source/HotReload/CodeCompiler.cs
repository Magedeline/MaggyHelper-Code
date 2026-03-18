using System.Reflection;
using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using RoslynPlatform = Microsoft.CodeAnalysis.Platform;

namespace MaggyHelper.HotReload;

/// <summary>
/// Dynamic code compiler using Roslyn.
/// Compiles C# source files into assemblies at runtime for hot reloading.
/// </summary>
public class CodeCompiler : IDisposable
{
    #region Fields
    
    private readonly string _sourcePath;
    private readonly List<MetadataReference> _references;
    private readonly CSharpCompilationOptions _compilationOptions;
    private readonly CSharpParseOptions _parseOptions;
    private int _assemblyCounter = 0;
    private bool _disposed = false;
    
    // Cache for assembly references
    private static readonly Dictionary<string, MetadataReference> _referenceCache = new Dictionary<string, MetadataReference>();
    
    #endregion
    
    #region Constructor
    
    /// <summary>
    /// Create a new code compiler for the given source path.
    /// </summary>
    /// <param name="sourcePath">Path to the source code directory.</param>
    public CodeCompiler(string sourcePath)
    {
        _sourcePath = sourcePath;
        
        // Initialize compilation options
        _compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Debug,
            allowUnsafe: true,
            platform: RoslynPlatform.AnyCpu
        );
        
        // Initialize parse options
        _parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest,
            preprocessorSymbols: new[] { "DEBUG", "HOTRELOAD" }
        );
        
        // Collect references
        _references = CollectReferences();
        
        Logger.Log(LogLevel.Info, "HotReload", $"CodeCompiler initialized with {_references.Count} references");
    }
    
    #endregion
    
    #region Compilation Methods
    
    /// <summary>
    /// Compile a single source file.
    /// </summary>
    public CompilationResult CompileFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { $"File not found: {filePath}" }
            };
        }
        
        try
        {
            string sourceCode = File.ReadAllText(filePath);
            string fileName = Path.GetFileName(filePath);
            
            Logger.Log(LogLevel.Debug, "HotReload", $"Compiling: {fileName}");
            
            // Parse the source file
            var syntaxTree = CSharpSyntaxTree.ParseText(
                sourceCode,
                _parseOptions,
                filePath
            );
            
            // Check for parse errors
            var parseDiagnostics = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
            if (parseDiagnostics.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = parseDiagnostics.Select(FormatDiagnostic).ToList()
                };
            }
            
            // Also include GlobalUsings if it exists
            var syntaxTrees = new List<SyntaxTree> { syntaxTree };
            
            string globalUsingsPath = Path.Combine(_sourcePath, "GlobalUsings.cs");
            if (File.Exists(globalUsingsPath))
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                    File.ReadAllText(globalUsingsPath),
                    _parseOptions,
                    globalUsingsPath
                ));
            }
            
            // Create unique assembly name
            string assemblyName = $"HotReload_{Path.GetFileNameWithoutExtension(filePath)}_{++_assemblyCounter}";
            
            // Compile
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                _references,
                _compilationOptions
            );
            
            return EmitAssembly(compilation);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "HotReload", $"Compilation exception: {ex}");
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Compilation exception: {ex.Message}" }
            };
        }
    }
    
    /// <summary>
    /// Compile multiple source files together.
    /// </summary>
    public CompilationResult CompileFiles(IEnumerable<string> filePaths)
    {
        try
        {
            var syntaxTrees = new List<SyntaxTree>();
            var errors = new List<string>();
            
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    errors.Add($"File not found: {filePath}");
                    continue;
                }
                
                string sourceCode = File.ReadAllText(filePath);
                var tree = CSharpSyntaxTree.ParseText(sourceCode, _parseOptions, filePath);
                
                // Check for parse errors
                var parseDiagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
                if (parseDiagnostics.Any())
                {
                    errors.AddRange(parseDiagnostics.Select(FormatDiagnostic));
                }
                else
                {
                    syntaxTrees.Add(tree);
                }
            }
            
            if (errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = errors
                };
            }
            
            if (!syntaxTrees.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = new List<string> { "No valid source files to compile" }
                };
            }
            
            // Create unique assembly name
            string assemblyName = $"HotReload_Multi_{++_assemblyCounter}";
            
            // Compile
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                _references,
                _compilationOptions
            );
            
            var result = EmitAssembly(compilation);
            result.CompiledFiles = syntaxTrees.Count;
            return result;
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "HotReload", $"Multi-file compilation exception: {ex}");
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Compilation exception: {ex.Message}" }
            };
        }
    }
    
    /// <summary>
    /// Compile all source files in the source directory.
    /// </summary>
    public CompilationResult CompileAll()
    {
        try
        {
            // Find all .cs files
            var sourceFiles = Directory.GetFiles(_sourcePath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .ToList();
            
            Logger.Log(LogLevel.Info, "HotReload", $"Compiling {sourceFiles.Count} source files...");
            
            return CompileFiles(sourceFiles);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "HotReload", $"CompileAll exception: {ex}");
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { $"CompileAll exception: {ex.Message}" }
            };
        }
    }
    
    #endregion
    
    #region Assembly Emission
    
    /// <summary>
    /// Emit the compiled assembly.
    /// </summary>
    private CompilationResult EmitAssembly(CSharpCompilation compilation)
    {
        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        
        var emitOptions = new EmitOptions(
            debugInformationFormat: DebugInformationFormat.PortablePdb,
            pdbFilePath: compilation.AssemblyName + ".pdb"
        );
        
        var emitResult = compilation.Emit(peStream, pdbStream, options: emitOptions);
        
        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(FormatDiagnostic)
                .ToList();
            
            return new CompilationResult
            {
                Success = false,
                Errors = errors
            };
        }
        
        // Load the assembly
        peStream.Seek(0, SeekOrigin.Begin);
        pdbStream.Seek(0, SeekOrigin.Begin);
        
        var assembly = Assembly.Load(peStream.ToArray(), pdbStream.ToArray());
        
        Logger.Log(LogLevel.Info, "HotReload", $"Successfully compiled: {compilation.AssemblyName}");
        
        return new CompilationResult
        {
            Success = true,
            Assembly = assembly,
            CompiledFiles = compilation.SyntaxTrees.Count()
        };
    }
    
    #endregion
    
    #region Reference Collection
    
    /// <summary>
    /// Collect all necessary assembly references for compilation.
    /// </summary>
    private List<MetadataReference> CollectReferences()
    {
        var references = new List<MetadataReference>();
        var loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Add runtime assemblies
        AddRuntimeReferences(references, loadedPaths);
        
        // Add game assemblies
        AddGameReferences(references, loadedPaths);
        
        // Add mod assemblies
        AddModReferences(references, loadedPaths);
        
        Logger.Log(LogLevel.Debug, "HotReload", $"Collected {references.Count} assembly references");
        
        return references;
    }
    
    private void AddRuntimeReferences(List<MetadataReference> references, HashSet<string> loadedPaths)
    {
        // Add core runtime assemblies
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator);
        
        if (trustedAssemblies != null)
        {
            foreach (var path in trustedAssemblies)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path) && loadedPaths.Add(path))
                {
                    try
                    {
                        references.Add(GetOrCreateReference(path));
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Warn, "HotReload", $"Failed to load reference {path}: {ex.Message}");
                    }
                }
            }
        }
        
        // Ensure core references are included
        AddAssemblyReference(references, loadedPaths, typeof(object).Assembly); // System.Runtime
        AddAssemblyReference(references, loadedPaths, typeof(Console).Assembly); // System.Console
        AddAssemblyReference(references, loadedPaths, typeof(System.Linq.Enumerable).Assembly); // System.Linq
    }
    
    private void AddGameReferences(List<MetadataReference> references, HashSet<string> loadedPaths)
    {
        // Add Celeste and FNA
        AddAssemblyReference(references, loadedPaths, typeof(global::Celeste.Celeste).Assembly);
        AddAssemblyReference(references, loadedPaths, typeof(Microsoft.Xna.Framework.Vector2).Assembly);
        AddAssemblyReference(references, loadedPaths, typeof(Monocle.Engine).Assembly);
        
        // Add MMHOOK for hooks
        var mmhookPath = Path.Combine(Path.GetDirectoryName(typeof(global::Celeste.Celeste).Assembly.Location), "MMHOOK_Celeste.dll");
        if (File.Exists(mmhookPath) && loadedPaths.Add(mmhookPath))
        {
            try
            {
                references.Add(GetOrCreateReference(mmhookPath));
            }
            catch { }
        }
    }
    
    private void AddModReferences(List<MetadataReference> references, HashSet<string> loadedPaths)
    {
        // Add this mod's assembly
        AddAssemblyReference(references, loadedPaths, typeof(MaggyHelperModule).Assembly, preferLooseModCopy: true);
        
        // Add MonoMod for runtime detour
        AddAssemblyReference(references, loadedPaths, typeof(MonoMod.RuntimeDetour.Hook).Assembly);
        AddAssemblyReference(references, loadedPaths, typeof(MonoMod.Utils.DynamicMethodDefinition).Assembly);
        
        // Add other loaded mod assemblies that might be dependencies
        foreach (var mod in Everest.Modules)
        {
            if (mod?.Metadata?.DLL != null)
            {
                AddAssemblyReference(references, loadedPaths, mod.GetType().Assembly);
            }
        }
    }
    
    private void AddAssemblyReference(List<MetadataReference> references, HashSet<string> loadedPaths, Assembly assembly, bool preferLooseModCopy = false)
    {
        try
        {
            string location = ResolveAssemblyReferencePath(assembly, preferLooseModCopy);
            if (!string.IsNullOrEmpty(location) && File.Exists(location) && loadedPaths.Add(location))
            {
                references.Add(GetOrCreateReference(location));
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "HotReload", $"Failed to add assembly reference: {ex.Message}");
        }
    }

    private string ResolveAssemblyReferencePath(Assembly assembly, bool preferLooseModCopy)
    {
        if (assembly == null)
            return null;

        if (preferLooseModCopy)
        {
            string loosePath = TryResolveLooseModAssemblyPath(assembly);
            if (!string.IsNullOrEmpty(loosePath))
                return loosePath;
        }

        try
        {
            return assembly.Location;
        }
        catch
        {
            return null;
        }
    }

    private string TryResolveLooseModAssemblyPath(Assembly assembly)
    {
        if (string.IsNullOrEmpty(_sourcePath))
            return null;

        string assemblyFileName = assembly.GetName().Name + ".dll";
        string modRoot = Path.GetFullPath(Path.Combine(_sourcePath, ".."));

        string deployedPath = Path.Combine(modRoot, "Code", "net8.0", assemblyFileName);
        if (File.Exists(deployedPath))
            return deployedPath;

        string debugOutputPath = Path.Combine(_sourcePath, "bin", "Debug", "net8.0", assemblyFileName);
        if (File.Exists(debugOutputPath))
            return debugOutputPath;

        return null;
    }
    
    private static MetadataReference GetOrCreateReference(string path)
    {
        if (!_referenceCache.TryGetValue(path, out var reference))
        {
            // Use an in-memory PE image so Roslyn does not keep Everest cache DLLs locked.
            reference = MetadataReference.CreateFromImage(ImmutableArray.Create(File.ReadAllBytes(path)), filePath: path);
            _referenceCache[path] = reference;
        }
        return reference;
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Format a diagnostic message for display.
    /// </summary>
    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var lineSpan = location.GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        var column = lineSpan.StartLinePosition.Character + 1;
        var file = Path.GetFileName(lineSpan.Path);
        
        return $"{file}({line},{column}): {diagnostic.Severity} {diagnostic.Id}: {diagnostic.GetMessage()}";
    }
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        _references?.Clear();
        
        Logger.Log(LogLevel.Debug, "HotReload", "CodeCompiler disposed");
    }
    
    #endregion
}

/// <summary>
/// Result of a compilation operation.
/// </summary>
public class CompilationResult
{
    /// <summary>
    /// Whether the compilation succeeded.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// The compiled assembly (if successful).
    /// </summary>
    public Assembly Assembly { get; set; }
    
    /// <summary>
    /// List of compilation errors (if failed).
    /// </summary>
    public List<string> Errors { get; set; } = new List<string>();
    
    /// <summary>
    /// Number of source files compiled.
    /// </summary>
    public int CompiledFiles { get; set; }
    
    /// <summary>
    /// List of warnings (even if successful).
    /// </summary>
    public List<string> Warnings { get; set; } = new List<string>();
}
