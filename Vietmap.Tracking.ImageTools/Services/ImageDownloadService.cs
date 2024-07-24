using AutoMapper;
using Newtonsoft.Json;
using RestSharp;
using System.IO.Compression;
using Vietmap.Common;
using Vietmap.Tracking.Core;
using Vietmap.Tracking.DataModels;
using Vietmap.Tracking.ImageTools.Dtos;
using Vietmap.Tracking.ImageTools.Models;
using Vietmap.Tracking.Requests;

namespace Vietmap.Tracking.ImageTools.Services
{
    /// <summary>
    /// Xử lý hình ảnh
    /// </summary>
    public class ImageDownloadService : IImageDownloadService
    {
        private readonly IConfigurationRoot _configuration;
        private readonly ILogger<ImageDownloadService> _logger;
        public readonly ProcessStatusManager _processStatusManager;
        private readonly IMapper _autoMapper;
        private readonly TimeSpan _fileLifetime = TimeSpan.FromMinutes(30);

        public ImageDownloadService(IConfigurationRoot configuration, ILogger<ImageDownloadService> logger, ProcessStatusManager processStatusManager, IMapper autoMapper)
        {
            _configuration = configuration;
            _logger = logger;
            _processStatusManager = processStatusManager;
            _autoMapper = autoMapper;
        }

