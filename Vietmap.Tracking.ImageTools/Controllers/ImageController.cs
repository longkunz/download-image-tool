using Microsoft.AspNetCore.Mvc;
using Vietmap.Tracking.DataModels;
using Vietmap.Tracking.ImageTools.Services;
using Vietmap.Tracking.Requests;

namespace Vietmap.Tracking.ImageTools.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class ImageController : ControllerBase
    {
        private readonly IImageDownloadService _imageService;

        public ImageController(IImageDownloadService imageService)
        {
            _imageService = imageService;
        }

        [HttpPost]
        public DownloadImagesResponse DownloadImages([FromBody] DownloadImagesRequest request) => _imageService.CheckToken(request);
    }
}
