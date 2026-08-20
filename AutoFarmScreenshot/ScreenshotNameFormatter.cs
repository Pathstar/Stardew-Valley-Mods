using System;
using System.Collections.Generic;
using System.Text;

namespace AutoFarmScreenshot;
public static class ScreenshotNameFormatter
{
    private static readonly string[] VariableNames =
    {
        "gameSaveName",
        "playerName",
        "year",
        "month",
        "day",
        "hour",
        "minute",
        "second",
        "millisecond",
        "totalMilliseconds",
        "gameYear",
        "gameSeason",
        "gameDay",
        "gameTime",
        "location",
        "triggerBy"
    };

    private static readonly Dictionary<string, int> VariableIndices = CreateVariableIndices();

    private static Dictionary<string, int> CreateVariableIndices()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < VariableNames.Length; i++)
        {
            result[VariableNames[i]] = i;
        }

        return result;
    }

    public static string CompileTemplate(string template)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = new StringBuilder(template.Length);

        for (int i = 0; i < template.Length; i++)
        {
            if (template[i] != '{')
            {
                result.Append(template[i]);
                continue;
            }

            int end = template.IndexOf('}', i + 1);

            // 没有匹配的 }，原样保留
            if (end < 0)
            {
                result.Append(template[i]);
                continue;
            }

            string content = template.Substring(i + 1, end - i - 1);

            int colonIndex = content.IndexOf(':');

            string variableName;
            string format;

            if (colonIndex >= 0)
            {
                variableName = content.Substring(0, colonIndex);
                format = content.Substring(colonIndex);
            }
            else
            {
                variableName = content;
                format = string.Empty;
            }

            if (VariableIndices.TryGetValue(variableName, out int index))
            {
                result.Append('{');
                result.Append(index);
                result.Append(format);
                result.Append('}');
            }
            else
            {
                // 未知字段
                result.Append('{');
                result.Append(content);
                result.Append('}');
            }

            i = end;
        }

        return result.ToString();
    }

    // public static string Format(
    //     string compiledTemplate,
    //     string saveName,
    //     DateTime utcNow,
    //     int gameYear,
    //     string season,
    //     int gameDay,
    //     int gameTime,
    //     string location,
    //     string state)
    // {
    //     return string.Format(
    //         compiledTemplate,
    //         saveName,
    //         utcNow.Year,
    //         utcNow.Month,
    //         utcNow.Day,
    //         utcNow.Hour,
    //         utcNow.Minute,
    //         utcNow.Second,
    //         utcNow.Millisecond,
    //         gameYear,
    //         season,
    //         gameDay,
    //         gameTime,
    //         location,
    //         state
    //     );
    // }
}