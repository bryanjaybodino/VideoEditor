using System;
using System.Collections.Generic;
using System.Linq;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class SplitMediaItemCommand : IUndoableCommand
    {
        private readonly List<MediaItem> _list;
        private readonly MediaItem _originalItem;
        private readonly MediaItem _newItem;
        private readonly double _originalDuration;
        private readonly double _newOriginalDuration;
        private readonly float[] _originalAudioPeaks;

        public SplitMediaItemCommand(List<MediaItem> list, MediaItem originalItem, MediaItem newItem, double originalDuration)
        {
            _list = list;
            _originalItem = originalItem;
            _newItem = newItem;
            _originalDuration = originalDuration;
            _newOriginalDuration = originalItem.Duration;
            _originalAudioPeaks = originalItem.AudioPeaks;
        }

        public void Execute()
        {
            _originalItem.Duration = _newOriginalDuration;

            // Slice Audio Peaks array for Left (Original) and Right (New) items
            if (_originalAudioPeaks != null && _originalAudioPeaks.Length > 0)
            {
                double splitRatio = _newOriginalDuration / _originalDuration;
                int splitIndex = (int)(_originalAudioPeaks.Length * splitRatio);
                splitIndex = Math.Clamp(splitIndex, 1, _originalAudioPeaks.Length - 1);

                _originalItem.AudioPeaks = _originalAudioPeaks.Take(splitIndex).ToArray();
                _newItem.AudioPeaks = _originalAudioPeaks.Skip(splitIndex).ToArray();
            }

            if (!_list.Contains(_newItem))
            {
                _list.Add(_newItem);
            }
        }

        public void Undo()
        {
            _originalItem.Duration = _originalDuration;
            _originalItem.AudioPeaks = _originalAudioPeaks;
            _list.Remove(_newItem);
        }
    }
}