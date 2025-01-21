using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using Newtonsoft.Json;

namespace GalgameManager.Helpers;

public class CategoryAndUuidConverter(ICategoryService categoryService) : JsonConverter<Category>
{
    public override void WriteJson(JsonWriter writer, Category? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value?.Id);
    }

    public override Category? ReadJson(JsonReader reader, Type objectType, Category? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        Guid? uid = serializer.Deserialize<Guid?>(reader);
        return uid is null ? null : categoryService.GetCategory(uid.Value);
    }
}