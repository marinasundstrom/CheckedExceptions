using System.Text;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace CheckedExceptions;

public static class XmlDocumentationHelper
{
    public static Dictionary<string, XElement> CreateMemberLookup(XDocument xmlDoc)
    {
        // Query the <member> elements
        var members = xmlDoc.Descendants("member");

        // Build the lookup
        var lookup = members
            .Where(m => m.Attribute("name") != null) // Ensure the member has a 'name' attribute
            .ToDictionary(
                m => m.Attribute("name").Value,        // Key: the member's name attribute
                m => m                   // Value: the inner XML or text content
            );

        return lookup;
    }
}
