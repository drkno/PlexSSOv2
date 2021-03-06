using Microsoft.AspNetCore.Mvc;
using PlexSSO.Model;
using PlexSSO.Model.API;
using PlexSSO.Service.Auth;

namespace PlexSSO.Controllers
{
    [ApiController]
    [Route(Constants.ControllerPath)]
    public class SsoController : CommonAuthController
    {
        private readonly IAuthValidator _authValidator;

        public SsoController(IAuthValidator authValidator)
        {
            _authValidator = authValidator;
        }

        [HttpGet]
        public SsoResponse Get()
        {
            var response = _authValidator.ValidateAuthenticationStatus(Identity, ServiceName, ServiceUri);
            Response.StatusCode = response.Status;
            return response;
        }
    }
}
