using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Refit;

namespace GalgameManager.Helpers.API.Steam;
public static class SteamAPi
{
    public static ISteamApi GetApi()
    {
        HttpClient client = Utils.GetDefaultHttpClient().WithApplicationJson();
        client.BaseAddress = new Uri("https://api.steampowered.com");
        return RestService.For<ISteamApi>(client, new RefitSettings
        {
            ContentSerializer = new NewtonsoftJsonContentSerializer(new JsonSerializerSettings
            {
                Converters =
                {
                    new StringEnumConverter(),
                },
            }),
        });

    }
}
