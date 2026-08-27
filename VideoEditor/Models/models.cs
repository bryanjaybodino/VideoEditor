using System.Collections.Generic;
using System.Drawing;

namespace VideoEditor.Models
{
    public enum MediaType
    {
        Image,
        Audio,
        Text
    }

    public class TransitionEffect
    {
        public string Type { get; set; } = "None";
        public double Duration { get; set; } = 1.0;
    }

    public class TextLabel
    {
        public float RelativeX { get; set; } = 0.1f;
        public float RelativeY { get; set; } = 0.1f;
        public float RelativeWidth { get; set; } = 0.35f;
        public float RelativeHeight { get; set; } = 0.08f;
        public string Content { get; set; } = "Sample Text";
        public float X { get; set; } = 100;
        public float Y { get; set; } = 100;
        public float Width { get; set; } = 300;
        public float Height { get; set; } = 80;
        public float FontSize { get; set; } = 32;
        public string FontFamily { get; set; } = "Arial";
        public Color TextColor { get; set; } = Color.White;
        public Color BackgroundColor { get; set; } = Color.FromArgb(128, 0, 0, 0);
        public bool IsBold { get; set; } = false;

        public double StartTime { get; set; }
        public double Duration { get; set; } = 3.0;
    }

    public class Caption
    {
        public string Text { get; set; } = string.Empty;
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public double Duration => EndTime - StartTime;
    }

    public class MediaItem
    {
        public string FilePath { get; set; }
        public MediaType Type { get; set; }
        public double StartTime { get; set; }
        public double Duration { get; set; }
        public double OriginalDuration { get; set; }
        public double SourceOffset { get; set; }
        public float PositionX { get; set; } = 0;
        public float PositionY { get; set; } = 0;
        public float Scale { get; set; } = 1.0f;
        public int TrackIndex { get; set; } = 0;

        public float[] AudioPeaks { get; set; }
        public TransitionEffect InEffect { get; set; }
        public TransitionEffect OutEffect { get; set; }

        public List<TextLabel> TextLabels { get; set; } = new List<TextLabel>();
        public TextLabel TextData { get; set; }
        public List<Caption> Captions { get; set; } = new List<Caption>();
    }
}