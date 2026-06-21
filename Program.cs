using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Setup config & httpclient
var config = new Config(Environment.GetEnvironmentVariable("Token") ?? throw new Exception(), "645274523867807773", "/home/senne/Downloads/Sounds/");

if (string.IsNullOrWhiteSpace(config.BotToken) || string.IsNullOrWhiteSpace(config.GuildId)
    || !Directory.Exists(config.SoundsDirectory))
{
    throw new Exception("Error initializing");
}

using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", config.BotToken);
http.DefaultRequestHeaders.Add("User-Agent", "DiscordSoundboardUploader/1.0");

// Delete all existing files
await DeleteAllSoundsFromDiscord(http);

var oggFiles = Directory.GetFiles(config.SoundsDirectory, "*.ogg", SearchOption.TopDirectoryOnly);
var maxDiscordSounds = 8;

if (oggFiles.Length > maxDiscordSounds)
{
    List<int> hitIndices = [];

    for (int i = 0; i < maxDiscordSounds; i++)
    {
        int random;

        do
        {
            random = Random.Shared.Next(oggFiles.Length);
        } while (hitIndices.Contains(random));

        var randomFile = oggFiles[random];

        var fileName = Path.GetFileNameWithoutExtension(randomFile);

        var emoji = fileName.Split('-')[0];
        var soundName = fileName.Split('-')[1];

        await UploadSoundToDiscord(http, emoji, soundName, randomFile);
    }
}
else
{
    foreach (var file in oggFiles)
    {
        var fileName = Path.GetFileNameWithoutExtension(file);

        var emoji = fileName.Split('-')[0];
        var soundName = fileName.Split('-')[1];

        await UploadSoundToDiscord(http, emoji, soundName, file);
    }
}

async Task DeleteAllSoundsFromDiscord(HttpClient httpClient)
{
    var url = $"https://discord.com/api/v10/guilds/{config.GuildId}/soundboard-sounds";

    HttpResponseMessage response;
    try
    {
        response = await httpClient.GetAsync(url);
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine(ex);
        throw;
    }

    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new Exception("Error deleting previous sounds");
    }

    var soundboardSounds = JsonSerializer.Deserialize<SoundBoardSounds>(responseBody) ?? throw new Exception("Failed to deserialize");

    if (soundboardSounds.Items.Length == 0)
    {
        return;
    }

    foreach (var sound in soundboardSounds.Items)
    {
        try
        {
            var deleteUrl = $"https://discord.com/api/v10/guilds/{config.GuildId}/soundboard-sounds/{sound.SoundId}";
            response = await httpClient.DeleteAsync(deleteUrl);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(ex);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to delete sound with id {sound.SoundId}");
        }
    }
}

async Task UploadSoundToDiscord(HttpClient httpClient, string emoji, string soundName, string file)
{
    var fileBytes  = await File.ReadAllBytesAsync(file);
    var fileSizeKb = fileBytes.Length / 1024.0;

    if (fileBytes.Length > 512 * 1024)
    {
        throw new Exception($"File is too large ({fileSizeKb:F1} KB). Discord'imit is 512 KB.");
    }

    var base64Data = Convert.ToBase64String(fileBytes);
    var dataUri    = $"data:audio/ogg;base64,{base64Data}";

    var payload = new CreateSoundRequest
    {
        Name = soundName,
        Sound = dataUri,
        Volume = 1.0,
        EmojiName = emoji
    };

    var json = JsonSerializer.Serialize(payload);

    // Upload to Discord
    var url = $"https://discord.com/api/v10/guilds/{config.GuildId}/soundboard-sounds";
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    HttpResponseMessage response;
    try
    {
        response = await httpClient.PostAsync(url, content);
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine(ex);
        return;
    }

    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            Console.WriteLine(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            Console.WriteLine(responseBody);
        }
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

internal record SoundBoardSounds
{
    [JsonPropertyName("items")] 
    public SoundBoardSound[] Items { get; init; } = [];
}

internal record SoundBoardSound
{
    [JsonPropertyName("name")] 
    public string Name { get; init; } = "";

    [JsonPropertyName("sound_id")] 
    public string SoundId { get; init; } = "";

    [JsonPropertyName("volume")] 
    public double Volume { get; init; } = 1.0;

    [JsonPropertyName("emoji_id")]
    public string? EmojiId { get; init; }

    [JsonPropertyName("emoji_name")]
    public string? EmojiName { get; init; }

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; init; }

    [JsonPropertyName("available")]
    public bool Available { get; init; }
}
