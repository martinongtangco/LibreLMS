using System.Xml.Linq;

namespace LibreLms.Modules.Scorm.Infrastructure;

/// <summary>
/// Parse SCORM 1.2 imsmanifest.xml to extract the launch SCO path and manifest title.
/// Uses System.Xml.Linq (LINQ to XML) — no additional dependencies.
/// </summary>
public class ManifestParser
{
    private static readonly XNamespace AdlNs = "http://www.adlnet.org/xsd/adlcp_v1p2";
    private static readonly XNamespace ImsSchemaNs = "http://www.imsglobal.org/xsd/imsmd_v1p2";

    /// <summary>
    /// Parse an imsmanifest.xml stream and extract launch info.
    /// Returns null if the manifest cannot be parsed or has no launchable SCO.
    /// </summary>
    public ParsedManifest? Parse(Stream manifestStream)
    {
        try
        {
            var doc = XDocument.Load(manifestStream);

            // Extract manifest title from metadata
            var title = ExtractTitle(doc);

            // Find the first resource that has an adlcp:scorm type (the SCO)
            var resources = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "resources");

            if (resources is null)
                return null;

            // Look for a resource with adlcp:scorm="sco" or href attribute
            var scoResource = resources.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "resource" &&
                    (e.Attribute(AdlNs + "scorm")?.Value == "sco" ||
                     e.Attribute("type")?.Value == "webcontent"));

            if (scoResource is null)
            {
                // Fallback: take first resource
                scoResource = resources.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "resource");
            }

            if (scoResource is null)
                return null;

            var href = scoResource.Attribute("href")?.Value;
            if (string.IsNullOrEmpty(href))
                return null;

            // Extract the first item's identifierref for launch path
            var items = scoResource.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "items");

            string launchPath = href;

            if (items is not null)
            {
                var firstItem = items.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "item");

                if (firstItem is not null)
                {
                    // Check for isvparameters with launch URL override
                    var isvParams = firstItem.Attribute("isvparameters")?.Value;
                    if (!string.IsNullOrEmpty(isvParams))
                    {
                        launchPath = isvParams.TrimStart('?');
                    }
                }
            }

            return new ParsedManifest(title ?? "Untitled SCORM Package", launchPath);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractTitle(XDocument doc)
    {
        // Try ADL metadata first
        var titleEl = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "title" &&
                (e.Name.Namespace == AdlNs ||
                 e.Name.Namespace == ImsSchemaNs ||
                 e.Name.Namespace == XNamespace.None));

        if (titleEl is not null && !string.IsNullOrWhiteSpace(titleEl.Value))
            return titleEl.Value.Trim();

        // Fallback: try general metadata title
        var metaTitle = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "title");

        return metaTitle?.Value?.Trim();
    }
}

/// <summary>Result of parsing a SCORM manifest.</summary>
public record ParsedManifest(string Title, string LaunchPath);
