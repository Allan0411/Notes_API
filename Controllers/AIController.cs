using Microsoft.AspNetCore.Mvc;
using NotesAPI.Services; // Add this at the top


[Route("api/")]
public class AIController : ControllerBase
{
    public class ImageToTextRequest
    {
        public string ImageUrl { get; set; }
        public string? CustomPrompt { get; set; }
    }

    public class RefineImageRequest
    {
        public string Description { get; set; }
        public string? CustomPrompt { get; set; }
    }

    public class SketchToRefinedRequest
    {
        public string ImageUrl { get; set; }
        public string? RefinementInstructions { get; set; }
    }



    private readonly GeminiService _geminiService;
    public AIController(GeminiService geminiService)
    {
        _geminiService = geminiService;
    }
    [HttpPost("summarize")]
    public async Task<IActionResult> SummarizeText([FromBody] SummarizeRequest body)
    {
        if (body == null || body.Text == null)
            return BadRequest(new { message = "Provide the text to summarize." });

        var prompt = $"Summarize the following content in 3-5 clear, concise sentences. Avoid generic phrases and focus on the main points:\n\n{body.Text}";
        var aiResponse = await _geminiService.GenerateContent(prompt);
        return Ok(new { aiResponse, text = body.Text });
    }

    [HttpPost("expand")]
    public async Task<IActionResult> ExpandText([FromBody] SummarizeRequest body)
    {
        if (body == null || body.Text == null)
            return BadRequest(new { message = "Provide the text to expand." });

        var prompt = $"Expand on the following content in deeply detailed, well-structured paragraphs. Provide precise, contextually relevant elaboration. Do not include generic introductions or endings, only the expanded content:\n\n{body.Text}";
        var aiResponse = await _geminiService.GenerateContent(prompt);
        return Ok(new { aiResponse, text = body.Text });
    }

    [HttpPost("shorten")]
    public async Task<IActionResult> ShortenText([FromBody] SummarizeRequest body)
    {
        if (body == null || body.Text == null)
            return BadRequest(new { message = "Provide the text to shorten." });

        var prompt = $"Rewrite the following content to be as concise as possible while preserving all key information. Use short, clear sentences only:\n\n{body.Text}";
        var aiResponse = await _geminiService.GenerateContent(prompt);
        return Ok(new { aiResponse, text = body.Text });
    }

    [HttpPost("fix_grammar")]
    public async Task<IActionResult> FixGrammar([FromBody] SummarizeRequest body)
    {
        if (body == null || body.Text == null)
            return BadRequest(new { message = "Provide the text for grammar correction." });

        var prompt = $"Carefully correct all grammar, punctuation, and spelling mistakes in the following text. Preserve original meaning. Only provide the corrected version:\n\n{body.Text}";
        var aiResponse = await _geminiService.GenerateContent(prompt);
        return Ok(new { aiResponse, text = body.Text });
    }

    [HttpPost("make_formal")]
    public async Task<IActionResult> MakeFormal([FromBody] SummarizeRequest body)
    {
        if (body == null || body.Text == null)
            return BadRequest(new { message = "Provide the text to formalize." });

        var prompt = $"Rewrite the following content in a professional, academic tone suitable for formal communication. Use complete sentences and formal vocabulary, but do not use generic headers or closings. Provide only the improved version:\n\n{body.Text}";
        var aiResponse = await _geminiService.GenerateContent(prompt);
        return Ok(new { aiResponse, text = body.Text });
    }

    [HttpPost("sketch-to-refined-image")]
    public async Task<IActionResult> SketchToRefinedImage([FromBody] SketchToRefinedRequest body)
    {
        if (body == null || string.IsNullOrEmpty(body.ImageUrl))
            return BadRequest(new { message = "Please provide an image URL." });

        var result = await _geminiService.RefineSketchToImage(body.ImageUrl, body.RefinementInstructions);

        if (result.Success)
        {
            return Ok(result);
        }
        else
        {
            return StatusCode(500, new { message = "Failed to refine image.", error = result.ErrorMessage });
        }
    }

    

    [HttpPost("image-to-text")]
    public async Task<IActionResult> ImageToText([FromBody] ImageToTextRequest body)
    {
        if (body == null || string.IsNullOrEmpty(body.ImageUrl))
            return BadRequest(new { message = "Please provide an image URL." });

        try
        {
            // Validate URL format
            if (!Uri.TryCreate(body.ImageUrl, UriKind.Absolute, out _))
                return BadRequest(new { message = "Please provide a valid image URL." });

            // Create prompt for image description
            var prompt = string.IsNullOrEmpty(body.CustomPrompt)
                ? "Describe this image in detail. What do you see? Include objects, people, colors, setting, and any text visible in the image."
                : body.CustomPrompt;

            // Download image and convert to base64 for Gemini service
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var imageBytes = await httpClient.GetByteArrayAsync(body.ImageUrl);
            var base64Image = Convert.ToBase64String(imageBytes);

            // Determine MIME type from URL extension
            var mimeType = GetMimeTypeFromUrl(body.ImageUrl);

            var aiResponse = await _geminiService.GenerateContent(prompt, base64Image, mimeType);
            return Ok(new { description = aiResponse, imageUrl = body.ImageUrl });
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new { message = "Failed to download image from URL.", error = ex.Message });
        }
        catch (TaskCanceledException)
        {
            return BadRequest(new { message = "Image download timed out. Please check the URL." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to process image.", error = ex.Message });
        }
    }

    // Helper method to determine MIME type from URL
    private string GetMimeTypeFromUrl(string url)
    {
        try
        {
            var extension = Path.GetExtension(url.Split('?')[0]).ToLower();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/png" // default fallback
            };
        }
        catch
        {
            return "image/png";
        }
    }



}



