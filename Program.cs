using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var config = new Config(Environment.GetEnvironmentVariable("Token") ?? throw new Exception(), "696343127144923158", "/home/senne/Downloads/Sounds/");

if (string.IsNullOrWhiteSpace(config.BotToken) || string.IsNullOrWhiteSpace(config.GuildId)
    || !Directory.Exists(config.SoundsDirectory))
{
    throw new Exception("Error initializing");
}

var oggFiles = Directory.GetFiles(config.SoundsDirectory, "*.ogg", SearchOption.TopDirectoryOnly);

var randomFile = oggFiles[Random.Shared.Next(oggFiles.Length)];
var fileName = Path.GetFileNameWithoutExtension(randomFile);

var emoji = fileName.Split('-')[0];
var soundName = fileName.Split('-')[1];

var fileBytes  = await File.ReadAllBytesAsync(randomFile);
var fileSizeKb = fileBytes.Length / 1024.0;

if (fileBytes.Length > 512 * 1024)
{
    throw new Exception($"File is too large ({fileSizeKb:F1} KB). Discord'imit is 512 KB.");
}

var base64Data = Convert.ToBase64String(fileBytes);
var dataUri    = $"data:audio/ogg;base64,{base64Data}";

var payload = new CreateSoundRequest
{
    Name      = soundName,
    Sound     = dataUri,
    Volume    = 1.0,
    EmojiName = emoji
};

var json = JsonSerializer.Serialize(payload);

// Upload to Discord
using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", config.BotToken);
http.DefaultRequestHeaders.Add("User-Agent", "DiscordSoundboardUploader/1.0");

var url = $"https://discord.com/api/v10/guilds/{config.GuildId}/soundboard-sounds";
var content = new StringContent(json, Encoding.UTF8, "application/json");

HttpResponseMessage response;
try
{
    response = await http.PostAsync(url, content);
}
catch (HttpRequestException ex)
{
    Console.WriteLine(ex);
    return;
}

var responseBody = await response.Content.ReadAsStringAsync();

if (!response.IsSuccessStatusCode)
{
    // Pretty-print Discord's error body
    try
    {
        using var doc = JsonDocument.Parse(responseBody);
        Console.WriteLine(JsonSerializer.Serialize(
            doc.RootElement,
            new JsonSerializerOptions { WriteIndented = true }));
    }
    catch
    {
        Console.WriteLine(responseBody);
    }
}

internal record Config(string BotToken, string GuildId, string SoundsDirectory);

internal record CreateSoundRequest
{
    [JsonPropertyName("name")] 
    public string Name { get; init; } = "";

    [JsonPropertyName("sound")] 
    public string Sound { get; init; } = "";

    [JsonPropertyName("volume")] 
    public double Volume { get; init; } = 1.0;

    [JsonPropertyName("emoji_name")]
    public string? EmojiName { get; init; }
}
