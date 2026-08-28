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
}