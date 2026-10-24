using System;
using System.Text;

static class JsonFormatter
{
    internal static string Format(string json)
    {
        var output = new StringBuilder();
        int depth = 0;
        bool quoted = false, escaped = false;
        foreach (char ch in json)
        {
            if (quoted)
            {
                output.Append(ch);
                if (escaped)
                    escaped = false;
                else if (ch == '\\')
                    escaped = true;
                else if (ch == '"')
                    quoted = false;
                continue;
            }
            switch (ch)
            {
            case '"':
                quoted = true;
                output.Append(ch);
                break;
            case '{':
            case '[':
                output.Append(ch);
                output.Append(Environment.NewLine);
                depth++;
                output.Append(' ', depth * 2);
                break;
            case '}':
            case ']':
                output.Append(Environment.NewLine);
                depth--;
                output.Append(' ', depth * 2);
                output.Append(ch);
                break;
            case ',':
                output.Append(ch);
                output.Append(Environment.NewLine);
                output.Append(' ', depth * 2);
                break;
            case ':':
                output.Append(": ");
                break;
            default:
                if (!char.IsWhiteSpace(ch))
                    output.Append(ch);
                break;
            }
        }
        return output.ToString();
    }
}
