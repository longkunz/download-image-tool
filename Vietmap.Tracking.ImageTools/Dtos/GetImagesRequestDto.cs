namespace Vietmap.Tracking.ImageTools.Dtos
{
    public sealed record GetImagesRequestDto
    {
        public int Id { get; set; }

        public int FromTime { get; set; }

        public int ToTime { get; set; }

        public bool WithData { get; set; }

        public int Height { get; set; }

        public bool IsHttps { get; set; } = false;
    }
}
