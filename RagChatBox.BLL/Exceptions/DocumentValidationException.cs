using System;

namespace RagChatBox.BLL.Exceptions
{
    public class DocumentValidationException : Exception
    {
        public DocumentValidationException(string message) : base(message)
        {
        }
    }
}
