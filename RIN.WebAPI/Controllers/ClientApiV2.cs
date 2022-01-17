﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RIN.WebAPI.Controllers
{
    [ApiController]
    [Route("Clientapi/api/v2")]
    public partial class ClientApiV2 : TmwController
    {
        private readonly IConfiguration              Configuration;
        private readonly ILogger<OperatorController> Logger;

        public ClientApiV2(IConfiguration configuration, ILogger<OperatorController> logger)
        {
            Configuration = configuration;
            Logger        = logger;
        }
        
    }
}