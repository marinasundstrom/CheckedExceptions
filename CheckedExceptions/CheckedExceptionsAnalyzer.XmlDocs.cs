using System.Collections.Concurrent;
using System.Xml;
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

        if (string.IsNullOrEmpty(assemblyPath))
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

    private static IEnumerable<ExceptionInfo> GetExceptionTypesFromDocumentationCommentXml(Compilation compilation, XElement? xml)
    {
        try
        {
            return xml.Descendants("exception")
                .Select(e =>
                {
                    var cref = e.Attribute("cref")?.Value;
                    var crefValue = cref.StartsWith("T:") ? cref.Substring(2) : cref;
                    var innerText = e.Value;

                    var name = compilation.GetTypeByMetadataName(crefValue) ??
                           compilation.GetTypeByMetadataName(crefValue.Split('.').Last());

                    var parameters = e.Elements("paramref").Select(x => new ParamInfo(x.Attribute("name").Value));

                    return new ExceptionInfo(name, innerText, parameters);
                });
        }
        catch
        {
            // Handle or log parsing errors
            return Enumerable.Empty<ExceptionInfo>();
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
        return GetExceptionTypesFromDocumentationCommentXml(compilation, docCommentXml).ToList();
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
        if (path is not null)
        {
            if (!XmlDocPathsAndMembers.TryGetValue(path, out var lookup))
            {
                try
                {
                    using var reader = XmlReader.Create(path, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Ignore,
                        IgnoreComments = true,
                        IgnoreWhitespace = true,
                        CloseInput = true
                    });

                    var file = XmlDocumentationHelper.CreateMemberLookup(XDocument.Load(reader, LoadOptions.PreserveWhitespace));
                    lookup = new ConcurrentDictionary<string, XElement>(file);
                    XmlDocPathsAndMembers.TryAdd(path, lookup);
                }
                catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Suppress AD0001-inducing exceptions in analyzers
                    XmlDocPathsAndMembers.TryAdd(path, new ConcurrentDictionary<string, XElement>());
                    return null;
                }
            }
            var member = symbol.GetDocumentationCommentId();

            if (lookup.TryGetValue(member, out var xml))
            {
                return xml;
            }
        }

        return null;
    }
}