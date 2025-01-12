﻿using System;
using System.Net.Http;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Backups;
using Raven.Client.Http;
using Raven.Client.Json;
using Raven.Client.Json.Serialization;
using Sparrow.Json;

namespace Raven.Client.ServerWide.Operations
{
    public sealed class RestoreBackupOperation : IServerOperation<OperationIdResult>
    {
        private readonly RestoreBackupConfigurationBase _restoreConfiguration;
        public string NodeTag;

        public RestoreBackupOperation(RestoreBackupConfigurationBase restoreConfiguration)
        {
            _restoreConfiguration = restoreConfiguration;
        }

        public RestoreBackupOperation(RestoreBackupConfigurationBase restoreConfiguration, string nodeTag)
        {
            _restoreConfiguration = restoreConfiguration;
            NodeTag = nodeTag;
        }

        public RavenCommand<OperationIdResult> GetCommand(DocumentConventions conventions, JsonOperationContext ctx)
        {
            return new RestoreBackupCommand(conventions, _restoreConfiguration, NodeTag);
        }

        internal sealed class RestoreBackupCommand : RavenCommand<OperationIdResult>
        {
            public override bool IsReadRequest => false;
            private readonly DocumentConventions _conventions;
            private readonly RestoreBackupConfigurationBase _restoreConfiguration;

            public RestoreBackupCommand(DocumentConventions conventions, RestoreBackupConfigurationBase restoreConfiguration, string nodeTag = null)
            {
                _conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
                _restoreConfiguration = restoreConfiguration ?? throw new ArgumentNullException(nameof(restoreConfiguration));
                SelectedNodeTag = nodeTag;
            }

            public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
            {
                url = $"{node.Url}/admin/restore/database";

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    Content = new BlittableJsonContent(async stream =>
                    {
                        var config = DocumentConventions.Default.Serialization.DefaultConverter.ToBlittable(_restoreConfiguration, ctx);
                        await ctx.WriteAsync(stream, config).ConfigureAwait(false);
                    }, _conventions)
                };

                return request;
            }

            public override void SetResponse(JsonOperationContext context, BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                Result = JsonDeserializationClient.OperationIdResult(response);
            }
        }
    }
}
