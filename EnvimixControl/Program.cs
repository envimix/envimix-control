using ManiaAPI.XmlRpc;
using TmEssentials;

using var http = new HttpClient();

const string DefaultServerIp = "127.0.0.1";
const ushort DefaultServerPort = 5000;
const string SubmissionApiBaseUrl = "https://api.envimix.gbx.tools";

var serverIp = Environment.GetEnvironmentVariable("EMC_SERVER_IP")?.Trim() switch
{
	{ Length: > 0 } configuredIp => configuredIp,
	_ => DefaultServerIp
};

var serverPort = ushort.TryParse(Environment.GetEnvironmentVariable("EMC_SERVER_PORT")?.Trim(), out var port) ? port : DefaultServerPort;

Console.WriteLine($"Connecting to GBXRemote at {serverIp}:{serverPort}...");

await using var client = await XmlRpcClient.ConnectAsync(serverIp, serverPort);
Console.WriteLine("Connected to GBXRemote.");

await client.CallAsync("Authenticate", Environment.GetEnvironmentVariable("EMC_SUPERADMIN_LOGIN")?.Trim() ?? "SuperAdmin", Environment.GetEnvironmentVariable("EMC_SUPERADMIN_PASSWORD")?.Trim() ?? "SuperAdmin");
Console.WriteLine("Authenticated with GBXRemote.");

var callbacksEnabled = await client.CallAsync<bool>("EnableCallbacks", true);
if (!callbacksEnabled)
{
	throw new InvalidOperationException("GBXRemote refused to enable callbacks.");
}

var systemInfo = await client.CallAsync<Dictionary<string, object>>("GetSystemInfo");

var serverLogin = (string)systemInfo["ServerLogin"];
Console.WriteLine($"Connected to server '{serverLogin}'.");

var controllerCode = Environment.GetEnvironmentVariable("EMC_CONTROLLER_CODE");

if (controllerCode is null or { Length: 0 })
{
    Console.Error.WriteLine($"EMC_CONTROLLER_CODE is not set. Generate a controller code at: https://envimix.gbx.tools/envimania/servers/{serverLogin}#server-actions");
    return 1;
}

var serverName = TextFormatter.Deformat(await client.CallAsync<string>("GetServerName"));
Console.WriteLine($"Server name: '{serverName}'.");

var scriptSettings = await client.CallAsync<Dictionary<string, object>>("GetModeScriptSettings");

if (scriptSettings.TryGetValue("S_EnableEnvimaniaSessions", out var envimaniaSessionsEnabled) && envimaniaSessionsEnabled is bool isEnabled && !isEnabled)
{
    await client.CallAsync("SetModeScriptSettings", new Dictionary<string, object>
    {
        { "S_EnableEnvimaniaSessions", true }
    });
    Console.WriteLine("Enabled Envimania session recording.");
}

client.On("TrackMania.PlayerFinish", async (methodParameters, cancellationToken) =>
{
    var login = (string)methodParameters[1];
    var time = (int)methodParameters[2];

    if (time <= 0)
    {
        Console.WriteLine($"Ignoring non-positive finish time for '{login}': {time}.");
        return;
    }

    Console.WriteLine($"Processing finish for '{login}' ({time} ms).");
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var bestGhostsReplayName = Path.Combine("EnvimixControl", "Records", $"{timestamp}_{Guid.NewGuid()}");

    var results = await client.SystemMulticallAsync([
        new XmlRpcCall { MethodName = "SaveBestGhostsReplay", Parameters = [login, bestGhostsReplayName] },
        new XmlRpcCall { MethodName = "GetValidationReplay", Parameters = [login] }
    ], cancellationToken);

    var submissionTasks = new List<Task>();
    var saveBestGhostsReplayResult = results.ElementAt(0);

    if (saveBestGhostsReplayResult is { IsFault: false, Value: true })
    {
        var bestGhostsReplayPath = Path.Combine("UserData", "Replays", $"{bestGhostsReplayName}.Replay.Gbx");

        if (!File.Exists(bestGhostsReplayPath))
        {
            var gameDataDir = await client.CallAsync<string>("GameDataDirectory", cancellationToken);
            bestGhostsReplayPath = Path.Combine(gameDataDir, "Replays", $"{bestGhostsReplayName}.Replay.Gbx");
        }

        Console.WriteLine($"Queueing best ghost replay '{bestGhostsReplayPath}'.");
        submissionTasks.Add(SubmitBestGhostsReplayAsync(bestGhostsReplayPath, cancellationToken));
    }
    else
    {
        Console.WriteLine($"No best ghost replay available for '{login}'.");
    }

    var getValidationReplayResult = results.ElementAt(1);

    if (getValidationReplayResult is { IsFault: false, Value: byte[] validationReplayData })
    {
        Console.WriteLine($"Queueing validation replay for '{login}' ({validationReplayData.Length} bytes).");
        submissionTasks.Add(SubmitValidationReplayAsync(validationReplayData, timestamp, login, cancellationToken));
    }
    else
    {
        Console.WriteLine($"No validation replay available for '{login}'.");
    }

    Console.WriteLine($"Submitting {submissionTasks.Count} replay(s) for '{login}' in parallel.");
    await Task.WhenAll(submissionTasks);
    Console.WriteLine($"Finished replay submissions for '{login}'.");
});