        /// <summary>
        /// Get image waypoints from Waypoint Service  
        /// </summary>
        /// <param name="requestBody"></param>
        /// <returns></returns>
        private async Task<IEnumerable<ImageInfo>> GetImageWaypoints(GetImagesRequestDto requestBody)
        {
            var baseUrl = _configuration.GetSection("RemoteServices:WaypointService:BaseUrl").Value;
            var client = new RestClient(baseUrl + InternalApiUrl.GET_IMAGE_WAYPOINTS);

            var request = new RestRequest()
            {
                Method = Method.Post
            };

            request.AddHeader("Content-Type", "application/json");

            request.AddJsonBody(requestBody);

            try
            {
                var response = await client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    var images = JsonConvert.DeserializeObject<IEnumerable<ImageWaypointOmitted>>(response.Content!);
                    if (images is null)
                    {
                        return Enumerable.Empty<ImageInfo>();
                    }

                    return _autoMapper.Map<IEnumerable<ImageInfo>>(images);
                }
                else
                {
                    _logger.LogError("ImageService - GetImageWaypoints - UnSuccess: {Message}", response.ErrorMessage);
                    return Enumerable.Empty<ImageInfo>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImageService - GetImageWaypoints - Error: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Download image from url
        /// </summary>
        /// <param name="image"></param>
        /// <param name="storageLocation"></param>
        /// <returns></returns>
        private async Task DownloadImageAsync(ImageInfo image, string storageLocation)
        {
            try
            {
                using RestClient httpClient = new(image.Url);
                var request = new RestRequest();
                var response = await httpClient.ExecuteAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string fileName = string.Concat("VEHICLE", "-", image.VehicleId, "-", "CAM", "-", image.Cam, "-", image.Time, ".jpg");
                    var filePath = Path.Combine(storageLocation, fileName);
                    using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
                    await fileStream.WriteAsync(response.RawBytes);
                }
                else
                {
                    _logger.LogWarning("Download Image Error: {Image}", image.Url);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImageService - DownloadImageAsync - Error: {Message}", ex.Message);
            }
        }

        private async Task DownloadAndZipImages(IEnumerable<ImageInfo> imageModels, string token)
        {
            try
            {
                _processStatusManager.UpdateProcessStatus(token, ProcessStatusManager.PROCESSING);
                string baseDirectory = Directory.GetCurrentDirectory() + "\\temps";
                //string parentDirectory = DateTime.Now.ToString("yyyy-MM-dd");
                //string childrenDirectory = imageModels.First().VehicleId.ToString() ?? "Unknown";
                string tempDirectory = Path.Combine(baseDirectory, token);
                if (Directory.Exists(tempDirectory))
                {
                    _processStatusManager.UpdateProcessStatus(token, ProcessStatusManager.COMPLETED);
                    return;
                }
                Directory.CreateDirectory(tempDirectory);

                // Download img.
                await Task.WhenAll(imageModels.Select(async model => await DownloadImageAsync(model, tempDirectory)));
                // Create zip file.
                await CreateZipFile(tempDirectory, token);
                // Clear temp file.
                ClearTempFileDownloaded(tempDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImageService - DownloadAndZipImages - Error: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Create zip file
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task CreateZipFile(string sourceDirectory, string token)
        {
            try
            {
                string contentDirectiory = Path.Combine(Directory.GetCurrentDirectory(), "contents");
                if (!Directory.Exists(contentDirectiory))
                {
                    Directory.CreateDirectory(contentDirectiory);
                }
                string zipOutputFilePath = Path.Combine(contentDirectiory, token) + ".zip";

                if (File.Exists(zipOutputFilePath))
                {
                    _processStatusManager.UpdateProcessStatus(token, ProcessStatusManager.COMPLETED);
                }
                else
                {

                    ZipFile.CreateFromDirectory(sourceDirectory, zipOutputFilePath, CompressionLevel.Fastest, false);
                    _processStatusManager.UpdateProcessStatus(token, ProcessStatusManager.COMPLETED);
                }

                await Task.CompletedTask;
            }
            catch (IOException iox)
            {
                _logger.LogError(iox, "ImageService - CreateZipFile - IOException - Error: {Message}", iox.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImageService - CreateZipFile - Exception - Error: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Check token
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DownloadImagesResponse CheckToken(DownloadImagesRequest request)
        {
            int fromDate = ConvertUtil.ConvertDateTimeToInt32(request.FromDate);
            int toDate = ConvertUtil.ConvertDateTimeToInt32(request.ToDate);
            // Parse token from request.
            if (!string.IsNullOrEmpty(request.Token))
            {
                var result = _processStatusManager.GetImageProcess(request.Token) ?? throw new ArgumentException(ExceptionCodes.INVALID_TOKEN);
                return result;
            }

            var token = string.Concat(request.CompanyId, "_", request.VehicleId, "_", fromDate, "_", toDate, "_", DateTime.UtcNow.Ticks);
            var tokenResponse = new DownloadImagesResponse { Token = token, Status = ProcessStatusManager.INIT };
            _processStatusManager.AddImageProcess(tokenResponse);
            request.Token = token;
            try
            {
                Task.Run(async () => await ProcessDownloadImages(request));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImageService - CheckToken - Error: {Message}", ex.Message);
                throw;
            }
            return tokenResponse;
        }

        public async Task ProcessDownloadImages(DownloadImagesRequest request)
        {
            try
            {
                int fromDate = ConvertUtil.ConvertDateTimeToInt32(request.FromDate);
                int toDate = ConvertUtil.ConvertDateTimeToInt32(request.ToDate);
                if (toDate - fromDate >= ConvertUtil.DaySeconds * 7)
                {
                    throw new Exception(ExceptionCodes.OUT_OF_RANGE);
                }

                var images = await GetImageWaypoints(new GetImagesRequestDto
                {
                    FromTime = fromDate,
                    ToTime = toDate,
                    Id = request.VehicleId,
                    WithData = false,

                });

                if (images is null || !images.Any())
                {
                    throw new Exception(ExceptionCodes.IMAGES_IS_NOT_FOUND);
                }

                await DownloadAndZipImages(images, token: request.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImageService - DownloadImages - Error: {Message}", ex.Message);
                throw;
            }
        }

        private void ClearTempFileDownloaded(string path) => Directory.Delete(path, true);

        public void CleanUpTempFiles()
        {
            string contentsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "contents");
            if (Directory.Exists(contentsDirectory))
            {
                var files = Directory.GetFiles(contentsDirectory);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (DateTime.Now - fileInfo.CreationTime > _fileLifetime)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deleting file: {file}", file);
                        }
                    }
                }
            }
        }
    }
}
