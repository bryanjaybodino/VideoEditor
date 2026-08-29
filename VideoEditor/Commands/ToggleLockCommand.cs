using System.Collections.Generic;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class ToggleLockCommand : IUndoableCommand
    {
        private readonly HashSet<int> _lockedTracks;
        private readonly int _trackIndex;

        public ToggleLockCommand(HashSet<int> lockedTracks, int trackIndex)
        {
            _lockedTracks = lockedTracks;
            _trackIndex = trackIndex;
        }

        public void Execute()
        {
            Toggle();
        }

        public void Undo()
        {
            Toggle();
        }

        private void Toggle()
        {
            if (_lockedTracks.Contains(_trackIndex))
            {
                _lockedTracks.Remove(_trackIndex);
            }
            else
            {
                _lockedTracks.Add(_trackIndex);
            }
        }
    }
}