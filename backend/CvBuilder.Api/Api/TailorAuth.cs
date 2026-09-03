using System.Security.Cryptography;
using System.Text;

namespace CvBuilder.Api.Api;

/// <summary>
/// The one shared passphrase that guards the DeepSeek endpoints. Set it with
/// Auth__Password (or user-secrets "Auth:Password"). Leave it empty and the endpoints
/// are open, which is what you want on localhost.
/// </summary>
public class TailorAuthOptions
{
    public string Password { get; set; } = "";

    public bool IsRequired => !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// Authentication without state: no session, no token, no store. The browser sends the
/// passphrase on every gated call, exactly as it sends the CV. Only the calls that spend
/// money are gated — editing, preview, PDF and save files stay open.
/// </summary>
public static class TailorAuth
{
    public const string HeaderName = "X-Cv-Auth";

    public static RouteHandlerBuilder RequirePassphrase(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<TailorAuthOptions>();
            if (!options.IsRequired) return await next(context);

            var supplied = context.HttpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(supplied))
                return Results.Problem(
                    "This API needs a passphrase before it will ask DeepSeek anything.",
                    statusCode: StatusCodes.Status401Unauthorized);

            if (!Matches(supplied, options.Password))
                return Results.Problem(
                    "That passphrase is not right.",
                    statusCode: StatusCodes.Status401Unauthorized);

            return await next(context);
        });

    /// <summary>
    /// Compared as fixed-length hashes so neither the passphrase's length nor how far a
    /// guess got through it can be read off the response time.
    /// </summary>
    private static bool Matches(string supplied, string expected) =>
        CryptographicOperations.FixedTimeEquals(Hash(supplied), Hash(expected));

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
