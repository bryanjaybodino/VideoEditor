using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    // Command for Deleting a Media Item
    public class DeleteMediaItemCommand : IUndoableCommand
    {
        private readonly List<MediaItem> mediaList;
        private readonly MediaItem itemToDelete;
        private readonly int originalIndex;

        public DeleteMediaItemCommand(List<MediaItem> mediaList, MediaItem itemToDelete)
        {
            this.mediaList = mediaList;
            this.itemToDelete = itemToDelete;
            this.originalIndex = mediaList.IndexOf(itemToDelete);
        }

        public void Execute()
        {
            if (mediaList.Contains(itemToDelete))
            {
                mediaList.Remove(itemToDelete);
            }
        }

        public void Undo()
        {
            if (!mediaList.Contains(itemToDelete))
            {
                if (originalIndex >= 0 && originalIndex <= mediaList.Count)
                    mediaList.Insert(originalIndex, itemToDelete);
                else
                    mediaList.Add(itemToDelete);
            }
        }
    }
}