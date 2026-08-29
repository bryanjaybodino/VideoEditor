using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class MoveClipCommand : IUndoableCommand
    {
        private readonly MediaItem _item;
        private readonly double _oldStartTime;
        private readonly double _newStartTime;
        private readonly int _oldTrackIndex;
        private readonly int _newTrackIndex;

        public MoveClipCommand(MediaItem item, double oldStartTime, double newStartTime, int oldTrackIndex, int newTrackIndex)
        {
            _item = item;
            _oldStartTime = oldStartTime;
            _newStartTime = newStartTime;
            _oldTrackIndex = oldTrackIndex;
            _newTrackIndex = newTrackIndex;
        }

        public void Execute()
        {
            _item.StartTime = _newStartTime;
            _item.TrackIndex = _newTrackIndex;
        }

        public void Undo()
        {
            _item.StartTime = _oldStartTime;
            _item.TrackIndex = _oldTrackIndex;
        }
    }
}