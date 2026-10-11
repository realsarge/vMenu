using System.Globalization;
using System.Text;

namespace vMenuShared
{
    internal static class ScaleformText
    {
        // Scaleform fonts cannot draw emoji or arbitrary Unicode icon glyphs.
        // Keep ordinary localized names; also remove BMP emoji and variation selectors.
        internal static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var result = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                var category = char.GetUnicodeCategory(character);
                if (category == UnicodeCategory.Surrogate ||
                    character > 127 && (category == UnicodeCategory.OtherSymbol ||
                    category == UnicodeCategory.MathSymbol || category == UnicodeCategory.ModifierSymbol) ||
                    category == UnicodeCategory.Format || category == UnicodeCategory.Control ||
                    category == UnicodeCategory.EnclosingMark || character >= '\uFE00' && character <= '\uFE0F') continue;
                if (char.IsWhiteSpace(character) && (result.Length == 0 || char.IsWhiteSpace(result[result.Length - 1]))) continue;
                result.Append(character);
            }
            return result.ToString().Trim();
        }
    }
}
