namespace Silver_Task.Server.Common
{
    /// <summary>
    /// Shared constants for the httpOnly cookie that carries the JWT access token.
    /// Using a cookie (rather than returning the token in the response body) keeps it
    /// out of reach of JavaScript, so an XSS bug can't be used to steal it.
    /// </summary>
    public static class AuthCookie
    {
        public const string Name = "silvertask_auth";
    }
}
