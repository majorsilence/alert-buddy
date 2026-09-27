using System.Globalization;
using System.Text;

namespace AlertBuddy.Core.Interpretation
{
    /// <summary>Removes the emoji and spaces at the start of a title, so "🔥 Workshop: too warm" reads as "Workshop: too warm".</summary>
    public static class EmojiStripper
    {
        /// <summary>
        /// Strips leading whitespace and emoji. Only the start is touched: a symbol in the middle of a title is the sender's, and a
        /// leading degree sign or copyright mark is not an emoji, so the ranges are the emoji blocks and not "any symbol".
        /// </summary>
        public static string StripLeading (string text)
        {
            if (string.IsNullOrEmpty (text))
                return "";

            var index = 0;
            while (index < text.Length) {
                // A lone surrogate is not a rune; stop there rather than throw on a title the server mangled.
                if (!Rune.TryGetRuneAt (text, index, out var rune) || !(Rune.IsWhiteSpace (rune) || IsEmojiPart (rune)))
                    break;
                index += rune.Utf16SequenceLength;
            }

            return text[index..];
        }

        private static bool IsEmojiPart (Rune rune)
        {
            var v = rune.Value;

            // Emoji and pictograph blocks, and the regional indicators used in flags, all sit here.
            if (v is >= 0x1F000 and <= 0x1FAFF)
                return true;

            // Miscellaneous symbols and dingbats (the warning sign, the tick), miscellaneous technical (the hourglass, the watch) and
            // miscellaneous symbols and arrows (the star, the large squares).
            if (v is >= 0x2600 and <= 0x27BF or >= 0x2300 and <= 0x23FF or >= 0x2B00 and <= 0x2BFF)
                return true;

            // What glues an emoji sequence together: the zero-width joiner, the variation selectors, the keycap.
            if (v is 0x200D or 0xFE0F or 0xFE0E or 0x20E3)
                return true;

            // Skin tone modifiers are their own code points.
            return CharUnicodeInfo.GetUnicodeCategory (v) == UnicodeCategory.ModifierSymbol && v is >= 0x1F3FB and <= 0x1F3FF;
        }
    }
}
