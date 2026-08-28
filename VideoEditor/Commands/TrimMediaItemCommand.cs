using System.Collections.Generic;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    // Command for Split Left / Split Right (Trimming)
    public class TrimMediaItemCommand : IUndoableCommand
    {
        private readonly MediaItem _item;
        private readonly double _oldStartTime, _newStartTime;
        private readonly double _oldDuration, _newDuration;
        private readonly double _oldSourceOffset, _newSourceOffset;

        public TrimMediaItemCommand(
            MediaItem item,
            double oldStartTime, double newStartTime,
            double oldDuration, double newDuration,
            double oldSourceOffset, double newSourceOffset)
        {
            _item = item;
            _oldStartTime = oldStartTime;
            _newStartTime = newStartTime;
            _oldDuration = oldDuration;
            _newDuration = newDuration;
            _oldSourceOffset = oldSourceOffset;
            _newSourceOffset = newSourceOffset;
        }

        public void Execute()
        {
            _item.StartTime = _newStartTime;
            _item.Duration = _newDuration;
            _item.SourceOffset = _newSourceOffset;
        }

        public void Undo()
        {
            _item.StartTime = _oldStartTime;
            _item.Duration = _oldDuration;
            _item.SourceOffset = _oldSourceOffset;
        }
    }

    // Command for Resizing / Changing Duration
    public class ChangeDurationCommand : IUndoableCommand
    {
        private readonly MediaItem _item;
        private readonly double _oldDuration;
        private readonly double _newDuration;

        public ChangeDurationCommand(MediaItem item, double oldDuration, double newDuration)
        {
            _item = item;
            _oldDuration = oldDuration;
            _newDuration = newDuration;
        }

        public void Execute() => _item.Duration = _newDuration;
        public void Undo() => _item.Duration = _oldDuration;
    }
}