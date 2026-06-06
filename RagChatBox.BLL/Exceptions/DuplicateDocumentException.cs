using System;

namespace RagChatBox.BLL.Exceptions
{
    public class DuplicateDocumentException : Exception
    {
        public DuplicateDocumentException(string message) : base(message)
        {
        }
    }
}
