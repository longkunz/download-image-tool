namespace Vietmap.Tracking.ImageTools.Models
{
    public class ImageInfo
    {
        /// <summary>
        /// Gets or sets the vehicle identifier.
        /// </summary>
        public int VehicleId { get; set; }

        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        public string Time { get; set; } = string.Empty;

        public int Cam { get; set; }
    }
}
