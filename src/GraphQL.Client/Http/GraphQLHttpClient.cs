using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Client.Http.Internal;
using GraphQL.Common.Request;
using GraphQL.Common.Response;

namespace GraphQL.Client.Http {

	/// <summary>
	/// A Client to access GraphQL EndPoints
	/// </summary>
	public partial class GraphQLHttpClient : IGraphQLClient {

		#region Properties

		/// <summary>
		/// Gets the headers which should be sent with each request.
		/// </summary>
		public HttpRequestHeaders DefaultRequestHeaders => this.HttpClient.DefaultRequestHeaders;

		/// <summary>
		/// the instance of <see cref="HttpClient"/> which is used internally
		/// </summary>
		public HttpClient HttpClient => this.graphQLHttpHandler.HttpClient;

		/// <summary>
		/// The GraphQL EndPoint to be used
		/// </summary>
		public Uri EndPoint {
			get => this.Options.EndPoint;
			set => this.Options.EndPoint = value;
		}

		/// <summary>
		/// The Options	to be used
		/// </summary>
		public GraphQLHttpClientOptions Options {
			get => this.graphQLHttpHandler.Options;
			set => this.graphQLHttpHandler.Options = value;
		}

		/// <inheritdoc />
		[Obsolete("EXPERIMENTAL")]
		public IObservable<Exception> WebSocketReceiveErrors => graphQlHttpWebSocket.ReceiveErrors;

		#endregion

		internal readonly GraphQLHttpHandler graphQLHttpHandler;
		internal readonly GraphQLHttpWebSocket graphQlHttpWebSocket;
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		/// <summary>
		/// Initializes a new instance
		/// </summary>
		/// <param name="endPoint">The EndPoint to be used</param>
		public GraphQLHttpClient(string endPoint) : this(new Uri(endPoint)) { }

		/// <summary>
		/// Initializes a new instance
		/// </summary>
		/// <param name="endPoint">The EndPoint to be used</param>
		public GraphQLHttpClient(Uri endPoint) : this(new GraphQLHttpClientOptions { EndPoint = endPoint }) { }

		/// <summary>
		/// Initializes a new instance
		/// </summary>
		/// <param name="endPoint">The EndPoint to be used</param>
		/// <param name="options">The Options to be used</param>
		public GraphQLHttpClient(string endPoint, GraphQLHttpClientOptions options) : this(new Uri(endPoint), options) { }

		/// <summary>
		/// Initializes a new instance
		/// </summary>
		/// <param name="endPoint">The EndPoint to be used</param>
		/// <param name="options">The Options to be used</param>
		public GraphQLHttpClient(Uri endPoint, GraphQLHttpClientOptions options) {

			options.EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));

			if (options == null) { throw new ArgumentNullException(nameof(options)); }
			if (options.JsonSerializerSettings == null) { throw new ArgumentNullException(nameof(options.JsonSerializerSettings)); }
			if (options.HttpMessageHandler == null) { throw new ArgumentNullException(nameof(options.HttpMessageHandler)); }
			if (options.MediaType == null) { throw new ArgumentNullException(nameof(options.MediaType)); }

