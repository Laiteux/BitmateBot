using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bitmate.Utilities.Json
{
    public class InlineKeyboardMarkupJsonConverter : JsonConverter<InlineKeyboardMarkup>
    {
        public override InlineKeyboardMarkup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, InlineKeyboardMarkup value, JsonSerializerOptions options)
        {
            writer.WriteNullValue();
        }
    }
}
