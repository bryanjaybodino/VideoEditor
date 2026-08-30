using System.Drawing;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class ChangeTextPropertyCommand : IUndoableCommand
    {
        private readonly TextLabel _label;
        private readonly Action<TextLabel> _applyAction;
        private readonly Action<TextLabel> _undoAction;

        public ChangeTextPropertyCommand(TextLabel label, Action<TextLabel> applyAction, Action<TextLabel> undoAction)
        {
            _label = label;
            _applyAction = applyAction;
            _undoAction = undoAction;
        }

        public void Execute()
        {
            if (_label != null)
            {
                _applyAction(_label);
            }
        }

        public void Undo()
        {
            if (_label != null)
            {
                _undoAction(_label);
            }
        }
    }
}