client.On("TrackMania.EndRace", async (methodParameters, cancellationToken) =>
{
    Console.WriteLine("Race ended, saving session replay.");
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var replayName = Path.Combine("EnvimixControl", "Sessions", $"{timestamp}_{Guid.NewGuid()}");

    await client.CallAsync("SaveCurrentReplay", [replayName], cancellationToken);

    var replayPath = Path.Combine("UserData", "Replays", serverName, "Autosaves", $"{replayName}.Replay.Gbx");

    if (!File.Exists(replayPath))
    {
        var gameDataDir = await client.CallAsync<string>("GameDataDirectory", cancellationToken);
        replayPath = Path.Combine(gameDataDir, "Replays", serverName, "Autosaves", $"{replayName}.Replay.Gbx");
    }

    Console.WriteLine($"Submitting session replay '{replayPath}'.");
    await using var replay = File.OpenRead(replayPath);
    var submitted = await SubmitFileAsync(
        "envimania/session/replay",
        "replay",
        replay,
        Path.GetFileName(replayPath),
        cancellationToken);

    if (submitted)
    {
        File.Delete(replayPath);
        Console.WriteLine($"Deleted submitted session replay '{replayPath}'.");
    }
    else
    {
        Console.Error.WriteLine($"Unable to submit session replay, keeping '{replayPath}'.");
    }
});

Console.WriteLine("Ready.");
await client.WaitForCloseAsync();

return 0;

async Task SubmitBestGhostsReplayAsync(string replayPath, CancellationToken cancellationToken)
{
    Console.WriteLine($"Submitting best ghost replay '{replayPath}'.");
    await using var replay = File.OpenRead(replayPath);
    var submitted = await SubmitFileAsync(
        "replays/submit",
        "replay",
        replay,
        Path.GetFileName(replayPath),
        cancellationToken);

    if (submitted)
    {
        File.Delete(replayPath);
        Console.WriteLine($"Deleted submitted best ghost replay '{replayPath}'.");
    }
    else
    {
        Console.Error.WriteLine($"Unable to submit best ghost replay, keeping '{replayPath}'.");
    }
}

async Task SubmitValidationReplayAsync(byte[] replayData, long timestamp, string login, CancellationToken cancellationToken)
{
    Console.WriteLine($"Submitting validation replay for '{login}'.");
    await using var replay = new MemoryStream(replayData, writable: false);
    await SubmitFileAsync(
        "replays/submit/validable",
        "replay",
        replay,
        $"{timestamp}_{login}.Replay.Gbx",
        cancellationToken);
}

async Task<bool> SubmitFileAsync(
    string endpoint,
    string fileFieldName,
    Stream file,
    string fileName,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"Posting '{fileName}' to {endpoint}.");
    using var form = new MultipartFormDataContent
    {
        { new StringContent(serverLogin), "serverLogin" },
        { new StringContent(controllerCode), "controllerCode" }
    };
    using var fileContent = new StreamContent(file);
    form.Add(fileContent, fileFieldName, fileName);

    using var response = await http.PostAsync($"{SubmissionApiBaseUrl}/{endpoint}", form, cancellationToken);
    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine($"Submitted '{fileName}' to {endpoint} with HTTP {(int)response.StatusCode}.");
        return true;
    }

    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
    Console.Error.WriteLine($"Unable to submit '{fileName}' to {endpoint}: HTTP {(int)response.StatusCode} {responseBody}");
    return false;
}
