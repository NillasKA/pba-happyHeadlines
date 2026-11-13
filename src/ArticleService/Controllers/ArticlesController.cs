using HappyHeadlines.ArticleService.Services;
using HappyHeadlines.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HappyHeadlines.ArticleService.Controllers;

[ApiController]
[Route("articles")]
public sealed class ArticlesController(IArticleService articleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ArticleRecord>>> GetRecent([FromQuery] int? limit, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        var region = Request.Headers["X-Region"].FirstOrDefault() ?? Request.Query["region"].FirstOrDefault();
        if (limit is < 1 or > 100 || offset < 0) return BadRequest("limit must be 1-100 and offset must be non-negative");
        return Ok(await articleService.GetRecentAsync(region, cancellationToken, limit, offset));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ArticleRecord>> Get(Guid id, CancellationToken cancellationToken)
    {
        var region = Request.Headers["X-Region"].FirstOrDefault() ?? Request.Query["region"].FirstOrDefault();
        var article = await articleService.GetAsync(id, region, cancellationToken);
        return article is null ? NotFound() : Ok(article);
    }
}
