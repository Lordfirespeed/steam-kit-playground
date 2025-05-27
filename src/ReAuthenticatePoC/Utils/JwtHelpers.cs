using System;
using System.Text.Json;

namespace ReAuthenticatePoC.Utils;

public static class JwtHelpers
{
    public static string FormatJsonWebTokenContents( string token )
    {
        if (token is null or "") return "[ no token ]";

        // You can use a JWT library to do the parsing for you
        var tokenComponents = token.Split( '.' );

        // Fix up base64url to normal base64
        var base64 = tokenComponents[ 1 ].Replace( '-', '+' ).Replace( '_', '/' );

        if ( base64.Length % 4 != 0 )
        {
            base64 += new string( '=', 4 - base64.Length % 4 );
        }

        var payloadBytes = Convert.FromBase64String( base64 );

        // Payload can be parsed as JSON, and then fields such expiration date, scope, etc can be accessed
        var payload = JsonDocument.Parse( payloadBytes );

        // For brevity we will simply output formatted json to console
        var formatted = JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions {
                WriteIndented = true,
            }
        );
        return formatted;
    }
}
