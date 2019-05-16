using System;
using System.Diagnostics;
using FileUploadDownload.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FileUploadDownload.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> logger;

        public HomeController(ILogger<HomeController> logger)
        {
            this.logger = logger;
        }

        public IActionResult Index()
        {
            this.logger.LogInformation($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 访问上传视图");

            return this.View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
