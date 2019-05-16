using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FileUploadDownload
{
    public class ConnectMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<ConnectMiddleware> logger;

        public ConnectMiddleware(
            RequestDelegate next,
            ILogger<ConnectMiddleware> logger)
        {
            this.next = next;
            this.logger = logger;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            await this.next(httpContext);
        }
    }
}
