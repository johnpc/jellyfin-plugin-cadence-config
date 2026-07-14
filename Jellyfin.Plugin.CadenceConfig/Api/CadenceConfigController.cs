using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CadenceConfig.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Serves the Cadence client its runtime config. Authenticated (any signed-in Jellyfin user) so
    /// the config is only handed to real users of the server, and never carries a secret. The
    /// mapping itself lives in the unit-tested <see cref="CadenceConfigResult.FromConfiguration"/>;
    /// this controller is thin plumbing.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Cadence")]
    [ExcludeFromCodeCoverage]
    public class CadenceConfigController : ControllerBase
    {
        /// <summary>
        /// Gets the non-secret runtime config for the Cadence client (marlin/signup/cast URLs and
        /// whether the Lidarr proxy is available). The client merges this into its runtime config.
        /// </summary>
        /// <returns>The client config.</returns>
        [HttpGet("Config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<CadenceConfigResult> GetConfig()
        {
            return CadenceConfigResult.FromConfiguration(Plugin.GetConfiguration());
        }
    }
}
