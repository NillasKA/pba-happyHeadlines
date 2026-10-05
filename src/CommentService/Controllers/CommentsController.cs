using HappyHeadlines.CommentService.Services;
using HappyHeadlines.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HappyHeadlines.CommentService.Controllers;

[ApiController]
[Route("articles/{articleId:guid}/comments")]
public sealed class CommentsController(ICommentService commentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CommentRecord>>> Get(Guid articleId, CancellationToken cancellationToken)
    {
        var region = Request.Headers["X-Region"].FirstOrDefault() ?? Request.Query["region"].FirstOrDefault();
        return Ok(await commentService.GetForArticleAsync(articleId, region, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid articleId, [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var region = Request.Headers["X-Region"].FirstOrDefault() ?? Request.Query["region"].FirstOrDefault();
            var comment = await commentService.CreateAsync(articleId, request, region, cancellationToken);
            return Created($"/articles/{articleId}/comments", comment);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
