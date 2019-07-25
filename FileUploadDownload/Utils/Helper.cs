namespace FileUploadDownload.Utils
{
    public static class Helper
    {
        /// <summary>
        /// 格式化文件大小
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string FormatFileSize(long length) 
            => length switch
                {
                    _ when length < 1024 => $"{length} B",
                    _ when length < 1048576 => $"{length >> 10} KB+",
                    _ when length < 1073741824 => $"{length >> 20} MB+",
                    _ when length < 1099511627776 => $"{length >> 30} GB+",
                    _ => $"{length >> 40} TB+",
                };
    }
}
