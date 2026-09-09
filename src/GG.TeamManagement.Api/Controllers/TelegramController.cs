using GG.TeamManagement.Application.Telegram;
using GG.TeamManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/telegram")]
public class TelegramController : ControllerBase
{
    private readonly TelegramWebhookService _telegram;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        TelegramWebhookService telegram,
        IConfiguration configuration,
        ILogger<TelegramController> logger)
    {
        _telegram = telegram;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] Update update, CancellationToken cancellationToken)
    {
        var expectedSecret = AppEnvironment.Optional(_configuration, AppEnvironment.TelegramWebhookSecret);
        if (!string.IsNullOrWhiteSpace(expectedSecret))
        {
            var provided = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
            if (!string.Equals(provided, expectedSecret, StringComparison.Ordinal))
                return Unauthorized();
        }

        if (update.Type != UpdateType.Message || update.Message?.Text is null || update.Message.From is null)
            return Ok();

        var from = update.Message.From;
        var name = string.Join(' ', new[] { from.FirstName, from.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(name))
            name = from.Username ?? from.Id.ToString();

        try
        {
            await _telegram.HandleTextMessageAsync(from.Id, from.Username, name, update.Message.Text, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle Telegram update {UpdateId}", update.Id);
        }

        return Ok();
    }
}
