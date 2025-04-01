using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO
{
    public class AttachmentInfo
    {
        [Required(ErrorMessage = "Attachment ID is required")]
        public string Id { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Attachment name is required")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Web view link is required")]
        public string WebViewLink { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Web content link is required")]
        public string WebContentLink { get; set; } = string.Empty;
    }
}
