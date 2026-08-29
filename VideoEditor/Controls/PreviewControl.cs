using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VideoEditor.Commands;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Controls
{
    public class PreviewControl : UserControl
    {
        private List<MediaItem> activeFrameItems = new List<MediaItem>();
        private List<MediaItem> allFrameItems = new List<MediaItem>();
        private Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private double currentTimePosition = 0;

        private MediaItem selectedPreviewItem = null;
        private TextLabel selectedTextLabel = null;
        private bool isDraggingImage = false;
        private bool isDraggingText = false;
        private bool isResizingText = false;
        private bool isDraggingBlur = false;
        private bool isResizingBlur = false;
        private bool isResizingImage = false;
        private Point lastMousePos;

        private TextTransformState initialTextTransform;
        private BlurTransformState initialBlurTransform;
        private ImageTransformState initialImageTransform;

        public TimelineControl TimelineRef { get; set; }
        public UndoRedoManager UndoRedoManager { get; set; }

        private bool IsItemLocked(MediaItem item)
        {
            if (item == null || TimelineRef == null) return false;
            return item.Type != MediaType.Audio && TimelineRef.IsTrackLocked(item.TrackIndex);
        }

        public int LastCanvasWidth { get; private set; } = 1080;
        public int LastCanvasHeight { get; private set; } = 1920;

        public MediaItem SelectedItem { get; set; }
        public TextLabel SelectedTextLabel
        {
            get => selectedTextLabel;
            set
            {
                selectedTextLabel = value;
                TextLabelSelected?.Invoke(selectedTextLabel);
                this.Invalidate();
            }
        }

        public event Action<TextLabel> TextLabelSelected;
        public event Action ItemTransformChanged;

        public PreviewControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(15, 15, 15);
            this.TabStop = true;

            this.MouseDown += PreviewControl_MouseDown;
            this.MouseMove += PreviewControl_MouseMove;
            this.MouseUp += PreviewControl_MouseUp;
            this.MouseWheel += PreviewControl_MouseWheel;
            this.KeyUp += PreviewControl_KeyUp;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete)
            {
                if (SelectedTextLabel != null || SelectedItem != null)
                {
                    SelectedItem = null;
                    SelectedTextLabel = null;
                    this.Invalidate();
                    return true;
                }
            }

            if (keyData == (Keys.Control | Keys.Z) || keyData == (Keys.Control | Keys.Y))
            {
                return false;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (SelectedTextLabel != null) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (SelectedTextLabel != null)
            {
                if (e.KeyChar == (char)Keys.Back)
                {
                    if (SelectedTextLabel.Content.Length > 0)
                    {
                        SelectedTextLabel.Content = SelectedTextLabel.Content.Substring(0, SelectedTextLabel.Content.Length - 1);
                        this.Invalidate();
                    }
                }
                else if (e.KeyChar == (char)Keys.Enter || e.KeyChar == '\r')
                {
                    SelectedTextLabel.Content += Environment.NewLine;
                    this.Invalidate();
                }
                else if (!char.IsControl(e.KeyChar))
                {
                    SelectedTextLabel.Content += e.KeyChar;
                    this.Invalidate();
                }
            }
        }

        public void RenderFrame(List<MediaItem> items, double timePosition)
        {
            currentTimePosition = timePosition;
            allFrameItems = items ?? new List<MediaItem>();

            activeFrameItems = allFrameItems
                .Where(item => (item.Type == MediaType.Image || item.Type == MediaType.Blur) &&
                               timePosition >= item.StartTime &&
                               timePosition < item.StartTime + item.Duration)
                .OrderByDescending(item => item.TrackIndex)
                .ToList();

            foreach (var item in activeFrameItems)
            {
                if (item.Type == MediaType.Image && !string.IsNullOrEmpty(item.FilePath) && !imageCache.ContainsKey(item.FilePath) && File.Exists(item.FilePath))
                {
                    using (var temp = VideoRenderHelper.GetCachedImage(item.FilePath))
                    {
                        imageCache[item.FilePath] = new Bitmap(temp);
                    }
                }
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(this.BackColor);

            float targetAspect = 9.0f / 16.0f;
            int canvasWidth = this.Width;
            int canvasHeight = this.Height;

            if ((float)canvasWidth / canvasHeight > targetAspect)
                canvasWidth = (int)(canvasHeight * targetAspect);
            else
                canvasHeight = (int)(canvasWidth / targetAspect);

            LastCanvasWidth = canvasWidth;
            LastCanvasHeight = canvasHeight;

            int canvasX = (this.Width - canvasWidth) / 2;
            int canvasY = (this.Height - canvasHeight) / 2;

            g.SetClip(new Rectangle(canvasX, canvasY, canvasWidth, canvasHeight));

            var activeVisualItems = allFrameItems
                .Where(item => (item.Type == MediaType.Image || (item.Type == MediaType.Blur && item.BlurData != null)) &&
                               currentTimePosition >= item.StartTime &&
                               currentTimePosition < item.StartTime + item.Duration)
                .OrderByDescending(item => item.TrackIndex)
                .ToList();

            var activeTextItems = allFrameItems
                .Where(item => item.Type == MediaType.Text &&
                               item.TextData != null &&
                               currentTimePosition >= item.StartTime &&
                               currentTimePosition < item.StartTime + item.Duration)
                .OrderBy(item => item.TrackIndex)
                .ToList();

            if (activeVisualItems.Count > 0 || activeTextItems.Count > 0)
            {
                foreach (var item in activeVisualItems)
                {
                    if (item.Type == MediaType.Image && !string.IsNullOrEmpty(item.FilePath) && imageCache.TryGetValue(item.FilePath, out Image img) && img != null)
                    {
                        VideoRenderHelper.DrawImageItem(g, img, item, currentTimePosition, canvasX, canvasY, canvasWidth, canvasHeight);

                        if (item == SelectedItem)
                        {
                            DrawImageSelectionHighlight(g, item, canvasX, canvasY, canvasWidth, canvasHeight);
                        }

                        double localTime = currentTimePosition - item.StartTime;
                        if (item.TextLabels != null)
                        {
                            foreach (var label in item.TextLabels)
                            {
                                if (localTime >= label.StartTime && localTime <= label.StartTime + label.Duration)
                                {
                                    DrawTextLabelWithSelection(g, label, canvasX, canvasY, canvasWidth, canvasHeight);
                                }
                            }
                        }
                    }
                    else if (item.Type == MediaType.Blur && item.BlurData != null)
                    {
                        VideoRenderHelper.DrawBlurOverlay(g, item.BlurData, allFrameItems, currentTimePosition, canvasX, canvasY, canvasWidth, canvasHeight, item.TrackIndex);

                        if (item == SelectedItem)
                        {
                            DrawBlurSelectionHighlight(g, item, canvasX, canvasY, canvasWidth, canvasHeight);
                        }
                    }
                }

                foreach (var textItem in activeTextItems)
                {
                    DrawTextLabelWithSelection(g, textItem.TextData, canvasX, canvasY, canvasWidth, canvasHeight);
                }
            }
            else
            {
                using (var font = new Font("Segoe UI", 12))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    var msg = "No Active Media Frame";
                    var sz = g.MeasureString(msg, font);
                    g.DrawString(msg, font, brush, (this.Width - sz.Width) / 2, (this.Height - sz.Height) / 2);
                }
            }

            g.ResetClip();
            g.DrawRectangle(Pens.Gray, canvasX, canvasY, canvasWidth, canvasHeight);
        }

        private void DrawImageSelectionHighlight(Graphics g, MediaItem item, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            if (!string.IsNullOrEmpty(item.FilePath) && imageCache.TryGetValue(item.FilePath, out Image img) && img != null)
            {
                float scale = Math.Max((float)canvasWidth / img.Width, (float)canvasHeight / img.Height) * item.Scale;
                int baseW = (int)(img.Width * scale);
                int baseH = (int)(img.Height * scale);

                float posX = item.PositionX * ((float)canvasWidth / 1080f);
                float posY = item.PositionY * ((float)canvasHeight / 1920f);

                int originX = canvasX + (canvasWidth - baseW) / 2 + (int)posX;
                int originY = canvasY + (canvasHeight - baseH) / 2 + (int)posY;

                using (var pen = new Pen(Color.Cyan, 2f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(pen, originX, originY, baseW, baseH);
                }

                // Draw resize handle in bottom-right corner
                g.FillRectangle(Brushes.Cyan, originX + baseW - 6, originY + baseH - 6, 12, 12);
            }
        }

        private void DrawBlurSelectionHighlight(Graphics g, MediaItem blurItem, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            if (blurItem?.BlurData == null) return;

            var blur = blurItem.BlurData;
            float drawX = canvasX + (blur.RelativeX * canvasWidth);
            float drawY = canvasY + (blur.RelativeY * canvasHeight);
            float drawW = Math.Max(blur.RelativeWidth * canvasWidth, 30);
            float drawH = Math.Max(blur.RelativeHeight * canvasHeight, 30);

            using (var pen = new Pen(Color.Magenta, 2f) { DashStyle = DashStyle.Dash })
            {
                g.DrawRectangle(pen, drawX, drawY, drawW, drawH);
            }

            g.FillRectangle(Brushes.Magenta, drawX + drawW - 6, drawY + drawH - 6, 12, 12);
        }

        private void DrawTextLabelWithSelection(Graphics g, TextLabel label, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            VideoRenderHelper.DrawTextLabel(g, label, canvasX, canvasY, canvasWidth, canvasHeight);

            if (label == SelectedTextLabel)
            {
                float drawX = canvasX + (label.RelativeX * canvasWidth);
                float drawY = canvasY + (label.RelativeY * canvasHeight);
                float drawW = Math.Max(label.RelativeWidth * canvasWidth, 50);
                float drawH = Math.Max(label.RelativeHeight * canvasHeight, 30);

                using (var pen = new Pen(Color.Cyan, 2) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(pen, drawX, drawY, drawW, drawH);
                }
                g.FillRectangle(Brushes.Cyan, drawX + drawW - 6, drawY + drawH - 6, 12, 12);
            }
        }

        private void PreviewControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            this.Focus();

            int canvasX = (this.Width - LastCanvasWidth) / 2;
            int canvasY = (this.Height - LastCanvasHeight) / 2;

            int selectedTrack = TimelineRef?.SelectedTrackIndex ?? -1;
            var timelineSelectedItem = TimelineRef?.SelectedItem;

            SelectedTextLabel = null;
            selectedPreviewItem = null;

            var activeItems = allFrameItems
                .Where(item => !IsItemLocked(item) &&
                               currentTimePosition >= item.StartTime &&
                               currentTimePosition < item.StartTime + item.Duration)
                // 1. PRIMARY: Selected timeline item or active track gets first priority
                .OrderByDescending(item => item == timelineSelectedItem || item.TrackIndex == selectedTrack)
                // 2. SECONDARY: Higher track numbers draw on top, so check them first
                .ThenByDescending(item => item.TrackIndex)
                // 3. TIE-BREAKER: If on the same track, prioritize Text > Blur > Image
                .ThenByDescending(item => item.Type switch
                {
                    MediaType.Text => 3,
                    MediaType.Blur => 2,
                    MediaType.Image => 1,
                    _ => 0
                })
                .ToList();

            foreach (var item in activeItems)
            {
                if (item.Type == MediaType.Text && item.TextData != null)
                {
                    var label = item.TextData;
                    float lX = canvasX + (label.RelativeX * LastCanvasWidth);
                    float lY = canvasY + (label.RelativeY * LastCanvasHeight);
                    float lW = label.RelativeWidth * LastCanvasWidth;
                    float lH = label.RelativeHeight * LastCanvasHeight;

                    RectangleF handleRect = new RectangleF(lX + lW - 10, lY + lH - 10, 20, 20);
                    RectangleF boundsRect = new RectangleF(lX, lY, lW, lH);

                    if (handleRect.Contains(e.Location) || boundsRect.Contains(e.Location))
                    {
                        SelectedItem = item;
                        SelectedTextLabel = label;
                        isResizingText = handleRect.Contains(e.Location);
                        isDraggingText = !isResizingText;
                        lastMousePos = e.Location;

                        initialTextTransform = new TextTransformState
                        {
                            RelativeX = label.RelativeX,
                            RelativeY = label.RelativeY,
                            RelativeWidth = label.RelativeWidth,
                            RelativeHeight = label.RelativeHeight
                        };

                        TextLabelSelected?.Invoke(label);
                        this.Invalidate();
                        return;
                    }
                }
                else if (item.Type == MediaType.Blur && item.BlurData != null)
                {
                    var blur = item.BlurData;
                    float bX = canvasX + (blur.RelativeX * LastCanvasWidth);
                    float bY = canvasY + (blur.RelativeY * LastCanvasHeight);
                    float bW = blur.RelativeWidth * LastCanvasWidth;
                    float bH = blur.RelativeHeight * LastCanvasHeight;

                    RectangleF handleRect = new RectangleF(bX + bW - 10, bY + bH - 10, 20, 20);
                    RectangleF boundsRect = new RectangleF(bX, bY, bW, bH);

                    if (handleRect.Contains(e.Location) || boundsRect.Contains(e.Location))
                    {
                        SelectedItem = item;
                        selectedPreviewItem = item;
                        isResizingBlur = handleRect.Contains(e.Location);
                        isDraggingBlur = !isResizingBlur;
                        lastMousePos = e.Location;

                        initialBlurTransform = new BlurTransformState
                        {
                            RelativeX = blur.RelativeX,
                            RelativeY = blur.RelativeY,
                            RelativeWidth = blur.RelativeWidth,
                            RelativeHeight = blur.RelativeHeight
                        };

                        this.Invalidate();
                        return;
                    }
                }
                else if (item.Type == MediaType.Image && !string.IsNullOrEmpty(item.FilePath))
                {
                    if (imageCache.TryGetValue(item.FilePath, out Image img) && img != null)
                    {
                        float scale = Math.Max((float)LastCanvasWidth / img.Width, (float)LastCanvasHeight / img.Height) * item.Scale;
                        int baseW = (int)(img.Width * scale);
                        int baseH = (int)(img.Height * scale);

                        float posX = item.PositionX * ((float)LastCanvasWidth / 1080f);
                        float posY = item.PositionY * ((float)LastCanvasHeight / 1920f);

                        int originX = canvasX + (LastCanvasWidth - baseW) / 2 + (int)posX;
                        int originY = canvasY + (LastCanvasHeight - baseH) / 2 + (int)posY;

                        RectangleF handleRect = new RectangleF(originX + baseW - 10, originY + baseH - 10, 20, 20);
                        RectangleF imageBounds = new RectangleF(originX, originY, baseW, baseH);

                        if (handleRect.Contains(e.Location) || imageBounds.Contains(e.Location))
                        {
                            selectedPreviewItem = item;
                            SelectedItem = item;
                            isResizingImage = handleRect.Contains(e.Location);
                            isDraggingImage = !isResizingImage;
                            lastMousePos = e.Location;

                            initialImageTransform = new ImageTransformState
                            {
                                PositionX = item.PositionX,
                                PositionY = item.PositionY,
                                Scale = item.Scale
                            };

                            this.Invalidate();
                            return;
                        }
                    }
                }
                else if (item.Type == MediaType.Image && !string.IsNullOrEmpty(item.FilePath))
                {
                    if (imageCache.TryGetValue(item.FilePath, out Image img) && img != null)
                    {
                        float scale = Math.Max((float)LastCanvasWidth / img.Width, (float)LastCanvasHeight / img.Height) * item.Scale;
                        int baseW = (int)(img.Width * scale);
                        int baseH = (int)(img.Height * scale);

                        float posX = item.PositionX * ((float)LastCanvasWidth / 1080f);
                        float posY = item.PositionY * ((float)LastCanvasHeight / 1920f);

                        int originX = canvasX + (LastCanvasWidth - baseW) / 2 + (int)posX;
                        int originY = canvasY + (LastCanvasHeight - baseH) / 2 + (int)posY;

                        RectangleF handleRect = new RectangleF(originX + baseW - 10, originY + baseH - 10, 20, 20);
                        RectangleF imageBounds = new RectangleF(originX, originY, baseW, baseH);

                        if (handleRect.Contains(e.Location) || imageBounds.Contains(e.Location))
                        {
                            selectedPreviewItem = item;
                            SelectedItem = item;
                            isResizingImage = handleRect.Contains(e.Location);
                            isDraggingImage = !isResizingImage;
                            lastMousePos = e.Location;

                            initialImageTransform = new ImageTransformState
                            {
                                PositionX = item.PositionX,
                                PositionY = item.PositionY,
                                Scale = item.Scale
                            };

                            this.Invalidate();
                            return;
                        }
                    }
                }
            }

            TextLabelSelected?.Invoke(null);
            this.Invalidate();
        }

        private void PreviewControl_MouseMove(object sender, MouseEventArgs e)
        {
            int deltaX = e.X - lastMousePos.X;
            int deltaY = e.Y - lastMousePos.Y;

            if (isResizingText && selectedTextLabel != null && LastCanvasWidth > 0 && LastCanvasHeight > 0)
            {
                float currentW = selectedTextLabel.RelativeWidth * LastCanvasWidth;
                float currentH = selectedTextLabel.RelativeHeight * LastCanvasHeight;

                selectedTextLabel.RelativeWidth = Math.Max(50f, currentW + deltaX) / LastCanvasWidth;
                selectedTextLabel.RelativeHeight = Math.Max(30f, currentH + deltaY) / LastCanvasHeight;

                lastMousePos = e.Location;
                this.Invalidate();
            }
            else if (isDraggingText && selectedTextLabel != null && LastCanvasWidth > 0 && LastCanvasHeight > 0)
            {
                float currentX = selectedTextLabel.RelativeX * LastCanvasWidth;
                float currentY = selectedTextLabel.RelativeY * LastCanvasHeight;

                selectedTextLabel.RelativeX = (currentX + deltaX) / LastCanvasWidth;
                selectedTextLabel.RelativeY = (currentY + deltaY) / LastCanvasHeight;

                lastMousePos = e.Location;
                this.Invalidate();
            }
            else if (isResizingBlur && SelectedItem?.BlurData != null && LastCanvasWidth > 0 && LastCanvasHeight > 0)
            {
                var blur = SelectedItem.BlurData;
                float currentW = blur.RelativeWidth * LastCanvasWidth;
                float currentH = blur.RelativeHeight * LastCanvasHeight;

                blur.RelativeWidth = Math.Max(30f, currentW + deltaX) / LastCanvasWidth;
                blur.RelativeHeight = Math.Max(30f, currentH + deltaY) / LastCanvasHeight;

                lastMousePos = e.Location;
                ItemTransformChanged?.Invoke();
                this.Invalidate();
            }
            else if (isDraggingBlur && SelectedItem?.BlurData != null && LastCanvasWidth > 0 && LastCanvasHeight > 0)
            {
                var blur = SelectedItem.BlurData;
                float currentX = blur.RelativeX * LastCanvasWidth;
                float currentY = blur.RelativeY * LastCanvasHeight;

                blur.RelativeX = (currentX + deltaX) / LastCanvasWidth;
                blur.RelativeY = (currentY + deltaY) / LastCanvasHeight;

                lastMousePos = e.Location;
                ItemTransformChanged?.Invoke();
                this.Invalidate();
            }
            else if (isDraggingImage && selectedPreviewItem != null)
            {
                selectedPreviewItem.PositionX += deltaX * (1080f / LastCanvasWidth);
                selectedPreviewItem.PositionY += deltaY * (1920f / LastCanvasHeight);
                lastMousePos = e.Location;
                ItemTransformChanged?.Invoke();
                this.Invalidate();
            }
            else if (isResizingImage && selectedPreviewItem != null)
            {
                // Uniform scale adjustment based on drag distance
                float deltaScale = (deltaX + deltaY) * 0.005f;
                selectedPreviewItem.Scale = Math.Clamp(selectedPreviewItem.Scale + deltaScale, 0.1f, 10.0f);

                lastMousePos = e.Location;
                ItemTransformChanged?.Invoke();
                this.Invalidate();
            }
        }

        private void PreviewControl_MouseUp(object sender, MouseEventArgs e)
        {
            CommitTransformCommand();
            isResizingImage = false;
            isDraggingImage = false;
            isDraggingText = false;
            isResizingText = false;
            isDraggingBlur = false;
            isResizingBlur = false;
        }

        private void PreviewControl_KeyUp(object sender, KeyEventArgs e)
        {
            CommitTransformCommand();
        }

        private void CommitTransformCommand()
        {
            if ((isDraggingText || isResizingText) && selectedTextLabel != null && initialTextTransform != null)
            {
                var newState = new TextTransformState
                {
                    RelativeX = selectedTextLabel.RelativeX,
                    RelativeY = selectedTextLabel.RelativeY,
                    RelativeWidth = selectedTextLabel.RelativeWidth,
                    RelativeHeight = selectedTextLabel.RelativeHeight
                };

                if (initialTextTransform.RelativeX != newState.RelativeX ||
                    initialTextTransform.RelativeY != newState.RelativeY ||
                    initialTextTransform.RelativeWidth != newState.RelativeWidth ||
                    initialTextTransform.RelativeHeight != newState.RelativeHeight)
                {
                    var cmd = new TransformTextCommand(selectedTextLabel, initialTextTransform, newState);
                    UndoRedoManager?.ExecuteCommand(cmd);
                }
                initialTextTransform = null;
            }

            if ((isDraggingBlur || isResizingBlur) && SelectedItem?.BlurData != null && initialBlurTransform != null)
            {
                var blur = SelectedItem.BlurData;
                var newState = new BlurTransformState
                {
                    RelativeX = blur.RelativeX,
                    RelativeY = blur.RelativeY,
                    RelativeWidth = blur.RelativeWidth,
                    RelativeHeight = blur.RelativeHeight
                };

                if (initialBlurTransform.RelativeX != newState.RelativeX ||
                    initialBlurTransform.RelativeY != newState.RelativeY ||
                    initialBlurTransform.RelativeWidth != newState.RelativeWidth ||
                    initialBlurTransform.RelativeHeight != newState.RelativeHeight)
                {
                    var cmd = new TransformBlurCommand(blur, initialBlurTransform, newState);
                    UndoRedoManager?.ExecuteCommand(cmd);
                }
                initialBlurTransform = null;
            }

            if ((isDraggingImage || isResizingImage) && selectedPreviewItem != null && initialImageTransform != null)
            {
                var newState = new ImageTransformState
                {
                    PositionX = selectedPreviewItem.PositionX,
                    PositionY = selectedPreviewItem.PositionY,
                    Scale = selectedPreviewItem.Scale
                };

                if (initialImageTransform.PositionX != newState.PositionX ||
                    initialImageTransform.PositionY != newState.PositionY ||
                    initialImageTransform.Scale != newState.Scale)
                {
                    var cmd = new TransformImageCommand(selectedPreviewItem, initialImageTransform, newState);
                    UndoRedoManager?.ExecuteCommand(cmd);
                }
                initialImageTransform = null;
            }
        }

        private void PreviewControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (SelectedItem?.Type == MediaType.Image && selectedTextLabel == null && !IsItemLocked(SelectedItem))
            {
                var oldState = new ImageTransformState
                {
                    PositionX = SelectedItem.PositionX,
                    PositionY = SelectedItem.PositionY,
                    Scale = SelectedItem.Scale
                };

                float zoomDelta = e.Delta > 0 ? 1.05f : 0.95f;
                SelectedItem.Scale = Math.Clamp(SelectedItem.Scale * zoomDelta, 0.1f, 5.0f);

                var newState = new ImageTransformState
                {
                    PositionX = SelectedItem.PositionX,
                    PositionY = SelectedItem.PositionY,
                    Scale = SelectedItem.Scale
                };

                var cmd = new TransformImageCommand(SelectedItem, oldState, newState);
                UndoRedoManager?.ExecuteCommand(cmd);

                ItemTransformChanged?.Invoke();
                this.Invalidate();
            }
        }
    }
}