using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class TextTransformState
    {
        public float RelativeX { get; set; }
        public float RelativeY { get; set; }
        public float RelativeWidth { get; set; }
        public float RelativeHeight { get; set; }
    }

    public class BlurTransformState
    {
        public float RelativeX { get; set; }
        public float RelativeY { get; set; }
        public float RelativeWidth { get; set; }
        public float RelativeHeight { get; set; }
    }

    public class ImageTransformState
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float Scale { get; set; }
    }

    public class TransformTextCommand : IUndoableCommand
    {
        private readonly TextLabel _label;
        private readonly TextTransformState _oldState;
        private readonly TextTransformState _newState;

        public TransformTextCommand(TextLabel label, TextTransformState oldState, TextTransformState newState)
        {
            _label = label;
            _oldState = oldState;
            _newState = newState;
        }

        public void Execute()
        {
            _label.RelativeX = _newState.RelativeX;
            _label.RelativeY = _newState.RelativeY;
            _label.RelativeWidth = _newState.RelativeWidth;
            _label.RelativeHeight = _newState.RelativeHeight;
        }

        public void Undo()
        {
            _label.RelativeX = _oldState.RelativeX;
            _label.RelativeY = _oldState.RelativeY;
            _label.RelativeWidth = _oldState.RelativeWidth;
            _label.RelativeHeight = _oldState.RelativeHeight;
        }
    }

    public class TransformBlurCommand : IUndoableCommand
    {
        private readonly BlurOverlay _blur;
        private readonly BlurTransformState _oldState;
        private readonly BlurTransformState _newState;

        public TransformBlurCommand(BlurOverlay blur, BlurTransformState oldState, BlurTransformState newState)
        {
            _blur = blur;
            _oldState = oldState;
            _newState = newState;
        }

        public void Execute()
        {
            _blur.RelativeX = _newState.RelativeX;
            _blur.RelativeY = _newState.RelativeY;
            _blur.RelativeWidth = _newState.RelativeWidth;
            _blur.RelativeHeight = _newState.RelativeHeight;
        }

        public void Undo()
        {
            _blur.RelativeX = _oldState.RelativeX;
            _blur.RelativeY = _oldState.RelativeY;
            _blur.RelativeWidth = _oldState.RelativeWidth;
            _blur.RelativeHeight = _oldState.RelativeHeight;
        }
    }

    public class TransformImageCommand : IUndoableCommand
    {
        private readonly MediaItem _item;
        private readonly ImageTransformState _oldState;
        private readonly ImageTransformState _newState;

        public TransformImageCommand(MediaItem item, ImageTransformState oldState, ImageTransformState newState)
        {
            _item = item;
            _oldState = oldState;
            _newState = newState;
        }

        public void Execute()
        {
            _item.PositionX = _newState.PositionX;
            _item.PositionY = _newState.PositionY;
            _item.Scale = _newState.Scale;
        }

        public void Undo()
        {
            _item.PositionX = _oldState.PositionX;
            _item.PositionY = _oldState.PositionY;
            _item.Scale = _oldState.Scale;
        }
    }
}