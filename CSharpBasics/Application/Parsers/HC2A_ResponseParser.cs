using System.Globalization;
using CSharpBasics.Domain.Models;

namespace CSharpBasics.Application.Parsers;

public static class HC2A_ResponseParser
{
    public static HC2A_Reading Parse(string response)
    {
        var parts = response.Split(';');

        if (parts.Length <= 5)
        {
            throw new FormatException("Invalid HC2A_ response format.");
        }

        if (!double.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture, out double humidity))
        {
            throw new FormatException("Invalid HC2A_ humidity value.");
        }

        if (!double.TryParse(parts[5].Trim(), CultureInfo.InvariantCulture, out double temperature))
        {
            throw new FormatException("Invalid HC2A_ temperature value.");
        }

        return new HC2A_Reading(
            humidity,
            temperature,
            response,
            DateTime.Now
        );
    }
}