using System.Collections.Concurrent;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    // A thread-safe dictionary to cache XML documentation paths
    private static readonly ConcurrentDictionary<string, string?> XmlDocPathsCache = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, XElement>?> XmlDocPathsAndMembers = new();

    private string? GetXmlDocumentationPath(Compilation compilation, IAssemblySymbol assemblySymbol)
    {
        var assemblyName = assemblySymbol.Name;
        var assemblyPath = assemblySymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;

        if (string.IsNullOrEmpty(assemblyPath))
        {
            // Fallback: Attempt to get the path from the MetadataReference
            var metadataReference = compilation.References
                .FirstOrDefault(r => compilation.GetAssemblyOrModuleSymbol(r)?.Name == assemblyName);

            if (metadataReference is not null)
            {
                if (metadataReference is PortableExecutableReference peReference)
                {
                    assemblyPath = peReference.FilePath;
                }
            }
        }

        // explicitly check instead of using string.IsNullOrEmpty because netstandard2.0 does not support NotNullWhenAttribute
        if (assemblyPath is null || assemblyPath.Length == 0)
            return null;

        // Check cache first
        if (XmlDocPathsCache.TryGetValue(assemblyPath, out var cachedPath))
        {
            return cachedPath;
        }

        // Assume XML doc is in the same directory with the same base name
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");

#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        if (File.Exists(xmlDocPath))
        {
            XmlDocPathsCache[assemblyPath] = xmlDocPath;
            return xmlDocPath;
        }
#pragma warning restore RS1035 // Do not use APIs banned for analyzers

        // Handle .NET Core / .NET 5+ SDK paths
        // Attempt to locate XML docs in SDK installation directories
        // This requires knowledge of the SDK paths, which can vary
        // A heuristic approach is necessary

        // Example heuristic (may need adjustments based on environment)
        var sdkXmlDocPath = Path.Combine(
            Path.GetDirectoryName(assemblyPath) ?? string.Empty,
            "..", "xml",
            $"{assemblyName}.xml");

        sdkXmlDocPath = Path.GetFullPath(sdkXmlDocPath);

#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        if (File.Exists(sdkXmlDocPath))
        {
            XmlDocPathsCache[assemblyPath] = sdkXmlDocPath;
            return sdkXmlDocPath;
        }
#pragma warning restore RS1035 // Do not use APIs banned for analyzers

        // XML documentation not found
        XmlDocPathsCache[assemblyPath] = null;
        return null;
    }

    public record struct ParamInfo(string Name);

    public record struct ExceptionInfo(INamedTypeSymbol ExceptionType, string Description, IEnumerable<ParamInfo> Parameters);

    private static IEnumerable<ExceptionInfo> GetExceptionTypesFromDocumentationCommentXml(Compilation compilation, XElement xml)
    {
        try
        {
            return xml.Descendants("exception")
                .Select(e =>
                {
                    string? cref = e.Attribute("cref")?.Value;
                    if (string.IsNullOrWhiteSpace(cref))
                    {
                        return default;
                    }

                    var exceptionTypeSymbol = GetExceptionTypeSymbolFromCref(cref!, compilation);
                    if (exceptionTypeSymbol is null)
                    {
                        return default;
                    }

                    string innerText = e.Value;

                    IEnumerable<ParamInfo> parameters = e.Elements("paramref")
                        .Select(x => new ParamInfo(x.Attribute("name")?.Value!))
                        .Where(p => !string.IsNullOrWhiteSpace(p.Name));

                    return new ExceptionInfo(exceptionTypeSymbol, innerText, parameters);
                })
                .Where(x => x != default)
                .ToList(); // Materialize to catch parsing errors
        }
        catch
        {
            // Handle or log parsing errors
            return Enumerable.Empty<ExceptionInfo>();
        }

        static INamedTypeSymbol? GetExceptionTypeSymbolFromCref(string cref, Compilation compilation)
        {
            string exceptionTypeName = cref.StartsWith("T:", StringComparison.Ordinal) ? cref.Substring(2) : cref;
            string cleanExceptionTypeName = RemoveGenericParameters(exceptionTypeName);

            INamedTypeSymbol? typeSymbol = compilation.GetTypeByMetadataName(cleanExceptionTypeName);
            if (typeSymbol is null && !cleanExceptionTypeName.Contains('.'))
            {
                typeSymbol = compilation.GetTypeByMetadataName($"System.{cleanExceptionTypeName}");
            }

            return typeSymbol;
        }

        static string RemoveGenericParameters(string typeName)
        {
            // Handle generic types like "System.Collections.Generic.List`1"
            var backtickIndex = typeName.IndexOf('`');
            if (backtickIndex >= 0)
            {
                return typeName.Substring(0, backtickIndex);
            }

            // Handle generic syntax like "List<T>"
            var angleIndex = typeName.IndexOf('<');
            if (angleIndex >= 0)
            {
                return typeName.Substring(0, angleIndex);
            }

            return typeName;
        }
    }

    /// <summary>
    /// Retrieves exception types declared in XML documentation.
    /// </summary>
    private IEnumerable<ExceptionInfo> GetExceptionTypesFromDocumentationCommentXml(Compilation compilation, ISymbol symbol)
    {
        XElement? docCommentXml = GetDocumentationCommentXmlForSymbol(compilation, symbol);

        if (docCommentXml is null)
        {
            return Enumerable.Empty<ExceptionInfo>();
        }

        // Attempt to get exceptions from XML documentation
        return GetExceptionTypesFromDocumentationCommentXml(compilation, docCommentXml);
    }

    readonly bool loadFromProject = true;

    private XElement? GetDocumentationCommentXmlForSymbol(Compilation compilation, ISymbol symbol)
    {
        // Retrieve comment from project in solution that is being built
        var docCommentXmlString = symbol.GetDocumentationCommentXml();

        XElement? docCommentXml;

        if (!string.IsNullOrEmpty(docCommentXmlString) && loadFromProject)
        {
            try
            {
                docCommentXml = XElement.Parse(docCommentXmlString);
            }
            catch
            {
                // Badly formed XML
                return null;
            }
        }
        else
        {
            // Retrieve comment from referenced libraries (framework and DLLs in NuGet packages etc)
            docCommentXml = GetXmlDocumentation(compilation, symbol);
        }

        return docCommentXml;
    }

    public XElement? GetXmlDocumentation(Compilation compilation, ISymbol symbol)
    {
        var path = GetXmlDocumentationPath(compilation, symbol.ContainingAssembly);
        if (path is null)
        {
            return null;
        }

        if (!XmlDocPathsAndMembers.TryGetValue(path, out var lookup) || lookup is null)
        {
            var file = XmlDocumentationHelper.CreateMemberLookup(XDocument.Load(path));
            lookup = new ConcurrentDictionary<string, XElement>(file);
            XmlDocPathsAndMembers[path] = lookup;
        }

        var member = symbol.GetDocumentationCommentId();

        return member is not null && lookup.TryGetValue(member, out var xml) ? xml : null;
    }
}