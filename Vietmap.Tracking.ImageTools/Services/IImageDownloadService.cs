using Vietmap.Tracking.DataModels;
using Vietmap.Tracking.Requests;

namespace Vietmap.Tracking.ImageTools.Services
{
    public interface IImageDownloadService
    {
        DownloadImagesResponse CheckToken(DownloadImagesRequest request);
    }
}
