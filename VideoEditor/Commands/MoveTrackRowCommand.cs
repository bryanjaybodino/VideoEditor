using System.Collections.Generic;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class MoveTrackRowCommand : IUndoableCommand
    {
        private readonly List<MediaItem> _items;
        private readonly int _fromTrackIndex;
        private readonly int _toTrackIndex;

        public MoveTrackRowCommand(List<MediaItem> items, int fromTrackIndex, int toTrackIndex)
        {
            _items = items;
            _fromTrackIndex = fromTrackIndex;
            _toTrackIndex = toTrackIndex;
        }

        public void Execute()
        {
            foreach (var item in _items)
            {
                if (item.TrackIndex == _fromTrackIndex)
                    item.TrackIndex = _toTrackIndex;
                else if (_fromTrackIndex < _toTrackIndex && item.TrackIndex > _fromTrackIndex && item.TrackIndex <= _toTrackIndex)
                    item.TrackIndex--;
                else if (_fromTrackIndex > _toTrackIndex && item.TrackIndex >= _toTrackIndex && item.TrackIndex < _fromTrackIndex)
                    item.TrackIndex++;
            }
        }

        public void Undo()
        {
            foreach (var item in _items)
            {
                if (item.TrackIndex == _toTrackIndex)
                    item.TrackIndex = _fromTrackIndex;
                else if (_fromTrackIndex < _toTrackIndex && item.TrackIndex >= _fromTrackIndex && item.TrackIndex < _toTrackIndex)
                    item.TrackIndex++;
                else if (_fromTrackIndex > _toTrackIndex && item.TrackIndex > _toTrackIndex && item.TrackIndex <= _fromTrackIndex)
                    item.TrackIndex--;
            }
        }
    }
}