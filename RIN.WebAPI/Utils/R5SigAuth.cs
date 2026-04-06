using FauFau.Net.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using RIN.Core;
using RIN.Core.DB;
using RIN.WebAPI.Models;
using System.Web;

namespace RIN.WebAPI.Utils
{
    public class R5SigAuthRequiredAttribute : TypeFilterAttribute
    {
        public R5SigAuthRequiredAttribute() : base(typeof(R5SigAuth))
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class R5SigAuth : Attribute, IAsyncAuthorizationFilter
    {
        private WebApiConfigSettings Config;
        private DB Db;
        private IMemoryCache Cache;

        public R5SigAuth(IOptions<WebApiConfigSettings> config, DB db, IMemoryCache cache)
        {
            Config = config.Value;
            Db     = db;
            Cache  = cache;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (!Config.VerifyRed5Sig)
                return;

            var request = context.HttpContext.Request;
            var str = request.Headers.TryGetValue("X-Red5-Signature", out StringValues header) ? header.FirstOrDefault() : null;
            if (str == null)
            {
                context.Result = CreateError(Error.Codes.ERR_INCORRECT_USERPASS, "No Signature");
                return;
            }

            // Parse signature info into non-ref types using a non-async helper
            if (!ParseSignature(str, out string uid, out string? expectedBodyHash))
            {
                // Identification failed, but we already checked str != null
            }

            // Verify request body if expected
            if (expectedBodyHash != null)
            {
                request.EnableBuffering();
                var bodyBytes = new byte[request.ContentLength ?? 0];
                if (bodyBytes.Length > 0)
                {
                    await request.Body.ReadExactlyAsync(bodyBytes, context.HttpContext.RequestAborted);
                }

                request.Body.Position = 0; // Reset for the controller

                using var sha1 = System.Security.Cryptography.SHA1.Create();
                var hashBytes = sha1.ComputeHash(bodyBytes);
                var actualBodyHash = Convert.ToHexString(hashBytes).ToLower();

                if (expectedBodyHash != actualBodyHash)
                {
                    context.Result = CreateError(Error.Codes.ERR_INCORRECT_USERPASS, "Body Integrity Failure");
                    return;
                }
            }

            // TODO: Cache the users uuid to account id to skip a db call
            if (!Cache.TryGetValue(uid, out (string secret, long accountId) loginResult))
            {
                var dbResult = await Db.GetLoginData(uid);
                if (dbResult == null)
                {
                    context.Result = CreateError(Error.Codes.ERR_INCORRECT_USERPASS);
                    return;
                }
                loginResult = (dbResult.secret, dbResult.account_id);
                
                // Cache for 5 minutes
                Cache.Set(uid, loginResult, TimeSpan.FromMinutes(5));
            }

            if (!VerifySignature(loginResult.secret, str))
            {
                context.Result = CreateError(Error.Codes.ERR_INCORRECT_USERPASS);
            }
        }

        private static bool ParseSignature(string header, out string uid, out string? bodyHash)
        {
            var sig = Red5Sig.ParseString(header);
            uid      = HttpUtility.UrlDecode(sig.UID.ToString());
            bodyHash = sig.Body.IsEmpty ? null : sig.Body.ToString();
            return true;
        }

        private static bool VerifySignature(string secret, string header)
        {
            return Auth.Verify(secret, header);
        }

        private ObjectResult CreateError(string code, string? msg = null)
        {
            var error         = new Error(code, msg);
            var result        = new ObjectResult(error);
            result.StatusCode = 500;

            return result;
        }
    }
}
