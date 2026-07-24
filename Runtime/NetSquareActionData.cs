using NetSquare.Core;

namespace NetSquare.Client
{
    /// <summary>
    /// Stores one callback and its related message for main-thread execution.
    /// </summary>
    public readonly struct NetSquareActionData
    {
        public readonly NetSquareAction Action;
        public readonly NetworkMessage Message;

        /// <summary>
        /// Creates one immutable dispatch item.
        /// </summary>
        /// <param name="action">Action executed on the Unity main thread.</param>
        /// <param name="message">Message supplied to the action.</param>
        public NetSquareActionData(NetSquareAction action, NetworkMessage message)
        {
            Action = action;
            Message = message;
        }

        /// <summary>
        /// Executes the stored action when one is present.
        /// </summary>
        public void Invoke()
        {
            // Null actions are accepted because NetSquare also uses the dispatcher as a main-thread signal.
            Action?.Invoke(Message);
        }
    }
}
