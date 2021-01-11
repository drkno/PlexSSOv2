using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlexSSO.Common.Controller;
using PlexSSO.Common.Plugin;
using PlexSSO.Common.Service.Config;
using PlexSSO.Model;
using PlexSSO.Service.OmbiClient;
using PlexSSO.Service.TautulliClient;
using HttpProtocol = PlexSSO.Common.Model.Types.HttpProtocol;

namespace PlexSSO.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RedirectController : BaseAuthController
    {
        private readonly IConfigurationService _configurationService;
        private readonly IEnumerable<IServiceAuthenticator> _serviceAuthenticators;
        private readonly IOmbiTokenService _ombiTokenService;
        private readonly ITautulliTokenService _tautulliTokenService;
        private readonly ILogger<RedirectController> _logger;

        public RedirectController(IConfigurationService configurationService,
                                  IEnumerable<IServiceAuthenticator> serviceAuthenticators,
                                  IOmbiTokenService ombiTokenService,
                                  ITautulliTokenService tautulliTokenService,
                                  ILogger<RedirectController> logger)
        {
            _configurationService = configurationService;
            _serviceAuthenticators = serviceAuthenticators;
            _ombiTokenService = ombiTokenService;
            _tautulliTokenService = tautulliTokenService;
            _logger = logger;
        }

        [HttpGet("{*location}")]
        public async Task<BasicResponse> Get(string location)
        {           
            var redirectComponents = GetRedirectComponents(location);
            var redirectStatus = 500;
            switch (GetRedirectType(redirectComponents))
            {
                case RedirectType.Ombi:
                    redirectStatus = await GenerateOmbiRedirect(redirectComponents);
                    break;
                case RedirectType.Tautulli:
                    redirectStatus = await GenerateTautulliRedirect(redirectComponents);
                    break;
                default:
                    redirectStatus = GenerateNormalRedirectUrl(redirectComponents);
                    break;
            }
            Response.StatusCode = redirectStatus;
            return new BasicResponse(true);
        }

        private async Task<int> GenerateOmbiRedirect((string, string, string) redirectComponents)
        {
            var (protocol, host, path) = redirectComponents;
            var ombiToken = await _ombiTokenService.GetOmbiToken(Identity.PlexAccessToken);
            Response.Headers.Add("Location", protocol + host + "/auth/cookie");
            Response.Cookies.Append("Auth", ombiToken.Value, new CookieOptions() {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddHours(1),
                Domain = _configurationService.GetConfig().CookieDomain,
                Path = "/",
                Secure = false
            });
            return 302;
        }

        private async Task<int> GenerateTautulliRedirect((string, string, string) redirectComponents)
        {
            var (protocol, host, path) = redirectComponents;
            var tautulliToken = await _tautulliTokenService.GetTautulliToken(Identity.PlexAccessToken);
            Response.Headers.Add("Location", protocol + host + "/home");
            Response.Cookies.Append("tautulli_token_" + tautulliToken.UUID, tautulliToken.Value, new CookieOptions {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddDays(7),
                Domain = _configurationService.GetConfig().CookieDomain,
                Path = "/",
                Secure = false
            });
            return 302;
        }

        private int GenerateNormalRedirectUrl((string, string, string) redirectComponents)
        {
            var (protocol, host, path) = redirectComponents;
            Response.Headers.Add("Location", protocol + host + path);
            return 302;
        }

        private RedirectType GetRedirectType((string, string, string) redirectComponents)
        {
            if (_configurationService.GetOmbiUrl()?.Contains(redirectComponents.Item2) ?? false)
            {
                return RedirectType.Ombi;
            }
            if (_configurationService.GetTautulliUrl()?.Contains(redirectComponents.Item2) ?? false)
            {
                return RedirectType.Tautulli;
            }
            return RedirectType.Normal;
        }

        private (string, string, string) GetRedirectComponents(string location)
        {
            var spl = location.Split('/');
            var host = GetHostWithPort(spl[0]);
            var path = "/" + string.Join('/', spl[1..]) + Request.QueryString.Value;
            var protocol = GetProtocol() == HttpProtocol.Https ? "https://" : "http://";
            return (protocol, host, path);
        }

        private string GetHostWithPort(string subdomain)
        {
            var port = Request.Host.Port.HasValue ? $":{Request.Host.Port.Value}" : "";
            var spl = Request.Host.Host.Split('.');
            string host;
            if (spl.Length < 3)
            {
                host = "." + Request.Host.Host;
            }
            else
            {
                host = Request.Host.Host.Substring(Request.Host.Host.IndexOf('.'));
            }
            return subdomain + host + port;
        }
    }
}
