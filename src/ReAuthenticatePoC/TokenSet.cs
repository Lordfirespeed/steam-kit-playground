namespace ReAuthenticatePoC;

public class TokenSet(string accessToken, string refreshToken)
{
    public string AccessToken = accessToken;
    public string RefreshToken = refreshToken;
}
