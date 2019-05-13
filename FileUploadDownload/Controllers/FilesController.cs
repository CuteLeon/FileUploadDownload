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

namespace FileUploadDownload.Controllers
{
    /// <summary>
    /// 文件控制器
    /// </summary>
    public class FilesController : Controller
    {
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
                Console.WriteLine($"上传文件目录：{path}");

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
            Console.WriteLine($"缩略文件目录：{path}");

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
            return this.BadRequest($"\"拒收了文件：{string.Join("、", files.Select(file => file.FileName))}\"");
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
            var directoryPath = UploadFilsDirectory.Value;
            var filePath = Path.Combine(directoryPath, file.FileName);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                Console.WriteLine($"接收文件：{filePath}");
                using Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

                await file.CopyToAsync(stream);
                stream.Flush();

                this.SaveThumbnail(stream, file.FileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接收文件失败：{file.FileName} ({file.Length} 字节), 耗时: {stopwatch.Elapsed.ToString()}\n{ex.ToString()}");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Console.WriteLine($"接收文件结束：{file.FileName} ({file.Length} 字节), 耗时: {stopwatch.Elapsed.ToString()}");
            }
        }

        /// <summary>
        /// 保存文件缩略图
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        protected void SaveThumbnail(Stream stream, string fileName)
        {
            try
            {
                string thumbnailPath = $"{Path.Combine(ThumbnailFileDirectory.Value, fileName)}.jpg";
                Console.WriteLine($"生成缩略图：{thumbnailPath}");

                using var image = Image.FromStream(stream);
                if (image != null)
                {
                    using var thumbnail = image.GetThumbnailImage(100, 100, null, IntPtr.Zero);
                    thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
                }
            }
            catch (Exception imageEx)
            {
                Console.WriteLine($"生成缩略图失败：{imageEx.Message}");
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
            var directoryPath = UploadFilsDirectory.Value;
            if (!Directory.Exists(directoryPath))
            {
                return this.NotFound(directoryPath);
            }

            this.ViewData.Add("UploadFilsDirectory", directoryPath);
            var files = await Task.Factory.StartNew(() => new DirectoryInfo(UploadFilsDirectory.Value)?.GetFiles());
            files = files.OrderBy(file => file.CreationTime).ThenBy(file => file.Name).ToArray();
            return this.View(files);
        }
        #endregion

        #region 下载文件

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            var result = await Task.Factory.StartNew<IActionResult>(() =>
            {
                var path = Path.Combine(UploadFilsDirectory.Value, fileName);
                if (!System.IO.File.Exists(path))
                {
                    return this.NotFound(path);
                }

                return this.File(
                    new FileStream(path, FileMode.Open, FileAccess.Read),
                    "application/octet-stream",
                    Path.GetFileName(path));
            });

            return result;
        }
        #endregion
    }
}