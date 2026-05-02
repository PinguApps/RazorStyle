using System.Text;

namespace PinguApps.RazorStyle.Core.Formatting;

/// <summary>
/// Rewrites Razor start tags to satisfy attribute wrapping and alignment.
/// </summary>
public sealed class AttributeWrappingFixer
{
    /// <summary>
    /// Formats one parsed start tag.
    /// </summary>
    public string Format(TagInfo tag, string newLine)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(newLine);

        if (tag.Attributes.Count == 0)
        {
            return string.Empty;
        }

        string closeMarker = tag.IsSelfClosing ? " />" : ">";

        if (tag.Attributes.Count == 1)
        {
            return "<" + tag.Name + " " + tag.Attributes[0].RawText + closeMarker;
        }

        int firstAttributeColumn = tag.NameColumn + tag.Name.Length + 1;
        string alignment = new(' ', firstAttributeColumn - 1);
        StringBuilder builder = new();

        builder.Append('<');
        builder.Append(tag.Name);
        builder.Append(' ');
        builder.Append(tag.Attributes[0].RawText);

        for (int index = 1; index < tag.Attributes.Count; index++)
        {
            builder.Append(newLine);
            builder.Append(alignment);
            builder.Append(tag.Attributes[index].RawText);
        }

        builder.Append(closeMarker);
        return builder.ToString();
    }
}

