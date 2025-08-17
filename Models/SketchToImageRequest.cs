public class SketchToImageRequest
{
    public string? Description { get; set; } // Optional text description
    public string Base64Image { get; set; }  // Base64 encoded image
    public string? MimeType { get; set; }    // Optional MIME type (e.g., "image/jpeg")
}