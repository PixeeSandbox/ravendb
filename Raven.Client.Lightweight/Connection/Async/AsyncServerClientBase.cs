﻿using Raven.Abstractions.Connection;
using Raven.Abstractions.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Raven.Client.Connection.Async
{
    public abstract class AsyncServerClientBase<TConvention, TReplicationInformer> : IDisposalNotification 
        where TConvention : Convention
        where TReplicationInformer : IReplicationInformerBase
    {
        public AsyncServerClientBase(string serverUrl, TConvention convention, OperationCredentials credentials, HttpJsonRequestFactory jsonRequestFactory, 
                                     Guid? sessionId, NameValueCollection operationsHeaders)
        {
            this.WasDisposed = false;

            this.ServerUrl = serverUrl.TrimEnd('/'); 
            this.Convention = convention;
            this.CredentialsThatShouldBeUsedOnlyInOperationsWithoutReplication = credentials;
            this.RequestFactory = jsonRequestFactory;
            this.SessionId = sessionId;
            this.OperationsHeaders = operationsHeaders;
            this._replicationInformer = new Lazy<TReplicationInformer>(() => GetReplicationInformer(), true);
            this.readStrippingBase = new Lazy<int>(() => this.ReplicationInformer.GetReadStripingBase(), true);

            this.MaxQuerySizeForGetRequest = 8 * 1024;
        }

        public virtual int MaxQuerySizeForGetRequest
        {
            get;
            set;
        }

        public virtual string ServerUrl
        {
            get; protected set;
        }

        public abstract string BaseUrl
        {
            get;
        }

        public TConvention Convention
        {
            get;
            private set;
        }

        protected Guid? SessionId
        {
            get;
            private set;
        }

        public HttpJsonRequestFactory RequestFactory
        {
            get;
            private set;
        }

        protected OperationCredentials CredentialsThatShouldBeUsedOnlyInOperationsWithoutReplication
        {
            get; set;
        }

        public virtual OperationCredentials PrimaryCredentials
        {
            get { return CredentialsThatShouldBeUsedOnlyInOperationsWithoutReplication; }
        }

        public virtual NameValueCollection OperationsHeaders
        {
            get;
            set;
        }

        #region Execute with replication

        protected abstract TReplicationInformer GetReplicationInformer();


        private readonly Lazy<TReplicationInformer> _replicationInformer;

        /// <summary>
        /// Allow access to the replication informer used to determine how we replicate requests
        /// </summary>
        public TReplicationInformer ReplicationInformer { get { return this._replicationInformer.Value; } }

        private readonly Lazy<int> readStrippingBase;
        private int requestCount;
        private volatile bool currentlyExecuting;

        internal async Task<T> ExecuteWithReplication<T>(string method, Func<OperationMetadata, Task<T>> operation, Func<string> baseUrlGetter = null)
        {
            var currentRequest = Interlocked.Increment(ref requestCount);
            if (currentlyExecuting && Convention.AllowMultipuleAsyncOperations == false)
                throw new InvalidOperationException("Only a single concurrent async request is allowed per async client instance.");

            currentlyExecuting = true;
            try
            {
                string operationUrl = this.BaseUrl;
                if (baseUrlGetter != null)
                    operationUrl = baseUrlGetter();

                return await ReplicationInformer.ExecuteWithReplicationAsync(method, operationUrl, this.CredentialsThatShouldBeUsedOnlyInOperationsWithoutReplication, currentRequest, readStrippingBase.Value, operation)
                                                .ConfigureAwait(false);
            }
            catch (AggregateException e)
            {
                var singleException = e.ExtractSingleInnerException();
                if (singleException != null)
                    throw singleException;

                throw;
            }
            finally
            {
                currentlyExecuting = false;
            }
        }


        internal Task ExecuteWithReplication(string method, Func<OperationMetadata, Task> operation, Func<string> baseUrlGetter = null)
        {
            // Convert the Func<string, Task> to a Func<string, Task<object>>
            return ExecuteWithReplication(method, u => operation(u).ContinueWith<object>(t =>
            {
                t.AssertNotFailed();
                return null;
            }), baseUrlGetter);
        }

        #endregion

        #region IDisposalNotification

        public event EventHandler AfterDispose = (sender, args) => { };

        public bool WasDisposed { get; protected set; }

        public virtual void Dispose()
        {
            WasDisposed = true;
            AfterDispose(this, EventArgs.Empty);
        }

        #endregion
    }
}
