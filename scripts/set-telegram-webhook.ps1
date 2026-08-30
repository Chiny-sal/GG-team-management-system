param(
    [Parameter(Mandatory = $false)]
    [string]$BotToken = $env:TELEGRAM_BOT_TOKEN,
    [Parameter(Mandatory = $false)]
    [string]$PublicApiUrl = $env:PUBLIC_API_URL,
    [Parameter(Mandatory = $false)]
    [string]$SecretToken = $env:TELEGRAM_WEBHOOK_SECRET
)

if ([string]::IsNullOrWhiteSpace($BotToken) -or $BotToken.Contains("<<")) {
    throw "TELEGRAM_BOT_TOKEN is required."
}
if ([string]::IsNullOrWhiteSpace($PublicApiUrl) -or $PublicApiUrl.Contains("<<")) {
    throw "PUBLIC_API_URL is required."
}

$webhookUrl = "$($PublicApiUrl.TrimEnd('/'))/api/telegram/webhook"
$body = @{ url = $webhookUrl }
if (-not [string]::IsNullOrWhiteSpace($SecretToken)) {
    $body.secret_token = $SecretToken
}

$api = "https://api.telegram.org/bot$BotToken/setWebhook"
Write-Host "Registering webhook $webhookUrl"
Invoke-RestMethod -Method Post -Uri $api -Body $body
