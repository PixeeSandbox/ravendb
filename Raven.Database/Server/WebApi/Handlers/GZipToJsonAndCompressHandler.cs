﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Raven.Database.Server.WebApi.Handlers
{
	public class GZipToJsonAndCompressHandler : DelegatingHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
															   CancellationToken cancellationToken)
		{
			HttpResponseMessage response;
			// Handle only if content type is 'application/gzip'
			if (request.Content.Headers.ContentEncoding == null ||
				request.Content.Headers.ContentEncoding.Contains("gzip") == false)
			{
				response = await base.SendAsync(request, cancellationToken);
				return Compress(response);
			}

			// Read in the input stream, then decompress in to the outputstream.
			// Doing this asynronously, but not really required at this point
			// since we end up waiting on it right after this.
			Stream outputStream = new MemoryStream();
			await request.Content.ReadAsStreamAsync().ContinueWith(t =>
			{
				Stream inputStream = t.Result;
				var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);

				gzipStream.CopyTo(outputStream);
				gzipStream.Dispose();

				outputStream.Seek(0, SeekOrigin.Begin);
			}, cancellationToken);

			// This next section is the key...

			// Save the original content
			HttpContent origContent = request.Content;

			// Replace request content with the newly decompressed stream
			request.Content = new StreamContent(outputStream);

			// Copy all headers from original content in to new one
			foreach (var header in origContent.Headers)
			{
				foreach (var val in header.Value)
				{
					request.Content.Headers.Add(header.Key, val);
				}
			}

			// Replace the original content-type with content type
			// of decompressed data. In our case, we can assume application/json. A
			// more generic and reuseable handler would need some other 
			// way to differentiate the decompressed content type.
			request.Content.Headers.Remove("Content-Type");
			request.Content.Headers.Add("Content-Type", "application/json");

			response = await base.SendAsync(request, cancellationToken);
			return Compress(response);
		}

		public HttpResponseMessage Compress(HttpResponseMessage response)
		{
			if (response.RequestMessage != null && response.RequestMessage.Headers.AcceptEncoding != null && response.RequestMessage.Headers.AcceptEncoding.Count != 0)
			{
				string encodingType = response.RequestMessage.Headers.AcceptEncoding.First().Value;

				response.Content = new CompressedContent(response.Content, encodingType);
			}

			return response;
		}

		public class CompressedContent : HttpContent
		{
			private HttpContent originalContent;
			private string encodingType;

			public CompressedContent(HttpContent content, string encodingType)
			{
				if (content == null)
				{
					throw new ArgumentNullException("content");
				}

				if (encodingType == null)
				{
					throw new ArgumentNullException("encodingType");
				}

				originalContent = content;
				this.encodingType = encodingType.ToLowerInvariant();

				if (this.encodingType != "gzip" && this.encodingType != "deflate")
				{
					throw new InvalidOperationException(string.Format("Encoding '{0}' is not supported. Only supports gzip or deflate encoding.", this.encodingType));
				}

				// copy the headers from the original content
				foreach (KeyValuePair<string, IEnumerable<string>> header in originalContent.Headers)
				{
					Headers.TryAddWithoutValidation(header.Key, header.Value);
				}

				Headers.ContentEncoding.Add(encodingType);
			}

			protected override bool TryComputeLength(out long length)
			{
				length = -1;

				return false;
			}

			protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
			{
				Stream compressedStream = null;

				if (encodingType == "gzip")
				{
					compressedStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
				}
				else if (encodingType == "deflate")
				{
					compressedStream = new DeflateStream(stream, CompressionMode.Compress, leaveOpen: true);
				}

				return originalContent.CopyToAsync(compressedStream).ContinueWith(tsk =>
				{
					if (compressedStream != null)
					{
						compressedStream.Dispose();
					}
				});
			}
		}
	}
}
