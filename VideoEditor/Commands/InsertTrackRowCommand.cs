using System.Collections.Generic;
using System.Linq;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class InsertTrackRowCommand : IUndoableCommand
    {
        private readonly List<MediaItem> _items;
        private readonly HashSet<int> _lockedTracks;
        private readonly int _targetTrackIndex;

        public InsertTrackRowCommand(List<MediaItem> items, HashSet<int> lockedTracks, int targetTrackIndex)
        {
            _items = items;
            _lockedTracks = lockedTracks;
            _targetTrackIndex = targetTrackIndex;
        }

        public void Execute()
        {
            foreach (var item in _items.Where(x => x.Type != MediaType.Audio))
            {
                if (item.TrackIndex >= _targetTrackIndex)
                {
                    item.TrackIndex++;
                }
            }

            var updatedLocks = new HashSet<int>();
            foreach (var locked in _lockedTracks)
            {
                updatedLocks.Add(locked >= _targetTrackIndex ? locked + 1 : locked);
            }

            _lockedTracks.Clear();
            foreach (var lockIndex in updatedLocks)
            {
                _lockedTracks.Add(lockIndex);
            }
        }

        public void Undo()
        {
            foreach (var item in _items.Where(x => x.Type != MediaType.Audio))
            {
                if (item.TrackIndex > _targetTrackIndex)
                {
                    item.TrackIndex--;
                }
            }

            var updatedLocks = new HashSet<int>();
            foreach (var locked in _lockedTracks)
            {
                updatedLocks.Add(locked > _targetTrackIndex ? locked - 1 : locked);
            }

            _lockedTracks.Clear();
            foreach (var lockIndex in updatedLocks)
            {
                _lockedTracks.Add(lockIndex);
            }
        }
    }

    public class DeleteTrackRowCommand : IUndoableCommand
    {
        private readonly List<MediaItem> _items;
        private readonly HashSet<int> _lockedTracks;
        private readonly int _targetTrackIndex;
        private readonly bool _wasLocked;

        public DeleteTrackRowCommand(List<MediaItem> items, HashSet<int> lockedTracks, int targetTrackIndex)
        {
            _items = items;
            _lockedTracks = lockedTracks;
            _targetTrackIndex = targetTrackIndex;
            _wasLocked = lockedTracks.Contains(targetTrackIndex);
        }

        public void Execute()
        {
            foreach (var item in _items.Where(x => x.Type != MediaType.Audio))
            {
                if (item.TrackIndex > _targetTrackIndex)
                {
                    item.TrackIndex--;
                }
            }

            _lockedTracks.Remove(_targetTrackIndex);
            var updatedLocks = new HashSet<int>();
            foreach (var locked in _lockedTracks)
            {
                updatedLocks.Add(locked > _targetTrackIndex ? locked - 1 : locked);
            }

            _lockedTracks.Clear();
            foreach (var lockIndex in updatedLocks)
            {
                _lockedTracks.Add(lockIndex);
            }
        }

        public void Undo()
        {
            foreach (var item in _items.Where(x => x.Type != MediaType.Audio))
            {
                if (item.TrackIndex >= _targetTrackIndex)
                {
                    item.TrackIndex++;
                }
            }

            var updatedLocks = new HashSet<int>();
            foreach (var locked in _lockedTracks)
            {
                updatedLocks.Add(locked >= _targetTrackIndex ? locked + 1 : locked);
            }

            _lockedTracks.Clear();
            foreach (var lockIndex in updatedLocks)
            {
                _lockedTracks.Add(lockIndex);
            }

            if (_wasLocked)
            {
                _lockedTracks.Add(_targetTrackIndex);
            }
        }
    }
}