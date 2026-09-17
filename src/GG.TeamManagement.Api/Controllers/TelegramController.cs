using GG.TeamManagement.Application.Telegram;
using GG.TeamManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
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
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        var expectedSecret = AppEnvironment.Optional(_configuration, AppEnvironment.TelegramWebhookSecret);
        if (!string.IsNullOrWhiteSpace(expectedSecret))
        {
            var provided = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
            if (!string.Equals(provided, expectedSecret, StringComparison.Ordinal))
            {
                _logger.LogWarning("Telegram webhook rejected: secret token mismatch.");
                return Unauthorized();
            }
        }

        Update? update;
        try
        {
            update = await System.Text.Json.JsonSerializer.DeserializeAsync<Update>(
                Request.Body,
                JsonBotAPI.Options,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram webhook JSON could not be parsed. Incoming updates will be dropped until this is fixed.");
            return Ok();
        }

        if (update is null)
        {
            _logger.LogWarning("Telegram webhook body deserialized to null.");
            return Ok();
        }

        _logger.LogInformation(
            "Telegram webhook received update {UpdateId} type={UpdateType}.",
            update.Id,
            update.Type);

        if (update.Type != UpdateType.Message || update.Message?.Text is null || update.Message.From is null)
        {
            _logger.LogInformation(
                "Telegram update {UpdateId} ignored: not a text message (type={UpdateType}).",
                update.Id,
                update.Type);
            return Ok();
        }

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
            _logger.LogError(ex, "Failed to handle Telegram update {UpdateId} from {TelegramUserId}", update.Id, from.Id);
        }

        return Ok();
    }
}
