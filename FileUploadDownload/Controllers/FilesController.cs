using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FileUploadDownload.Controllers
{
    /// <summary>
    /// 文件控制器
    /// </summary>
    public class FilesController : Controller
    {
        public FilesController(ILogger<FilesController> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// 上传文件目录名称
        /// </summary>
        private const string UploadFilsDirectoryName = "UploadFiles";

        /// <summary>
        /// 缩略文件目录名称
        /// </summary>
        private const string ThumbnailFileDirectoryName = "ThumbnailFiles";

        /// <summary>
        /// 上传文件目录
        /// </summary>
        private readonly static Lazy<string> UploadFilsDirectory = new Lazy<string>(() =>
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UploadFilsDirectoryName);
                Console.WriteLine($"初始化上传文件目录：{path}");

                if (!Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"创建上传文件目录失败：{ex.Message}");
                    }
                }

                return path;
            }, true);

        /// <summary>
        /// 缩略文件目录
        /// </summary>
        private readonly static Lazy<string> ThumbnailFileDirectory = new Lazy<string>(() =>
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThumbnailFileDirectoryName);
            Console.WriteLine($"初始化缩略文件目录：{path}");

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建缩略文件目录失败：{ex.Message}");
                }
            }

            return path;
        }, true);

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<FilesController> logger;

        #region 上传文件

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="uploadFiles"></param>
        /// <returns></returns>
        /// <remarks>使用 Input(File 和 Submit) 上传多个文件时，会一次请求上传所有文件</remarks>
        public async Task<IActionResult> UploadFileInput(List<IFormFile> uploadFiles)
        {
            foreach (var file in uploadFiles)
            {
                try
                {
                    await this.ReceiveFileAsync(file);
                }
                catch
                {
                }
            }

            return this.RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <returns></returns>
        /// <remarks>使用 Bootstrap-FileInput 上传多个文件时，会分成多次请求，每次请求上传一个文件</remarks>
        public async Task<IActionResult> UploadFileBootstrap()
        {
            var files = this.HttpContext.Request.Form.Files;

            foreach (var file in files)
            {
                try
                {
                    await this.ReceiveFileAsync(file);
                }
                catch
                {
                }
            }

            // 成功接收可以直接返回 Json
            return this.Json($"成功接收 {files.Count} 个文件：{string.Join("、", files.Select(file => file.FileName))}");

            // 拒收也要最消息转义为 Json (外加双引号)
            // return this.BadRequest($"\"拒收了文件：{string.Join("、", files.Select(file => file.FileName))}\"");
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <returns></returns>
        /// <remarks>使用 Ajax 上传多个文件时，可能会因为请求数据过大而出错</remarks>
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadFileAjax()
        {
            IFormFileCollection files;

            try
            {
                files = this.HttpContext.Request.Form.Files;
            }
            catch (Exception ex)
            {
                return this.BadRequest(ex.Message);
            }

            foreach (var file in files)
            {
                try
                {
                    await this.ReceiveFileAsync(file);
                }
                catch
                {
                }
            }

            return this.Ok(new { Message = $"收到 {files.Count} 个文件，共 {files.Sum(file => file.Length)} 字节" });
        }

        /// <summary>
        /// 接收文件
        /// </summary>
        /// <param name="file"></param>
        protected async Task ReceiveFileAsync(IFormFile file)
        {
            this.logger.LogInformation($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 开始上传文件 {file.FileName}");

            var filePath = this.GetFilePath(file.FileName);
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                using Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

                await file.CopyToAsync(stream);
                stream.Flush();

                this.SaveThumbnail(stream, file.FileName);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 上传文件失败 {file.FileName} ({file.Length} 字节), 耗时: {stopwatch.Elapsed.ToString()}");

                throw;
            }
            finally
            {
                stopwatch.Stop();
                this.logger.LogInformation($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 接收文件结束 {file.FileName} ({file.Length} 字节), 耗时: {stopwatch.Elapsed.ToString()}");
            }
        }

        /// <summary>
        /// 保存文件缩略图
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        protected void SaveThumbnail(Stream stream, string fileName)
        {
            this.logger.LogInformation($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 创建缩略文件 {fileName}");

            try
            {
                string thumbnailPath = this.GetThumnailPath(fileName);

                using var image = Image.FromStream(stream);
                if (image != null)
                {
                    var size = this.GetThumnailSize(image.Size);
                    using var thumbnail = image.GetThumbnailImage(size.Width, size.Height, null, IntPtr.Zero);
                    thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
                }
            }
            catch (Exception thumbnailEx)
            {
                this.logger.LogError(thumbnailEx, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 创建缩略图失败 {fileName}");
            }
        }

        /// <summary>
        /// 获取缩略图尺寸
        /// </summary>
        /// <param name="originalSize"></param>
        /// <returns></returns>
        protected Size GetThumnailSize(Size originalSize)
        {
            const int maxSide = 200;
            if (originalSize.Width == originalSize.Height)
            {
                return new Size(maxSide, maxSide);
            }
            else if (originalSize.Width > originalSize.Height)
            {
                return new Size(maxSide, (int)(maxSide * ((double)originalSize.Height / originalSize.Width)));
            }
            else
            {
                return new Size((int)(maxSide * ((double)originalSize.Width / originalSize.Height)), maxSide);
            }
        }
        #endregion

        #region 主页

        /// <summary>
        /// 主页
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Index()
        {
            this.logger.LogInformation($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 访问文件视图");

            var directoryPath = UploadFilsDirectory.Value;
            this.ViewData.Add("UploadFilsDirectory", directoryPath);

            var files = Enumerable.Empty<FileInfo>();
            if (Directory.Exists(directoryPath))
            {
                files = await Task.Factory.StartNew(() =>
                    new DirectoryInfo(UploadFilsDirectory.Value)?
                    .GetFiles()
                    .OrderBy(file => file.CreationTime)
                    .ThenBy(file => file.Name));
            }

            return this.View(files.ToArray());
        }

        /// <summary>
        /// 获取文件路径
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public string GetFilePath(string fileName)
            => Path.Combine(UploadFilsDirectory.Value, fileName);

        /// <summary>
        /// 获取缩略文件路径
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public string GetThumnailPath(string fileName)
            => $"{Path.Combine(ThumbnailFileDirectory.Value, fileName)}.jpg";
        #endregion

        #region 下载文件

        /// <summary>
        /// 缩略文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<IActionResult> ThumbnailFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return this.File("~/images/unknown.png", "image/png");
            }

            var result = await Task.Factory.StartNew<IActionResult>(() =>
             {
                 var path = this.GetThumnailPath(fileName);
                 if (!System.IO.File.Exists(path))
                 {
                     this.logger.LogWarning($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 文件 {fileName} 不存在缩略文件");

                     return this.File("~/images/unknown.png", "image/png");
                 }

                 return this.File(new FileStream(path, FileMode.Open, FileAccess.Read), "image/jpeg");
             });

            return result;
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            this.logger.LogInformation($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 请求下载文件 {fileName}");

            if (string.IsNullOrEmpty(fileName))
            {
                return this.File("~/images/unknown.png", "application/octet-stream", "unknown.png");
            }

            var result = await Task.Factory.StartNew<IActionResult>(() =>
            {
                var path = this.GetFilePath(fileName);
                if (!System.IO.File.Exists(path))
                {
                    this.logger.LogWarning($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 文件 {fileName} 不存在，无法下载");

                    return this.File("~/images/unknown.png", "application/octet-stream", "unknown.png");
                }

                return this.File(
                    new FileStream(path, FileMode.Open, FileAccess.Read),
                    "application/octet-stream",
                    Path.GetFileName(path));
            });

            return result;
        }
        #endregion

        #region 删除文件

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteFile(string fileName)
            => await Task.Factory.StartNew<IActionResult>(() =>
                 {
                     this.logger.LogWarning($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 请求删除文件 {fileName}");

                     if (string.IsNullOrEmpty(fileName))
                     {
                         return this.BadRequest("无法删除空的文件名称");
                     }

                     try
                     {
                         System.IO.File.Delete(this.GetThumnailPath(fileName));
                         System.IO.File.Delete(this.GetFilePath(fileName));

                         return this.Ok($"删除 {fileName} 成功");
                     }
                     catch (Exception ex)
                     {
                         this.logger.LogWarning(ex, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} [{this.HttpContext.Connection.Id}] {this.HttpContext.Connection.RemoteIpAddress} 删除文件失败 {fileName}");

                         return this.BadRequest(ex.Message);
                     }
                 });
        #endregion
    }
}