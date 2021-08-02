﻿using Newtonsoft.Json;

namespace Nano.Net
{
    public class ReceivableBlock
    {
        [JsonProperty("hash")] public string Hash { get; set; }
        [JsonProperty("amount")] public string Amount { get; set; }
        [JsonProperty("source")] public string Source { get; set; }
    }
}