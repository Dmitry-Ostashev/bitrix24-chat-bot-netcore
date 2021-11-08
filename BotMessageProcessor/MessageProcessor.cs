using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace ChatBotNet.BotMessageProcessor {
    public static class BotEvents {
        public const string OnInstall = "ONAPPINSTALL";
        public const string OnMessageAdd = "ONIMBOTMESSAGEADD";
    }

    public class MessageProcessor {
        private const string UNKNOWN_COMMAND_MESSAGE = "Unknown command! Please type [send=info]info[/send] to get a list of all commands.";
        private const string COMMAND_ERROR_MESSAGE = "Ann error occurred during the command execution. Please try again later.";

        private string accessToken;
        private JObject commandListConfig;
        private string bitrixBaseUrl;
        private IHostingEnvironment hostingEnvironment;
        private IConfiguration configuration;
        private bool isTestMode;

        public MessageProcessor(IHostingEnvironment hostingEnvironment, IConfiguration configuration) {
            this.hostingEnvironment = hostingEnvironment;
            this.configuration = configuration;
        }

        private string GetCommandName(string message, out string[] messageArgs) {
            string resultMessage = message.Trim();

            if (resultMessage.Contains(' ')) {
                string[] messageParts = resultMessage.Split(' ');
                resultMessage = messageParts[0];
                messageArgs = messageParts.Skip(1).ToArray();
            }
            else {
                messageArgs = null;
            }

            return resultMessage.ToLower();
        }

        private HttpResponseMessage ProcessRequest(string url, string method, Dictionary<string, string> data, Dictionary<string, string> headers = null) {
            HttpClient httpClient = new HttpClient();

            if (headers != null) {
                foreach (KeyValuePair<string, string> header in headers) {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            HttpResponseMessage result = null;

            if (method == "POST") {
                result = httpClient.PostAsync(url, new FormUrlEncodedContent(data)).Result;
            }
            else {
                result = httpClient.GetAsync(url).Result;
            }

            return result;
        }
        private object ProcessMessageAdd(IFormCollection messageData) {            
            string dialogId = messageData["data[PARAMS][DIALOG_ID]"];
            string username = messageData["data[USER][NAME]"];
            string userId = messageData["data[USER][ID]"];

            string message = messageData["data[PARAMS][MESSAGE]"];
            Dictionary<string, string> answer = GetMessageAnswer(message, userId, dialogId);
            answer["auth"] = accessToken;

            if (isTestMode) {
                return answer;
            }

            string url = string.Format("{0}/imbot.message.add", bitrixBaseUrl);
            HttpResponseMessage responseMessage = ProcessRequest(url, "POST", answer);

            WriteToLog(url);
            WriteToLog(responseMessage.ToString());

            return null;
        }
        private object ProcessMessageInstall(string handlerUrl) {
            JObject botProperties = DeserializeJsonFile("botProperties.json");
            Dictionary<string, string> result = new Dictionary<string, string>();

            result["CODE"] = "AnatolChatBot2";
            result["TYPE"] = "B";
            result["EVENT_MESSAGE_ADD"] = handlerUrl;
            result["EVENT_WELCOME_MESSAGE"] = handlerUrl;
            result["EVENT_BOT_DELETE"] = handlerUrl;

            foreach (KeyValuePair<string, JToken> property in botProperties) {
                result[string.Format("PROPERTIES[{0}]", property.Key)] = property.Value.ToString();
            }

            result["auth"] = accessToken;
            string url = string.Format("{0}/imbot.register", bitrixBaseUrl);
            HttpResponseMessage responseMessage = ProcessRequest(url, "POST", result);

            WriteToLog(url);
            WriteToLog(responseMessage.ToString());

            return null;
        }

        private IDictionary<string, JToken> FindCurrentCommand(IEnumerable<object> commandList, string message) {            
            foreach (object commandObject in commandList) {
                IDictionary<string, JToken> command = (IDictionary<string, JToken>)commandObject;
                if (message.Contains(command["name"].ToString())) {
                    return command;
                }
            }
            return null;
        }

        private Dictionary<string, string> GetMessageAnswer(string message, string userId, string dialogId) {
            string actionBaseUrl              = configuration.GetSection("BotSettings:actionBaseUrl").Value;
            Dictionary<string, string> result = new Dictionary<string, string>();

            result["DIALOG_ID"] = dialogId;

            IEnumerable<object> commandList = commandListConfig["commandList"] as IEnumerable<object>;
            string defaultCommand = commandListConfig["defaultCommand"].ToString();
            IDictionary<string, JToken> currentCommand = FindCurrentCommand(commandList, message);

            if (currentCommand == null) {
                result["MESSAGE"] = UNKNOWN_COMMAND_MESSAGE;
            }
            else {
                string currentCommandName = currentCommand["name"].ToString();

                if (currentCommandName == defaultCommand) {
                    result["MESSAGE"] = currentCommand["defaultResponse"].ToString();
                    int i = 0;

                    foreach (object commandObject in commandList) {
                        IDictionary<string, JToken> command = (IDictionary<string, JToken>)commandObject;
                        string key = string.Format("ATTACH[{0}][MESSAGE]", i);
                        result[key] = string.Format("Command [put={0}]{0}[/put]: {1}", command["name"], command["description"]);
                        i++;
                    }
                }
                else {
                    JToken action = null;

                    if (currentCommand.TryGetValue("action", out action) && action != null && !string.IsNullOrEmpty(action.ToString())) {
                        string actionUrl = string.Format("{0}?action={1}", actionBaseUrl, action);

                        try {
                            JToken args = null;
                            string[] commandArgs = message.Trim().Replace(currentCommandName, "").Trim().Split(' ');
                            if (currentCommand.TryGetValue("args", out args) && args.ToList<JToken>() != null && commandArgs != null) {
                                List<JToken> argsList = args.ToList<JToken>();
                                if (commandArgs.Count() == argsList.Count) {
                                    foreach (JToken arg in argsList) {
                                        actionUrl += string.Format("&{0}={1}", arg, commandArgs[argsList.IndexOf(arg)]);
                                    }
                                }
                            }
                            string clientId = configuration.GetSection("BotSettings:client_id").Value;
                            string clientSecret = configuration.GetSection("BotSettings:client_secret").Value;
                            Dictionary<string, string> headers = new Dictionary<string, string> { { "userId", userId },
                                                                                                  { "client_id", clientId },
                                                                                                  { "client_secret", clientSecret } };

                            HttpResponseMessage response = ProcessRequest(actionUrl, "GET", null, headers);
                            string res = response.Content.ReadAsStringAsync().Result;
                            IList<object> resultDict = JsonConvert.DeserializeObject(res) as IList<object>;
                            string resultMessage = "";

                            foreach (object resultObj in resultDict) {
                                IDictionary<string, object> resultObjDict = (IDictionary<string, object>)resultObj;
                                JToken resultItems = null;
                                if (currentCommand.TryGetValue("resultItems", out resultItems) && resultItems != null) {
                                    foreach (string resultItem in (IEnumerable<object>)resultItems) {
                                        resultMessage += string.Format("{0}  ", resultObjDict[resultItem]);
                                    }
                                }
                                resultMessage += "\r\n";
                            }

                            result["MESSAGE"] = resultMessage;

                            if (string.IsNullOrEmpty(resultMessage)) {
                                result["MESSAGE"] = COMMAND_ERROR_MESSAGE;
                            }
                        }
                        catch {
                            result["MESSAGE"] = COMMAND_ERROR_MESSAGE;
                        }
                    }
                    else {
                        result["MESSAGE"] = currentCommand["defaultResponse"].ToString();
                    }
                }
            }

            return result;
        }

        private JObject DeserializeJsonFile(string fileName) {
            string path = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("App_Data\\{0}", fileName));
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject(json) as JObject;
        }

        public object Process(HttpRequest request, IFormCollection messageData) {
            isTestMode = messageData.ContainsKey("isTestMode") && bool.Parse(messageData["isTestMode"].ToString());
            commandListConfig = DeserializeJsonFile("commandList.json");
            bitrixBaseUrl = configuration.GetSection("BotSettings:bitrixBaseUrl").Value;
            accessToken = messageData["auth[access_token]"];

            string url = configuration.GetSection("BotSettings:chatBotUrl").Value;
            string ev = messageData["event"];
            switch (ev) {
                case BotEvents.OnMessageAdd:
                    object result = ProcessMessageAdd(messageData);
                    if (isTestMode) {
                        return result;
                    }
                    break;
                case BotEvents.OnInstall: return ProcessMessageInstall(url);
            }
            return null;
        }

        public void WriteToLog(string message) {
            string logPath = Path.Combine(hostingEnvironment.ContentRootPath, "App_Data\\errorLog.log");
            StreamReader reader = new StreamReader(logPath);
            string content = reader.ReadToEnd();
            reader.Close();
            FileStream logFile = null;

            if (System.IO.File.Exists(logPath)) {
                logFile = System.IO.File.OpenWrite(logPath);
            }
            else {
                logFile = System.IO.File.Create(logPath);
            }

            StreamWriter writer = new StreamWriter(logFile);

            writer.Write(content);
            writer.WriteLine(DateTime.Now.ToString());
            writer.WriteLine(message);
            writer.Close();
            logFile.Close();
        }
    }
}