			this.graphQLHttpHandler = new GraphQLHttpHandler(options);
			this.graphQlHttpWebSocket = new GraphQLHttpWebSocket(_getWebSocketUri(), options);
		}

		/// <summary>
		/// Initializes a new instance
		/// </summary>
		/// <param name="options">The Options to be used</param>
		public GraphQLHttpClient(GraphQLHttpClientOptions options) {
			if (options == null) { throw new ArgumentNullException(nameof(options)); }
			if (options.EndPoint == null) { throw new ArgumentNullException(nameof(options.EndPoint)); }
			if (options.JsonSerializerSettings == null) { throw new ArgumentNullException(nameof(options.JsonSerializerSettings)); }
			if (options.HttpMessageHandler == null) { throw new ArgumentNullException(nameof(options.HttpMessageHandler)); }
			if (options.MediaType == null) { throw new ArgumentNullException(nameof(options.MediaType)); }

			this.graphQLHttpHandler = new GraphQLHttpHandler(options);
			this.graphQlHttpWebSocket = new GraphQLHttpWebSocket(_getWebSocketUri(), options);
		}

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="options">The Options to be used</param>
		/// <param name="httpClient">the <see cref="HttpClient"/> to be used</param>
		public GraphQLHttpClient(GraphQLHttpClientOptions options, HttpClient httpClient) {
			if (options == null) { throw new ArgumentNullException(nameof(options)); }
			if (options.EndPoint == null) { throw new ArgumentNullException(nameof(options.EndPoint)); }
			if (options.JsonSerializerSettings == null) { throw new ArgumentNullException(nameof(options.JsonSerializerSettings)); }
			if (options.HttpMessageHandler == null) { throw new ArgumentNullException(nameof(options.HttpMessageHandler)); }
			if (options.MediaType == null) { throw new ArgumentNullException(nameof(options.MediaType)); }

			this.graphQLHttpHandler = new GraphQLHttpHandler(options, httpClient);
			this.graphQlHttpWebSocket = new GraphQLHttpWebSocket(_getWebSocketUri(), options);
		}

		public Task<GraphQLResponse> SendQueryAsync(string query, CancellationToken cancellationToken = default) =>
			this.SendQueryAsync(new GraphQLRequest(query), cancellationToken);

		public Task<GraphQLResponse> SendQueryAsync(GraphQLRequest request, CancellationToken cancellationToken = default)
		{
			return Options.UseWebSocketForQueriesAndMutations
				? this.graphQlHttpWebSocket.Request(request, cancellationToken)
				: this.graphQLHttpHandler.PostAsync(request, cancellationToken);
		}

		public Task<GraphQLResponse> SendMutationAsync(string query, CancellationToken cancellationToken = default) =>
			this.SendMutationAsync(new GraphQLRequest(query), cancellationToken);

		public Task<GraphQLResponse> SendMutationAsync(GraphQLRequest request, CancellationToken cancellationToken = default)
		{
			return Options.UseWebSocketForQueriesAndMutations
				? this.graphQlHttpWebSocket.Request(request, cancellationToken)
				: this.graphQLHttpHandler.PostAsync(request, cancellationToken);
		}

		[Obsolete("EXPERIMENTAL API")]
		public Task<IGraphQLSubscriptionResult> SendSubscribeAsync(string query, CancellationToken cancellationToken = default) =>
			this.SendSubscribeAsync(new GraphQLRequest(query), cancellationToken);

		[Obsolete("EXPERIMENTAL API")]
		public Task<IGraphQLSubscriptionResult> SendSubscribeAsync(GraphQLRequest request, CancellationToken cancellationToken = default)
		{
			GraphQLHttpSubscriptionResult graphQLSubscriptionResult = _createSubscription(request, cancellationToken);
			return Task.FromResult<IGraphQLSubscriptionResult>(graphQLSubscriptionResult);
		}

		private GraphQLHttpSubscriptionResult _createSubscription(GraphQLRequest request, CancellationToken cancellationToken)
		{
			if (request == null) { throw new ArgumentNullException(nameof(request)); }
			if (request.Query == null) { throw new ArgumentNullException(nameof(request.Query)); }

			var graphQLSubscriptionResult = new GraphQLHttpSubscriptionResult(_getWebSocketUri(), request);
			graphQLSubscriptionResult.StartAsync(cancellationToken);
			return graphQLSubscriptionResult;
		}

		private Uri _getWebSocketUri()
		{
			var webSocketSchema = this.EndPoint.Scheme == "https" ? "wss" : "ws";
			return new Uri($"{webSocketSchema}://{this.EndPoint.Host}:{this.EndPoint.Port}{this.EndPoint.AbsolutePath}");
		}

		/// <inheritdoc />
		[Obsolete("EXPERIMENTAL API")]
		public IObservable<GraphQLResponse> CreateSubscriptionStream(GraphQLRequest request)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(GraphQLHttpClient));

			if (subscriptionStreams.ContainsKey(request))
				return subscriptionStreams[request];

			var observable = graphQlHttpWebSocket.CreateSubscriptionStream(request, Options, cancellationToken: _cancellationTokenSource.Token);

			subscriptionStreams.TryAdd(request, observable);
			return observable;
		}

		/// <inheritdoc />
		[Obsolete("EXPERIMENTAL API")]
		public IObservable<GraphQLResponse> CreateSubscriptionStream(GraphQLRequest request, Action<WebSocketException> webSocketExceptionHandler)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(GraphQLHttpClient));
					   
			return CreateSubscriptionStream(request, e =>
			{
				if (e is WebSocketException webSocketException)
					webSocketExceptionHandler(webSocketException);
				else
					throw e;
			});
		}

		/// <inheritdoc />
		[Obsolete("EXPERIMENTAL API")]
		public IObservable<GraphQLResponse> CreateSubscriptionStream(GraphQLRequest request, Action<Exception> exceptionHandler)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(GraphQLHttpClient));

			if(subscriptionStreams.ContainsKey(request))
				return subscriptionStreams[request];

			var observable = graphQlHttpWebSocket.CreateSubscriptionStream(request, Options, exceptionHandler, _cancellationTokenSource.Token);
			subscriptionStreams.TryAdd(request, observable);
			return observable;
		}

		private ConcurrentDictionary<GraphQLRequest, IObservable<GraphQLResponse>> subscriptionStreams = new ConcurrentDictionary<GraphQLRequest, IObservable<GraphQLResponse>>();

		/// <summary>
		/// Releases unmanaged resources
		/// </summary>
		public void Dispose()
		{
			lock (_disposeLocker)
			{
				if (!_disposed)
				{
					_dispose();
				}
			}
		}

		private bool _disposed = false;
		private object _disposeLocker = new object();

		private void _dispose()
		{
			_disposed = true;
			this.graphQLHttpHandler.Dispose();
			this.graphQlHttpWebSocket.Dispose();
			_cancellationTokenSource.Cancel();
			_cancellationTokenSource.Dispose();
		}
	}

}
