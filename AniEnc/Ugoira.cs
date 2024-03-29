﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AniEnc
{
    public class Ugoira
    {
        [JsonPropertyName("src")]
        public string? Src { get; set; }
        
        [JsonPropertyName("originalSrc")]
        public string? OriginalSrc { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("frames")]
        public List<UgoiraFrame> Frames { get; set; } = new List<UgoiraFrame>();
    }

    public class UgoiraFrame
    {
        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("delay")]
        public int Delay { get; set; }
    }
}
