using Newtonsoft.Json;
namespace SephiriaCompanion
{
    public static class JsonProtocol
    {
        public const string Version = "0.4.2";
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 16,
            ReferenceLoopHandling = ReferenceLoopHandling.Error
        };
        public static string Write(object value) => JsonConvert.SerializeObject(value, Settings);
        public static CompanionRuntime.Command ReadCommand(string json) => JsonConvert.DeserializeObject<CompanionRuntime.Command>(json, Settings);
    }
}
