using System;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class TransformCommand : IUndoableCommand
    {
        private readonly Action _applyOldState;
        private readonly Action _applyNewState;

        public TransformCommand(Action applyOldState, Action applyNewState)
        {
            _applyOldState = applyOldState;
            _applyNewState = applyNewState;
        }

        public void Execute() => _applyNewState();
        public void Undo() => _applyOldState();
    }
}