using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VideoEditor.Models
{
    public enum MediaType
    {
        Image,
        Audio,
        Video
    }

    public class TransitionEffect
    {
        public string Type { get; set; } = "None";
        public double Duration { get; set; } = 0.5;
    }

    public class MediaItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; }
        public MediaType Type { get; set; }
        public double Duration { get; set; } = 3.0;
        public double StartTime { get; set; } = 0.0;
        public int TrackIndex { get; set; } = 0;

        public double OriginalDuration { get; set; } = 0;
        public double SourceOffset { get; set; } = 0;

        // Waveform Cache Data
        public float[] AudioPeaks { get; set; }

        public TransitionEffect InEffect { get; set; } = new TransitionEffect { Type = "Fade", Duration = 0.5 };
        public TransitionEffect OutEffect { get; set; } = new TransitionEffect { Type = "None", Duration = 0.5 };

        public List<TextLabel> TextLabels { get; set; } = new List<TextLabel>();

        public float Scale { get; set; } = 1.0f;
        public float PositionX { get; set; } = 0.0f;
        public float PositionY { get; set; } = 0.0f;
    }

    public class TextLabel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float FontSize { get; set; } = 24;
        public Color Color { get; set; } = Color.White;
        public string FontFamily { get; set; } = "Segoe UI";
        public double StartTime { get; set; }
        public double Duration { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
    }

    public class Caption
    {
        public string Text { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int ConfidenceScore { get; set; }
    }

    public class ProjectState
    {
        public ListBox MediaListView { get; set; }
        public Panel PropertiesPanel { get; set; }
        public int SelectedItemIndex { get; set; } = -1;
        public string ProjectName { get; set; } = "Untitled Project";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public enum EffectType
    {
        Blur
    }

    public class EffectItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public EffectType Type { get; set; } = EffectType.Blur;
        public double StartTime { get; set; }
        public double Duration { get; set; } = 3.0;
        public float Intensity { get; set; } = 10.0f;
    }
}