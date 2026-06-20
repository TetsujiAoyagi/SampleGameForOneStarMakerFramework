#nullable enable

using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// inspector の 1 行プロパティ。
    /// まずは表示文字列を主とし、必要に応じて raw 値や path を補助情報で持たせる。
    /// </summary>
    [MessagePackObject]
    public sealed class InspectorPropertyDtoV1
    {
        [Key(0)]
        public int PropertyId { get; set; }

        [Key(1)]
        public int ValueTypeId { get; set; }

        [Key(2)]
        public InspectorPropertyFlags Flags { get; set; } = InspectorPropertyFlags.None;

        [Key(3)]
        public string DisplayName { get; set; } = string.Empty;

        [Key(4)]
        public string ValueText { get; set; } = string.Empty;

        [Key(5)]
        public string? RawValue { get; set; }

        [Key(6)]
        public string? Unit { get; set; }

        [Key(7)]
        public string? Path { get; set; }
    }
}
