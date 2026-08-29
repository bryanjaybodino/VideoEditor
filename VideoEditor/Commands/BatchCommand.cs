using System.Collections.Generic;
using VideoEditor.Services;

namespace VideoEditor.Commands
{
    public class BatchCommand : IUndoableCommand
    {
        private readonly List<IUndoableCommand> _commands;

        public BatchCommand(List<IUndoableCommand> commands)
        {
            _commands = commands;
        }

        public void Execute()
        {
            foreach (var command in _commands)
            {
                command.Execute();
            }
        }

        public void Undo()
        {
            // Reverse loop ensures dependencies undo in exact opposite order
            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }
    }
}