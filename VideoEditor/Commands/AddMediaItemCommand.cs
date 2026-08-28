using System.Collections.Generic;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    // Command for Adding Media Items
    public class AddMediaItemCommand : IUndoableCommand
    {
        private readonly List<MediaItem> _list;
        private readonly MediaItem _item;

        public AddMediaItemCommand(List<MediaItem> list, MediaItem item)
        {
            _list = list;
            _item = item;
        }

        public void Execute() => _list.Add(_item);
        public void Undo() => _list.Remove(_item);
    }

    // Command for Splitting Media Items
    public class SplitMediaItemCommand : IUndoableCommand
    {
        private readonly List<MediaItem> _list;
        private readonly MediaItem _originalItem;
        private readonly MediaItem _newItem;
        private readonly double _originalDuration;
        private readonly double _newOriginalDuration;

        public SplitMediaItemCommand(List<MediaItem> list, MediaItem originalItem, MediaItem newItem, double originalDuration)
        {
            _list = list;
            _originalItem = originalItem;
            _newItem = newItem;
            _originalDuration = originalDuration;
            _newOriginalDuration = originalItem.Duration;
        }

        public void Execute()
        {
            _originalItem.Duration = _newOriginalDuration;
            if (!_list.Contains(_newItem))
            {
                _list.Add(_newItem);
            }
        }

        public void Undo()
        {
            _originalItem.Duration = _originalDuration;
            _list.Remove(_newItem);
        }
    }
}