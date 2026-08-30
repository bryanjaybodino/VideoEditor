# Video Reel & Image Slideshow Editor with Auto Caption

A feature-rich desktop application built with C# and WinForms designed for creating video reels, animated image slideshows, custom overlays, and automatic speech-to-text captions.

---

## 🌟 Key Features

* **Multi-Track Timeline:**
  * Interactive timeline with support for drag-and-drop position editing and clip resizing.
  * Multi-layer visuals (Images, Blur Overlays, Text Labels) and dedicated audio tracks.
  * Real-time playhead scrubbing and audio synchronization.

* **AI Auto-Captioning:**
  * Automated audio transcription using Google Gemini AI.
  * Automatically generates perfectly synchronized text overlays across the timeline.

* **Image & Dynamic Animation Controls:**
  * Built-in transition effects (Dynamic Zoom Blur, Fade, Slide, Wave, Zoom).
  * Direct canvas editing: drag to position, wheel to scale/zoom, and control element boundaries.

* **Blur & Text Overlays:**
  * Custom blur overlay boxes with adjustable radius operating across timeline layers.
  * Fully customizable text layers (Font size, text color, background color, and position).

* **Video Export & Undo/Redo:**
  * FFmpeg integration for exporting multi-track compositions into HD MP4 video files.
  * Full Undo/Redo command stack (`Ctrl+Z` / `Ctrl+Y`) for seamless editing workflows.
  * Modern Dark Mode UI theme.

---

## 🚀 Getting Started

### Prerequisites

* **.NET Framework / .NET Core / .NET SDK** (compatible with WinForms)
* **FFmpeg**: Placed in system `PATH` or the output binary folder for video rendering and audio processing.
* **Gemini API Key**: Required for auto-captioning features.

### Installation & Running

1. Clone the repository:
   ```bash
   git clone [https://github.com/bryanjaybodino/VideoEditor.git](https://github.com/bryanjaybodino/VideoEditor.git)
