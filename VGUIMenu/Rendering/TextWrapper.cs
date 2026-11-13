using System.Globalization;
using System.Text;

namespace VGUIMenu.Rendering;

internal static class TextWrapper
{
    /// <summary>
    ///     Wraps <paramref name="text" /> so no line exceeds <paramref name="maxLength" /> characters
    ///     (counted as text elements, so emoji / combining marks are never split).
    ///     Breaks on spaces; words longer than a line are hard-broken.
    ///     Existing '\n' are kept. Continuation lines start with <paramref name="indent" />.
    /// </summary>
    public static string Wrap(string text, int maxLength, string indent = "")
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (indent.Length >= maxLength)
        {
            indent = string.Empty;
        }

        var lines = new List<string>();

        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            WrapParagraph(paragraph, maxLength, indent, lines);
        }

        return string.Join('\n', lines);
    }

    private static void WrapParagraph(string paragraph, int maxLength, string indent, List<string> output)
    {
        var  sb    = new StringBuilder();
        var  len   = 0;
        var  first = true;
        var  start = output.Count;

        int Limit()
            => first ? maxLength : maxLength - indent.Length;

        void Flush()
        {
            output.Add(first ? sb.ToString() : indent + sb);
            sb.Clear();
            len   = 0;
            first = false;
        }

        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var elements = TextElements(word);
            var needed   = (len == 0 ? 0 : 1) + elements.Count;

            if (len + needed <= Limit())
            {
                if (len != 0)
                {
                    sb.Append(' ');
                }

                sb.Append(word);
                len += needed;

                continue;
            }

            if (len != 0)
            {
                Flush();
            }

            var offset = 0;

            while (elements.Count - offset > Limit())
            {
                var take = Limit();

                for (var i = 0; i < take; i++)
                {
                    sb.Append(elements[offset + i]);
                }

                offset += take;
                len    =  take;
                Flush();
            }

            for (var i = offset; i < elements.Count; i++)
            {
                sb.Append(elements[i]);
            }

            len = elements.Count - offset;
        }

        if (len != 0 || output.Count == start)
        {
            Flush();
        }
    }

    private static List<string> TextElements(string word)
    {
        var list = new List<string>(word.Length);
        var e    = StringInfo.GetTextElementEnumerator(word);

        while (e.MoveNext())
        {
            list.Add(e.GetTextElement());
        }

        return list;
    }
}
