﻿using System.Threading.Tasks;
using GraphQL.Attachments;

namespace GraphQL
{
    /// <summary>
    /// Extensions to GraphQL to enable Attachments.
    /// </summary>
    public static class AttachmentsExtensions
    {
        /// <summary>
        /// Executes a GraphQL query and makes attachments available.
        /// </summary>
        public static async Task<AttachmentExecutionResult> ExecuteWithAttachments(
            this IDocumentExecuter executer,
            ExecutionOptions options,
            IIncomingAttachments? attachments = null)
        {
            Guard.AgainstNull(nameof(executer), executer);
            Guard.AgainstNull(nameof(options), options);
            await using var attachmentContext = BuildAttachmentContext(attachments);
            options.SetAttachmentContext(attachmentContext);
            var result = await executer.ExecuteAsync(options);
            return new AttachmentExecutionResult(result, attachmentContext.Outgoing);
        }

        static AttachmentContext BuildAttachmentContext(IIncomingAttachments? incoming)
        {
            if (incoming == null)
            {
                return new AttachmentContext();
            }

            return new AttachmentContext(incoming);
        }
    }
}