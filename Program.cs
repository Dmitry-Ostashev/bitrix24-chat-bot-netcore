﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChatBotNetCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
     .SetBasePath(Directory.GetCurrentDirectory())
     .AddCommandLine(args)
     .AddEnvironmentVariables(prefix: "ASPNETCORE_")
     .AddJsonFile("hosting.json", optional: true)
     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
     .Build();

            var host = new WebHostBuilder()
                .UseConfiguration(config)
                .UseKestrel()
                //.UseKestrel(options => {
                    //options.Listen(IPAddress.Loopback, 5000);  // http:localhost:5000
                    //options.Listen(IPAddress.Any, 80);         // http:*:80
                    //options.Listen(IPAddress.Any, 5001, listenOptions => {
                    //    listenOptions.UseHttps("C:\\ProgramData\\win-acme\\httpsacme-v01.api.letsencrypt.org\\api.maxis-it.ru-all.pfx", "");
                    //});
                //})
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
