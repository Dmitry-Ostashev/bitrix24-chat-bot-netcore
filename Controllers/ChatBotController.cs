using ChatBotNet.BotMessageProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace ChatBotNetCore.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class ChatBotController : ControllerBase {
        private MessageProcessor messageProcessor;
        private IHostingEnvironment hostingEnvironment;
        //private IApplicationEnvironment
        private IConfiguration configuration;
        public ChatBotController(IHostingEnvironment hostingEnvironment, IConfiguration configuration) {
            this.hostingEnvironment = hostingEnvironment;
            this.configuration = configuration;

            messageProcessor = new MessageProcessor(hostingEnvironment, configuration);
        }
        [Route("message")]
        [HttpPost]
        public object PostMessage(string values) {
            IFormCollection messageData = Request.Form;
            object result = null;
            object applicationToken = messageData.FirstOrDefault(s => s.Key == "auth[application_token]");

            if (applicationToken == null) {
                return Unauthorized();
            }
            else {
                try {
                    messageProcessor.WriteToLog(messageData.ToString());
                    result = messageProcessor.Process(this.HttpContext.Request, messageData);
                }
                catch (Exception ex) {
                    messageProcessor.WriteToLog(ex.Message);
                }
            }

            return Ok(result);
        }
        [Route("echo")]
        [HttpGet]
        public ActionResult<string> GetEcho() {
            return "Echo ok!";
        }
    }
}
