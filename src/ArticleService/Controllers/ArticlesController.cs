using HappyHeadlines.ArticleService.Services;
using HappyHeadlines.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HappyHeadlines.ArticleService.Controllers;

[ApiController]
[Route("articles")]
public sealed class ArticlesController(IArticleService articleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ArticleRecord>>> GetRecent(CancellationToken cancellationToken)
    {
        var region = Request.Headers["X-Region"].FirstOrDefault() ?? Request.Query["region"].FirstOrDefault();
        return Ok(await articleService.GetRecentAsync(region, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ArticleRecord>> Get(Guid id, CancellationToken cancellationToken)
    {
        var region = Request.Headers["X-Region"].FirstOrDefault() ?? Request.Query["region"].FirstOrDefault();
        var article = await articleService.GetAsync(id, region, cancellationToken);
        return article is null ? NotFound() : Ok(article);
    }
}
