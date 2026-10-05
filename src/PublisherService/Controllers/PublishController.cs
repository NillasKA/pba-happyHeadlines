using HappyHeadlines.PublisherService.Services;
using HappyHeadlines.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HappyHeadlines.PublisherService.Controllers;

[ApiController]
[Route("publish")]
public sealed class PublishController(IPublisherService publisherService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Publish([FromBody] PublishArticleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var article = await publisherService.PublishAsync(request, cancellationToken);
            return Accepted($"/articles/{article.Id}", new { article.Id, article.Region, status = "queued" });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
