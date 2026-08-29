using System.Collections.Generic;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class MoveClipCommand : IUndoableCommand
    {
        private class ClipState
        {
            public MediaItem Item { get; set; }
            public double OldStartTime { get; set; }
            public double NewStartTime { get; set; }
            public int OldTrackIndex { get; set; }
            public int NewTrackIndex { get; set; }
        }

        private readonly List<ClipState> _states = new List<ClipState>();

        // Constructor for single clip move
        public MoveClipCommand(MediaItem item, double oldStartTime, double newStartTime, int oldTrackIndex, int newTrackIndex)
        {
            _states.Add(new ClipState
            {
                Item = item,
                OldStartTime = oldStartTime,
                NewStartTime = newStartTime,
                OldTrackIndex = oldTrackIndex,
                NewTrackIndex = newTrackIndex
            });
        }

        // Constructor for multi-clip move
        public MoveClipCommand(IEnumerable<(MediaItem Item, double OldStart, double NewStart, int OldTrack, int NewTrack)> moves)
        {
            foreach (var move in moves)
            {
                _states.Add(new ClipState
                {
                    Item = move.Item,
                    OldStartTime = move.OldStart,
                    NewStartTime = move.NewStart,
                    OldTrackIndex = move.OldTrack,
                    NewTrackIndex = move.NewTrack
                });
            }
        }

        public void Execute()
        {
            foreach (var state in _states)
            {
                state.Item.StartTime = state.NewStartTime;
                state.Item.TrackIndex = state.NewTrackIndex;
            }
        }

        public void Undo()
        {
            foreach (var state in _states)
            {
                state.Item.StartTime = state.OldStartTime;
                state.Item.TrackIndex = state.OldTrackIndex;
            }
        }
    }
}