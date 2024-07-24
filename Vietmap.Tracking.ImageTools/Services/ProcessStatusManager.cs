using System.Collections.Concurrent;
using Vietmap.Tracking.DataModels;

namespace Vietmap.Tracking.ImageTools.Services
{
    public class ProcessStatusManager
    {
        public const int INIT = 0;
        public const int PROCESSING = 1;
        public const int COMPLETED = 2;
        public ConcurrentDictionary<string, DownloadImagesResponse> ProcessingImages { get; } = new();

        public void UpdateProcessStatus(string token, int status)
        {
            if (ProcessingImages.TryGetValue(token, out var imageProcess))
            {
                imageProcess.Status = status;
                return;
            }
            throw new ArgumentException(ExceptionCodes.INVALID_TOKEN);
        }

        public DownloadImagesResponse? GetImageProcess(string token)
        {
            if (ProcessingImages.TryGetValue(token, out var imageProcess))
            {
                return imageProcess;
            }
            return null;
        }

        public void AddImageProcess(DownloadImagesResponse imageProcess)
        {
            if (string.IsNullOrEmpty(imageProcess.Token))
            {
                throw new ArgumentException(ExceptionCodes.INVALID_TOKEN);
            }
            ProcessingImages.TryAdd(imageProcess.Token, imageProcess);
        }
    }
}
