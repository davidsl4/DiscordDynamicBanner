using SqlKata;

namespace DynamicBanner.Models
{
    public class GuildProps
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        [Key("ID")]
        public ulong Id { get; set; }
        public bool Status { get; set; }
        public string BaseImageUrl { get; set; }
        [Column("EndpointURL")]
        public string EndpointUrl { get; set; }

        public string EndpointPathSelector { get; set; }
        public string FontName { get; set; }
        public GoogleFont.FontVariants FontStyle { get; set; }
        public float FontSize { get; set; }
        public uint FontColor { get; set; }
        public ushort FontRotationRadius { get; set; }
        public FontAlignment FontAlignment { get; set; }
    }
}