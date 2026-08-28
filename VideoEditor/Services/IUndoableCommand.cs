using System;
using System.Collections.Generic;

namespace VideoEditor.Services
{
    public interface IUndoableCommand
    {
        void Execute();
        void Undo();
    }

    public class UndoRedoManager
    {
        private readonly Stack<IUndoableCommand> undoStack = new Stack<IUndoableCommand>();
        private readonly Stack<IUndoableCommand> redoStack = new Stack<IUndoableCommand>();

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        public void ExecuteCommand(IUndoableCommand command)
        {
            command.Execute();
            undoStack.Push(command);
            redoStack.Clear(); // Clear forward redo history on new action
        }

        public void Undo()
        {
            if (CanUndo)
            {
                var command = undoStack.Pop();
                command.Undo();
                redoStack.Push(command);
            }
        }

        public void Redo()
        {
            if (CanRedo)
            {
                var command = redoStack.Pop();
                command.Execute();
                undoStack.Push(command);
            }
        }
    }
}