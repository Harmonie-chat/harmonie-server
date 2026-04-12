namespace Harmonie.API.SignalRDoc.Middleware;

internal static class EmbeddedAssets
{
    private static string? _htmlPage;

    internal static string LoadHtmlPage()
    {
        if (_htmlPage is not null)
            return _htmlPage;

        var assembly = typeof(EmbeddedAssets).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "Harmonie.API.SignalRDoc.Assets.asyncapi-ui.html");

        if (stream is null)
            throw new InvalidOperationException(
                "Embedded resource 'Harmonie.API.SignalRDoc.Assets.asyncapi-ui.html' not found.");

        using var reader = new StreamReader(stream);
        _htmlPage = reader.ReadToEnd();
        return _htmlPage;
    }
}
