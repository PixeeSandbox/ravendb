﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Document;
using Raven.Tests.Bugs;
using Raven.Tests.Common;
using Xunit;

namespace Raven.Tests.Issues
{
	public class RavenDb1962 : RavenTest
	{
		private const int Cntr = 5;

		[Fact]
		public async Task CanExecuteLazyLoadsInAsyncSession()
		{
			using (var store = NewRemoteDocumentStore())
			{
				using (var session = store.OpenAsyncSession())
				{
					await StoreDataAsync(store, session);

					var userFetchTasks = LazyLoadAsync(store, session);
					var i = 1;
					foreach (var lazy in userFetchTasks)
					{
						var user = await lazy.Value;
						Assert.Equal(user.Name, "Test User #" + i);
						i++;
					}
				}
			}
		}

		[Fact]
		public async Task CanExecuteLazyLoadsInAsyncSession_CheckSingleCall()
		{
			using (var store = NewRemoteDocumentStore())
			{
				using (var session = store.OpenAsyncSession())
				{
					await StoreDataAsync(store, session);
				}

				using (var session = store.OpenAsyncSession())
				{
					
					LazyLoadAsync(store, session);

					var requestTimes = await session.Advanced.Eagerly.ExecuteAllPendingLazyOperationsAsync();
					Assert.Equal(1, session.Advanced.NumberOfRequests); 
					Assert.NotNull(requestTimes.TotalClientDuration);
					Assert.NotNull(requestTimes.TotalServerDuration);
					Assert.Equal(Cntr, requestTimes.DurationBreakdown.Count);
				}
			}
		}


		public async Task StoreDataAsync(DocumentStore store, IAsyncDocumentSession session)
		{
			for (var i = 1; i <= Cntr; i++)
			{
				await session.StoreAsync(new User {Name = "Test User #" + i}, "users/" + i);
			}
			await session.SaveChangesAsync();
		}

		public List<Lazy<Task<User>>> LazyLoadAsync(DocumentStore store, IAsyncDocumentSession session)
		{
			var listTasks = new List<Lazy<Task<User>>>();
			for (var i = 1; i <= Cntr; i++)
			{
				var userFetchTask = session.Advanced.Lazily.LoadAsync<User>("users/" + i);

				listTasks.Add(userFetchTask);
			}
			return listTasks;
		}
	}
}