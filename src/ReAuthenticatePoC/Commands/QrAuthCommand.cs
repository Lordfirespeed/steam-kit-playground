using System;
using System.Threading;
using System.Threading.Tasks;
using QRCoder;
using ReAuthenticatePoC.Utils.Reprint;
using SteamKit2.Authentication;

namespace ReAuthenticatePoC.Commands;

public class QrAuthCommand(ProgramState state)
{
    private static readonly QRCodeGenerator QrGenerator = new();
    private static readonly Reprinter Reprinter = new();

    public async ValueTask Run(CancellationToken cancellationToken = default)
    {
        var authSessionDetails = new AuthSessionDetails {
            IsPersistentSession = true,
        };
        var authSession = await state.SteamClient.Authentication.BeginAuthSessionViaQRAsync(authSessionDetails);
        var initialDisplayTextLines = GetDisplayTextLines(authSession);
        AuthPollResult authResult;
        {
            // todo: replace reprinter with an HTTP/websocket server + browser client
            using var reprintSession = Reprinter.Open(initialDisplayTextLines.Length);

            authSession.ChallengeURLChanged = () =>
            {
                var displayTextLines = GetDisplayTextLines(authSession);
                reprintSession.PrintLines(displayTextLines);
            };

            reprintSession.PrintLines(initialDisplayTextLines);

            authResult = await authSession.PollingWaitForResultAsync(cancellationToken);
        }
        state.AccountName = authResult.AccountName;
        state.TokenSet = new TokenSet(authResult.AccessToken, authResult.RefreshToken);
        state.HasAuthenticated = true;
        Console.WriteLine("Authentication successful, retrieved TokenSet");
    }

    QRCodeData GetQrCode(QrAuthSession authSession)
    {
        return QrGenerator.CreateQrCode(authSession.ChallengeURL, QRCodeGenerator.ECCLevel.L);
    }

    string[] GetQrCodeAsciiLines(QrAuthSession authSession)
    {
        using var qrCode = GetQrCode(authSession);
        using var asciiQrCode = new AsciiQRCode(qrCode);
        return asciiQrCode.GetLineByLineGraphic( 1, drawQuietZones: false );
    }

    string[] GetDisplayTextLines(QrAuthSession authSession) => [
        $"Challenge URL: {authSession.ChallengeURL}",
        "",
        "Use the Steam Mobile App to sign in via QR code:",
        ..GetQrCodeAsciiLines(authSession),
    ];
}
