using System;
using OAuth;
using System.Net;
using System.Web;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Runtime.Serialization;
using SimpleJson;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Toopher
{
	/// <summary>
	/// DotNet helper library to generate Toopher iFrame requests and validate responses.
	/// Register at https://dev.toopher.com to get your Toopher Developer API Credentials.
	/// </summary>
	public class ToopherIframe
	{
		public const string IFRAME_VERSION = "2";
		public const long DEFAULT_TTL = 300L;
		public const string DEFAULT_BASE_URL = "https://api.toopher.com/v1/";

		private string baseUrl;
		private string consumerKey;
		private string consumerSecret;
		private static DateTime? dateOverride;

		public static void SetDateOverride (DateTime? dateOverride)
		{
			ToopherIframe.dateOverride = dateOverride;
		}

		private static DateTime GetDate ()
		{
			return dateOverride ?? DateTime.UtcNow;
		}

		/// <summary>
		/// Create an instance of the ToopherIframe helper for the specified API URL.
		/// </summary>
		/// <param name="consumerKey">Your Toopher API OAuth Consumer Key.</param>
		/// <param name="consumerSecret">Your Toopher API OAuth Consumer Secret.</param>
		/// <param name="baseUrl">The base URL of the Toopher API to target. If blank, the default is "https://api.toopher.com/v1/".</param>
		public ToopherIframe (string consumerKey, string consumerSecret, string baseUrl = null)
		{
			this.consumerKey = consumerKey;
			this.consumerSecret = consumerSecret;
			if (baseUrl != null) {
				this.baseUrl = baseUrl;
			} else {
				this.baseUrl = DEFAULT_BASE_URL;
			}
		}

		/// <summary>
		/// Generate a URL to retrieve a Toopher Authentication iFrame for a given user.
		/// </summary>
		/// <param name="userName">Unique name that identifies this user. This will be displayed to the user on their mobile device when they pair or authenticate.</param>
		/// <param name="resetEmail">Email adddress that the user has access to. In case the user has lost or cannot access their mobile device, Toopher will send a reset email to this address.</param>
		/// <param name="requestToken">Optional, can be empty. Toopher will include this token in the signed data returned with the iFrame response.</param>
		/// <param name="actionName">The name of the action to authenticate; will be shown to the user. If blank, the Toopher API will default the action to "Log In".</param>
		/// <param name="requesterMetadata">Optional, can be empty. Toopher will include this value in the signed data returned with the iFrame response.</param>
		/// <param name="extras">An optional Dictionary of extra parameters to provide to the API.</param>
		/// <returns>A string URL that can be used to retrieve the Authentication iFrame by the user's browser.</returns>
		public string GetAuthenticationUrl (string userName, string resetEmail, string requestToken, string actionName = "Log In", string requesterMetadata = "None", Dictionary<string, string> extras = null)
		{
			NameValueCollection parameters = new NameValueCollection ();
			long ttl;
			if (extras != null && extras.ContainsKey("ttl")) {
				ttl = Convert.ToInt64(extras["ttl"]);
				extras.Remove ("ttl");
			} else {
				ttl = DEFAULT_TTL;
			}

			parameters.Add ("v", IFRAME_VERSION);
			parameters.Add ("username", userName);
			parameters.Add ("reset_email", resetEmail);
			parameters.Add ("session_token", requestToken);
			parameters.Add ("action_name", actionName);
			parameters.Add ("requester_metadata", requesterMetadata);
			parameters.Add ("expires", (GetUnixEpochTimeInSeconds() + ttl).ToString());

			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}
			return GetOauthUrl (baseUrl + "web/authenticate", parameters);
		}

		/// <summary>
		/// Generate a URL to retrieve a Toopher Pairing iFrame for a given user.
		/// </summary>
		/// <param name="userName">Unique name that identifies this user. This will be displayed to the user on their mobile device when they pair or authenticate.</param>
		/// <param name="resetEmail">Email address that the user has access to. In case the user has lost or cannot access their mobile device, Toopher will send a reset email to this address.</param>
		/// <param name="extras">An optional Dictionary of extra parameters to provide to the API.</param>
		/// <returns>A string URL that can be used to retrieve the Pairing iFrame by the user's browser.</reeturns>
		public string GetUserManagementUrl (string userName, string resetEmail, Dictionary<string, string> extras = null)
		{
			NameValueCollection parameters = new NameValueCollection ();
			long ttl;
			if (extras != null && extras.ContainsKey("ttl")) {
				ttl = Convert.ToInt64(extras["ttl"]);
				extras.Remove ("ttl");
			} else {
				ttl = DEFAULT_TTL;
			}

			parameters.Add("v", IFRAME_VERSION);
			parameters.Add("username", userName);
			parameters.Add("reset_email", resetEmail);
			parameters.Add ("expires", (GetUnixEpochTimeInSeconds() + ttl).ToString());

			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}
			return GetOauthUrl (baseUrl + "web/manage_user", parameters);
		}

		/// <summary>
		/// Verify the authenticity of data returned from the Toopher iFrame by validating the crytographic signature.
		/// </summary>
		/// <param name="params">The data returned from the iFrame.</param>
		/// <param name="sessionToken">The session token</param>
		/// <param name="ttl">Time-To-Live (seconds) to enforce on the Toopher API signature. This value sets the maximum duration between the Toopher API creating the signature and the signature being validated on your server.</param>
		/// <returns>A Dictionary of the validated data if the signature is valid, or null if the signature is invalid.</returns>
		public Dictionary<string, string> ValidatePostback (Dictionary<string, string[]> parameters, string sessionToken, long ttl)
		{
			try {
				List<string> missingKeys = new List<string> ();
				Dictionary<string, string> data = new Dictionary<string, string>();

				foreach (var entry in parameters)
				{
					if (entry.Value.Length > 0) {
						data.Add(entry.Key, entry.Value[0]);
					}
				}

				if (!data.ContainsKey("toopher_sig")) {
					missingKeys.Add("toopher_sig");
				}
				if (!data.ContainsKey("timestamp")) {
					missingKeys.Add("timestamp");
				}
				if (!data.ContainsKey("session_token")) {
					missingKeys.Add("session_token");
				}
				if (missingKeys.Count() > 0) {
					var keys = string.Join(", ", missingKeys.ToArray());
					throw new SignatureValidationError ("Missing required keys: " + keys);
				}

				if (data["session_token"] != sessionToken) {
					throw new SignatureValidationError ("Session token does not match expected value");
				}

				string maybeSignature = data["toopher_sig"];
				data.Remove("toopher_sig");
				var signatureValid = false;
				try {
					var computedSig = Signature(consumerSecret, maybeSignature, data);
					signatureValid = computedSig == maybeSignature;
				} catch (Exception e) {
					signatureValid = false;
				}

				if (!signatureValid) {
					throw new SignatureValidationError ("Computed signature does not match");
				}

				var ttlValid = (int)(GetUnixEpochTimeInSeconds () - ttl) < Int32.Parse(data["timestamp"]);
				if (!ttlValid) {
					throw new SignatureValidationError ("TTL Expired");
				}

				return data;
			} catch (Exception e) {
				throw new SignatureValidationError ("Exception while validating toopher signature: " + e);
			}
		}

		private string GetOauthUrl (string url, NameValueCollection parameters)
		{
			OAuthRequest client = OAuthRequest.ForRequestToken (consumerKey, consumerSecret);
			client.RequestUrl = url;

			string oauthParams = client.GetAuthorizationQuery (parameters);
			string requestParams = UrlEncodeParameters (parameters);
			return url + "?" + requestParams + "&" + oauthParams;
		}

		private static int GetUnixEpochTimeInSeconds ()
		{
			TimeSpan t = (GetDate() - new DateTime(1970, 1, 1));
			return (int) t.TotalSeconds;
		}

		private static string Signature (string secret, string maybeSignature, Dictionary<string, string> data)
		{
			Dictionary<string, string> sortedData = data.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
			string joinedString = string.Join("&", (sortedData.Select(d => d.Key + "=" + d.Value).ToArray()));

			byte[] keyByte = Encoding.UTF8.GetBytes(secret);
			byte[] messageBytes = Encoding.UTF8.GetBytes(joinedString);

			using (var hmac = new HMACSHA1(keyByte))
			{
				byte[] hashMessage = hmac.ComputeHash(messageBytes);
				return Convert.ToBase64String(hashMessage);
			}
		}

		private static string UrlEncodeParameters (NameValueCollection parameters)
		{
			WebParameterCollection collection = new WebParameterCollection(parameters);
			foreach (var parameter in collection)
			{
				parameter.Value = OAuthTools.UrlEncodeStrict(parameter.Value).Replace("%20", "+");
			}
			collection.Sort((x, y) => x.Name.Equals(y.Name) ? x.Value.CompareTo(y.Value) : x.Name.CompareTo(y.Name));
			return OAuthTools.Concatenate(collection, "=", "&");
		}
	}

	public class SignatureValidationError: Exception
	{
		public SignatureValidationError (string message): base(message)
		{
		}
	}

	public class ToopherApi
	{
		public const string VERSION = "2.0.0";
		public const string DEFAULT_BASE_URL = "https://api.toopher.com/v1/";
		public ToopherApi.AdvancedApiUsageFactory advanced;

		private string consumerKey;
		private string consumerSecret;
		private string baseUrl;
		private Type webClientProxyType;

		/// <summary>
		/// Create an instance of the ToopherAPI helper for the specified API URL.
		/// </summary>
		/// <param name="consumerKey">Your Toopher API OAuth Consumer Key</param>
		/// <param name="consumerSecret">Your Toopher API OAuth Consumer Secret</param>
		/// <param name="baseUrl">The base URL of the Toopher API to target. If blank, the default is "https://api.toopher.com/v1/".</param>
		/// <param name="webClientType">The type of web client. Override WebClient class for testing. If blank, the default is WebClientProxy.</param>
		public ToopherApi (string consumerKey, string consumerSecret, string baseUrl = null, Type webClientProxyType = null)
		{
			this.advanced = new ToopherApi.AdvancedApiUsageFactory(this);
			this.consumerKey = consumerKey;
			this.consumerSecret = consumerSecret;
			if (baseUrl != null) {
				this.baseUrl = baseUrl;
			} else {
				this.baseUrl = ToopherApi.DEFAULT_BASE_URL;
			}
			if (webClientProxyType != null) {
				this.webClientProxyType = webClientProxyType;
			} else {
				this.webClientProxyType = typeof(WebClientProxy);
			}
		}

		/// <summary>
		/// Create an SMS pairing, QR pairing or regular pairing.
		/// <para>Must provide a username and phone number for an SMS pairing.
		/// <para>Must provide a username for a QR pairing.
		/// <para>Must provide a username and pairing phrase for a regular pairing.
		/// </summary>
		/// <param name="userName">A user-facing descriptive name for the user (displayed in requests).</param>
		/// <param name="pairingPhraseOrNum">The pairing phrase or phone number supplied by the user.</param>
		/// <param name="extras">An optional Dictionary of extra parameters to provide to the API.</param>
		/// <returns>A <see cref="Pairing"/> object.</returns>
		public Pairing Pair (string userName, string pairingPhraseOrNum = null, Dictionary<string, string> extras = null)
		{
			string endpoint;

			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("user_name", userName);

			if (pairingPhraseOrNum != null) {
				if (Regex.IsMatch(pairingPhraseOrNum, @"[0-9]")) {
					parameters.Add("phone_number", pairingPhraseOrNum);
					endpoint = "pairings/create/sms";
				} else {
					parameters.Add ("pairing_phrase", pairingPhraseOrNum);
					endpoint = "pairings/create";
				}
			} else {
				endpoint = "pairings/create/qr";
			}

			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}

			var json = advanced.raw.post (endpoint, parameters);
			return new Pairing (json, this);
		}

		/// <summary>
		/// Initiate an authentication request by pairing id or username
		/// </summary>
		/// <param name="pairingIdOrUsername">The pairing id or username indicating whom the request should be sent to.</param>
		/// <param name="terminalNameOrTerminalNameExtra">The user-facing descriptive name for the terminal from which the request originates or the unique identifier for this terminal. Not displayed to the user.</param>
		/// <param name="actionName">The user-facing descriptive name for the action which is being authenticated.</param>
		/// <param name="extras">An optional Dictionary of extra parameters to provide to the API.</param>
		/// <returns>An <see cref="AuthenticationRequest"/> object.</returns>
		public AuthenticationRequest Authenticate (string pairingIdOrUsername, string terminalNameOrTerminalNameExtra, string actionName = null, Dictionary<string, string> extras = null)
		{
			string endpoint = "authentication_requests/initiate";
			NameValueCollection parameters = new NameValueCollection ();

			try {
				new Guid(pairingIdOrUsername);
				parameters.Add ("pairing_id", pairingIdOrUsername);
				parameters.Add ("terminal_name", terminalNameOrTerminalNameExtra);
			} catch (Exception) {
				parameters.Add ("user_name", pairingIdOrUsername);
				parameters.Add ("terminal_name_extra", terminalNameOrTerminalNameExtra);
			}

			if (actionName != null && actionName.Length > 0) {
				parameters.Add ("action_name", actionName);
			}

			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}

			var json = advanced.raw.post (endpoint, parameters);
			return new AuthenticationRequest (json, this);
		}

		public class AdvancedApiUsageFactory
		{
			public ToopherApi.AdvancedApiUsageFactory.Pairings pairings;
			public ToopherApi.AdvancedApiUsageFactory.AuthenticationRequests authenticationRequests;
			public ToopherApi.AdvancedApiUsageFactory.Users users;
			public ToopherApi.AdvancedApiUsageFactory.UserTerminals userTerminals;
			public ToopherApi.AdvancedApiUsageFactory.ApiRawRequester raw;

			public AdvancedApiUsageFactory (ToopherApi toopherApi)
			{
				this.pairings = new ToopherApi.AdvancedApiUsageFactory.Pairings(toopherApi);
				this.authenticationRequests = new ToopherApi.AdvancedApiUsageFactory.AuthenticationRequests(toopherApi);
				this.users = new ToopherApi.AdvancedApiUsageFactory.Users(toopherApi);
				this.userTerminals = new ToopherApi.AdvancedApiUsageFactory.UserTerminals(toopherApi);
				this.raw = new ToopherApi.AdvancedApiUsageFactory.ApiRawRequester(toopherApi);
			}

			public class Pairings
			{
				private ToopherApi api;

				public Pairings (ToopherApi toopherApi)
				{
					this.api = toopherApi;
				}

				/// <summary>
				/// Retrieve the current status of a pairing.
				/// </summary>
				/// <param name="pairingId">The unique id for a pairing.</param>
				/// <returns>A <see cref="Pairing"/> object.</returns>
				public Pairing GetById (string pairingId)
				{
					string endpoint = string.Format ("pairings/{0}", pairingId);
					var json = api.advanced.raw.get (endpoint);
					return new Pairing (json, api);
				}
			}

			public class AuthenticationRequests
			{
				private ToopherApi api;

				public AuthenticationRequests (ToopherApi toopherApi)
				{
					this.api = toopherApi;
				}

				/// <summary>
				/// Retrieve the current status of an authenticationrequest.
				/// </summary>
				/// <param name="authenticationRequestId">The unique id for an authentication request.</param>
				/// <returns>A <see cref="AuthenticationRequest"/> object.</returns>
				public AuthenticationRequest GetById (string authenticationRequestId)
				{
					string endpoint = string.Format ("authentication_requests/{0}", authenticationRequestId);
					var json = api.advanced.raw.get (endpoint);
					return new AuthenticationRequest (json, api);
				}
			}

			public class Users
			{
				private ToopherApi api;

				public Users (ToopherApi toopherApi)
				{
					this.api = toopherApi;
				}

				/// <summary>
				/// Retrieve the current status of a user with the user id.
				/// </summary>
				/// <param name="userId">The unique id for a user.</param>
				/// <returns>A <see cref="User"/> object.</returns>
				public User GetById (string userId)
				{
					string endpoint = string.Format ("users/{0}", userId);
					var json = api.advanced.raw.get (endpoint);
					return new User (json, api);
				}


				/// <summary>
				/// Retrieve the current status of a user with the user name.
				/// </summary>
				/// <param name="userName">The name of the user.</param>
				/// <returns>A <see cref="User"/> object.</returns>
				public User GetByName (string userName)
				{
					string endpoint = "users";
					NameValueCollection parameters = new NameValueCollection ();
					parameters.Add ("name", userName);
					JsonArray json = api.advanced.raw.getArray (endpoint, parameters);

					if (json.Count() > 1) {
						throw new RequestError (string.Format ("More than one user with name {0}", userName));
					}
					if (json.Count() == 0) {
						throw new RequestError (string.Format ("No users with name {0}", userName));
					}
					var user = (JsonObject)json[0];
					string userId = user["id"].ToString ();
					return GetById (userId);
				}

				/// <summary>
				/// Create a new user with a user name.
				/// </summary>
				/// <param name="userName">The name of the user.</param>
				/// <param name="parameters">An optional collection of parameters to provide to the API.</param>
				/// <returns>A <see cref="User"/> object.</returns>
				public User Create (string userName, NameValueCollection parameters = null)
				{
					string endpoint = "users/create";
					if (parameters == null) {
						parameters = new NameValueCollection ();
					}
					parameters.Add ("name", userName);
					var json = api.advanced.raw.post (endpoint, parameters);
					return new User (json, api);
				}
			}

			public class UserTerminals
			{
				private ToopherApi api;

				public UserTerminals (ToopherApi toopherApi)
				{
					this.api = toopherApi;
				}

				/// <summary>
				/// Retrieve the current status of a user terminal.
				/// </summary>
				/// <param name="userTerminalId">The unique id for a user terminal.</param>
				/// <returns>A <see cref="UserTerminal"/> object.</returns>
				public UserTerminal GetById (string userTerminalId)
				{
					string endpoint = string.Format ("user_terminals/{0}", userTerminalId);
					var json = api.advanced.raw.get (endpoint);
					return new UserTerminal (json, api);
				}

				/// <summary>
				/// Create a new user terminal
				/// </summary>
				/// <param name="userName">The name of the user.</param>
				/// <param name="terminalName">The user-facing descriptive name for the terminal from which the request originates</param>
				/// <param name="requesterSpecifiedId">The requester specified id that uniquely idenfities this terminal.</param>
				/// <param name="paramters">An optional collection of parameters to provide to the API.</param>
				/// <returns>A <see cref="UserTerminal"/> object.</returns>
				public UserTerminal Create (string userName, string terminalName, string requesterSpecifiedId, NameValueCollection parameters = null)
				{
					string endpoint = "user_terminals/create";
					if (parameters == null) {
						parameters = new NameValueCollection ();
					}
					parameters.Add ("user_name", userName);
					parameters.Add ("terminal_name", terminalName);
					parameters.Add ("requester_specified_id", requesterSpecifiedId);
					var json = api.advanced.raw.post (endpoint, parameters);
					return new UserTerminal (json, api);
				}
			}

			public class ApiRawRequester
			{
				private ToopherApi api;

				public ApiRawRequester (ToopherApi toopherApi)
				{
					this.api = toopherApi;
				}

				private object request (string method, string endpoint, NameValueCollection parameters = null)
				{
					// Normalize method string
					method = method.ToUpper ();

					// Build an empty collection for parameters (if necessary)
					if (parameters == null) {
						parameters = new NameValueCollection ();
					}

					// can't have null parameters, or oauth signing will barf
					foreach (String key in parameters.AllKeys){
						if (parameters[key] == null) {
							parameters[key] = "";
						}
					}

					var client = OAuthRequest.ForRequestToken (api.consumerKey, api.consumerSecret);
					client.RequestUrl = api.baseUrl + endpoint;
					client.Method = method;

					string auth = client.GetAuthorizationHeader (parameters);
					// FIXME: OAuth library puts extraneous comma at end, workaround: remove it if present
					auth = auth.TrimEnd (new char[] { ',' });

					using (WebClientProxy wClient = (WebClientProxy)Activator.CreateInstance(api.webClientProxyType)) {
						wClient.Headers.Add ("Authorization", auth);
						wClient.Headers.Add ("User-Agent",
							string.Format ("Toopher-DotNet/{0} (DotNet {1})", VERSION, Environment.Version.ToString ()));
						if (parameters.Count > 0) {
							wClient.QueryString = parameters;
						}

						string response;
						try {
							if (method.Equals ("POST")) {
								var responseArray = wClient.UploadValues (client.RequestUrl, client.Method, parameters);
								response = Encoding.UTF8.GetString (responseArray);
							} else {
								response = wClient.DownloadString (client.RequestUrl);
							}
						} catch (WebException wex) {
							IHttpWebResponse httpResp = HttpWebResponseWrapper.create(wex.Response);
							string error_message;
							using (Stream stream = httpResp.GetResponseStream ()) {
								StreamReader reader = new StreamReader (stream, Encoding.UTF8);
								error_message = reader.ReadToEnd ();
							}

							String statusLine = httpResp.StatusCode.ToString () + " : " + httpResp.StatusDescription;

							if (String.IsNullOrEmpty (error_message)) {
								throw new RequestError (statusLine);
							} else {

								try {
									// Attempt to parse JSON response
									var json = (JsonObject)SimpleJson.SimpleJson.DeserializeObject (error_message);
									parseRequestError (json);
								} catch (RequestError e) {
									throw e;
								} catch (Exception) {
									throw new RequestError (statusLine + " : " + error_message);
								}
							}

							throw new RequestError (error_message, wex);
						}

						try {
							return SimpleJson.SimpleJson.DeserializeObject (response);
						} catch (Exception ex) {
							throw new RequestError ("Could not parse response", ex);
						}
					}
				}

				public JsonObject get (string endpoint, NameValueCollection parameters = null)
				{
					return (JsonObject)request ("GET", endpoint, parameters);
				}

				public JsonArray getArray (string endpoint, NameValueCollection parameters = null)
				{
					return (JsonArray)request ("GET", endpoint, parameters);
				}

				public JsonObject post (string endpoint, NameValueCollection parameters = null)
				{
					return (JsonObject) request ("POST", endpoint, parameters);
				}

				private void parseRequestError (JsonObject err)
				{
					long errCode = (long)err["error_code"];
					string errMessage = (string)err["error_message"];
					if (errCode == UserDisabledError.ERROR_CODE) {
						throw new UserDisabledError ();
					} else if (errCode == UserUnknownError.ERROR_CODE) {
						throw new UserUnknownError ();
					} else if (errCode == TerminalUnknownError.ERROR_CODE) {
						throw new TerminalUnknownError ();
					} else {
						if (errMessage.ToLower ().Contains ("pairing has been deactivated")
							|| errMessage.ToLower ().Contains ("pairing has not been authorized")) {
							throw new PairingDeactivatedError ();
						} else {
							throw new RequestError (errMessage);
						}
					}
				}
			}
		}
	}


	public class Pairing
	{
		private IDictionary<string, Object> rawResponse;
		private ToopherApi api;

		public object this[string key]
		{
			get
			{
				return rawResponse[key];
			}
		}

		public string id
		{
			get;
			private set;
		}
		public bool pending
		{
			get;
			private set;
		}
		public bool enabled
		{
			get;
			private set;
		}
		public User user;

		public override string ToString ()
		{
			return string.Format ("[Pairing: id={0}; enabled={1}; pending={2}; userId={3}; userName={4}; userToopherAuthenticationEnabled={5}]", id, enabled, pending, user.id, user.name, user.toopherAuthenticationEnabled);
		}

		/// <summary>
		/// Provide information about the status of a pairing.
		/// </summary>
		/// <param name="response">The response from the API.</param>
		/// <param name="toopherApi">The Toopher API associated with this pairing.</param>
		public Pairing (IDictionary<string, object> response, ToopherApi toopherApi)
		{
			this.rawResponse = response;
			this.api = toopherApi;
			try {
				this.id = (string)response["id"];
				this.pending = (bool)response["pending"];
				this.enabled = (bool)response["enabled"];
				this.user = new User ((JsonObject)response["user"], toopherApi);
			} catch (Exception ex) {
				throw new RequestError ("Could not parse pairing from response", ex);
			}
		}

		/// <summary>
		/// Update the pairing object with response from the API.
		/// </summary>
		public void RefreshFromServer ()
		{
			string endpoint = string.Format ("pairings/{0}", id);
			var json = api.advanced.raw.get (endpoint);
			Update (json);
		}

		/// <summary>
		/// Retrieve link to allow user to reset the pairing.
		/// </summary>
		/// <param name="extras">An optional Dictionary of extra parameters to provide to the API.</param>
		/// <returns>A reset link as a string</returns>
		public string GetResetLink (Dictionary<string, string> extras = null)
		{
			string endpoint = string.Format ("pairings/{0}/generate_reset_link", id);
			NameValueCollection parameters = new NameValueCollection ();
			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}
			var json = api.advanced.raw.post (endpoint, parameters);
			return (string)json["url"];
		}

		/// <summary>
		/// Send reset link to user via email.
		/// </summary>
		/// <param name="email">The email address where the reset link is sent.</param>
		/// <param name="extras">An optional Dictionary of extra parameters to provide to the API.</param>
		public void EmailResetLink (string email, Dictionary<string, string> extras = null)
		{
			string endpoint = string.Format ("pairings/{0}/send_reset_link", id);
			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("reset_email", email);
			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}
			api.advanced.raw.post(endpoint, parameters);
		}

		/// <summary>
		/// Retrieve QR code image for the pairing.
		/// </summary>
		/// <returns>QR code image stored as a byte[].</returns>
		public byte[] GetQrCodeImage ()
		{
			string endpoint = string.Format ("qr/pairings/{0}", id);
			var result = api.advanced.raw.get(endpoint);
			return System.Text.Encoding.UTF8.GetBytes (result.ToString());
		}

		private void Update (IDictionary<string, object> response)
		{
			this.rawResponse = response;
			try {
				this.pending = (bool)response["pending"];
				this.enabled = (bool)response["enabled"];
				this.user.Update ((JsonObject)response["user"]);
			} catch (Exception ex) {
				throw new RequestError ("Could not parse pairing from response", ex);
			}
		}
	}

	public class AuthenticationRequest
	{
		private IDictionary<string, object> rawResponse;
		private ToopherApi api;

		public object this[string key]
		{
			get
			{
				return rawResponse[key];
			}
		}

		public string id
		{
			get;
			private set;
		}
		public bool pending
		{
			get;
			private set;
		}
		public bool granted
		{
			get;
			private set;
		}
		public bool automated
		{
			get;
			private set;
		}
		public int reasonCode
		{
			get;
			private set;
		}
		public string reason
		{
			get;
			private set;
		}
		public Action action;
		public UserTerminal terminal;
		public User user;

		public override string ToString ()
		{
			return string.Format ("[AuthenticationRequest: id={0}; pending={1}; granted={2}; automated={3}; reasonCode={4}; reason={5}; actionId={6}; actionName={7}; terminalId={8}; terminalName={9}; terminalRequesterSpecifiedId={10}; userId={11}; userName={12}; userToopherAuthenticationEnabled={13}]", id, pending, granted, automated, reasonCode, reason, action.id, action.name, terminal.id, terminal.name, terminal.requesterSpecifiedId, user.id, user.name, user.toopherAuthenticationEnabled);
		}

		/// <summary>
		/// Provide information about the status of an authentication request.
		/// </summary>
		/// <param name="response">The response from the API.</param>
		/// <param name="toopherApi">The Toopher API associated with this authentication request.</param>
		public AuthenticationRequest (IDictionary<string, object> response, ToopherApi toopherApi)
		{
			this.rawResponse = response;
			this.api = toopherApi;
			try {
				this.id = (string)response["id"];
				this.pending = (bool)response["pending"];
				this.granted = (bool)response["granted"];
				this.automated = (bool)response["automated"];
				this.reasonCode = Convert.ToInt32(response["reason_code"]);
				this.reason = (string)response["reason"];
				this.action = new Action((JsonObject)response["action"]);
				this.terminal = new UserTerminal((JsonObject)response["terminal"], toopherApi);
				this.user = new User((JsonObject)response["user"], toopherApi);
			} catch (Exception ex) {
				throw new RequestError ("Could not parse authentication request from response", ex);
			}
		}

		/// <summary>
		/// Update the authentication request object with response from the API.
		/// </summary>
		public void RefreshFromServer ()
		{
			string endpoint = string.Format ("authentication_requests/{0}",  id);
			var json = api.advanced.raw.get(endpoint);
			Update (json);
		}

		/// <summary>
		/// Grant the authentication request with an OTP.
		/// </summary>
		/// <param name="otp">One-time password for the authentication request.</param>
		public void GrantWithOtp (string otp)
		{
			string endpoint = string.Format ("authentication_requests/{0}/otp_auth", id);
			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("otp", otp);
			var json = api.advanced.raw.post(endpoint, parameters);
			Update (json);
		}

		private void Update (IDictionary<string, object> response)
		{
			this.rawResponse = response;
			try {
				this.pending = (bool)response["pending"];
				this.granted = (bool)response["granted"];
				this.automated = (bool)response["automated"];
				this.reasonCode = Convert.ToInt32(response["reason_code"]);
				this.reason = (string)response["reason"];
				this.action.Update((JsonObject)response["action"]);
				this.terminal.Update((JsonObject)response["terminal"]);
				this.user.Update((JsonObject)response["user"]);
			} catch (Exception ex) {
				throw new RequestError ("Could not parse authentication request from response", ex);
			}
		}
	}

	public class User
	{
		private IDictionary<string, object> rawResponse;
		private ToopherApi api;

		public object this[string key]
		{
			get
			{
				return rawResponse[key];
			}
		}

		public string id
		{
			get;
			private set;
		}
		public string name
		{
			get;
			private set;
		}
		public bool toopherAuthenticationEnabled
		{
			get;
			private set;
		}

		public override string ToString ()
		{
			return string.Format ("[User: id={0}; name={1}; toopherAuthenticationEnabled={2}]", id, name, toopherAuthenticationEnabled);
		}

		/// <summary>
		/// Provide information about the status of a user.
		/// </summary>
		/// <param name="response">The response from the API.</param>
		/// <param name="toopherApi">The Toopher API associated with this authentication request.</param>
		public User (IDictionary<string, object> response, ToopherApi toopherApi)
		{
			this.rawResponse = response;
			this.api = toopherApi;
			try {
				this.id = (string)response["id"];
				this.name = (string)response["name"];
				this.toopherAuthenticationEnabled = (bool)response["toopher_authentication_enabled"];
			} catch (Exception ex) {
				throw new RequestError ("Could not parse user from response", ex);
			}
		}

		/// <summary>
		/// Update the user object with response from the API.
		/// </summary>
		public void RefreshFromServer ()
		{
			string endpoint = string.Format ("users/{0}", id);
			var json = api.advanced.raw.get(endpoint);
			Update (json);
		}

		/// <summary>
		/// Update the user object with provided response.
		/// </summary>
		/// <param name="response">The response from the API.</param>
		public void Update (IDictionary<string, object> response)
		{
			this.rawResponse = response;
			try {
				this.name = (string)response["name"];
				this.toopherAuthenticationEnabled = (bool)response["toopher_authentication_enabled"];
			} catch (Exception ex) {
				throw new RequestError ("Could not parse user from response", ex);
			}
		}

		/// <summary>
		/// Enable Toopher Authentication for an individual user.
		/// </summary>
		public void EnableToopherAuthentication ()
		{
			string endpoint = string.Format ("users/{0}", id);
			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("toopher_authentication_enabled", "true");
			var json = api.advanced.raw.post (endpoint, parameters);
			Update (json);
		}

		/// <summary>
		/// Disable Toopher Authentication for an individual user.  If the user is
		/// disabled, future attempts to authenticate the user with Toopher will return
		/// a UserDisabledError.
		/// </summary>
		public void DisableToopherAuthentication ()
		{
			string endpoint = string.Format ("users/{0}", id);
			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("toopher_authentication_enabled", "false");
			var json = api.advanced.raw.post (endpoint, parameters);
			Update (json);
		}

		/// <summary>
		/// Remove all pairings for the user.
		/// </summary>
		public void Reset ()
		{
			string endpoint = "users/reset";
			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("name", name);
			api.advanced.raw.post(endpoint, parameters);
		}
	}

	public class UserTerminal
	{
		private IDictionary<string, object> rawResponse;
		private ToopherApi api;

		public object this[string key]
		{
			get
			{
				return rawResponse[key];
			}
		}

		public string id
		{
			get;
			private set;
		}
		public string name
		{
			get;
			private set;
		}
		public string requesterSpecifiedId
		{
			get;
			private set;
		}
		public User user;

		public override string ToString ()
		{
			return string.Format ("[UserTerminal: id={0}; name={1}; requesterSpecifiedId={2}; userId={3}, userName={4}, userToopherAuthenticationEnabled={5}]", id, name, requesterSpecifiedId, user.id, user.name, user.toopherAuthenticationEnabled);
		}

		/// <summary>
		/// Provide information about the status of a user terminal.
		/// </summary>
		/// <param name="response">The response from the API.</param>
		/// <param name="toopherApi">The Toopher API associated with this authentication request.</param>
		public UserTerminal (IDictionary<string, object> response, ToopherApi toopherApi)
		{
			this.rawResponse = response;
			this.api = toopherApi;
			try {
				this.id = (string)response["id"];
				this.name = (string)response["name"];
				this.requesterSpecifiedId = (string)response["requester_specified_id"];
				this.user = new User ((JsonObject)response["user"], toopherApi);
			} catch (Exception ex) {
				throw new RequestError ("Could not parse user terminal from response", ex);
			}
		}

		/// <summary>
		/// Update the user terminal object with response from the API.
		/// </summary>
		public void RefreshFromServer ()
		{
			string endpoint = string.Format ("user_terminals/{0}", id);
			var json = api.advanced.raw.get (endpoint);
			Update (json);
		}

		/// <summary>
		/// Update the user object with provided response.
		/// </summary>
		/// <param name="response">The response from the API.</param>
		public void Update (IDictionary<string, object> response)
		{
			this.rawResponse = response;
			try {
				this.name = (string)response["name"];
				this.requesterSpecifiedId = (string)response["requester_specified_id"];
				this.user.Update ((JsonObject)response["user"]);
			} catch (Exception ex) {
				throw new RequestError ("Could not parse user terminal from response", ex);
			}
		}
	}

	public class Action
	{
		private IDictionary<string, object> rawResponse;
		public object this[string key]
		{
			get
			{
				return rawResponse[key];
			}
		}

		public string id
		{
			get;
			private set;
		}
		public string name
		{
			get;
			private set;
		}

		public override string ToString ()
		{
			return string.Format ("[Action: id={0}; name={1}]", id, name);
		}

		public Action (IDictionary<string, object> response)
		{
			this.rawResponse = response;
			try {
				this.id = (string)response["id"];
				this.name = (string)response["name"];
			} catch (Exception ex) {
				throw new RequestError ("Could not parse action from response", ex);
			}
		}

		public void Update (IDictionary<string, object> response)
		{
			this.rawResponse = response;
			try {
				this.name = (string)response["name"];
			} catch (Exception ex) {
				throw new RequestError ("Could not parse action from response", ex);
			}
		}
	}

	// An exception class used to indicate an error in a request
	public class RequestError : System.ApplicationException
	{
		public RequestError () : base () { }
		public RequestError (string message) : base (message) { }
		public RequestError (string message, System.Exception inner) : base (message, inner) { }
	}

	/// <summary>
	/// Thrown when a requester attempts to authenticate a user who has been disabled
	/// </summary>
	public class UserDisabledError : RequestError
	{
		static public int ERROR_CODE = 704;
	}

	/// <summary>
	/// Thrown when there is no active pairing for the user.
	/// Requester should respond by guiding the user through the pairing process,
	/// then re-authenticating
	/// </summary>
	public class UserUnknownError : RequestError
	{
		static public int ERROR_CODE = 705;
	}

	/// <summary>
	/// Thrown when Toopher API encounters an unknown (user, requesterTerminalIdentifier) tuple.
	/// Requester should respond by assigning a friendly name to the terminal
	/// </summary>
	public class TerminalUnknownError : RequestError
	{
		static public int ERROR_CODE = 706;
	}

	/// <summary>
	/// Thrown when the user has deleted the pairing on their mobile device.
	/// Requester should prompt user to re-pair their account
	/// </summary>
	public class PairingDeactivatedError : RequestError
	{
	}

	/// <summary>
	/// Design-For-Testability shims from here on down...
	/// </summary>
	
	public class WebClientProxy : IDisposable
	{
		WebClient _theClient = new WebClient ();
		public WebHeaderCollection Headers
		{
			get { return _theClient.Headers; }
			set { _theClient.Headers = value; }
		}
		public NameValueCollection QueryString
		{
			get { return _theClient.QueryString; }
			set { _theClient.QueryString = value; }
		}
		virtual public byte[] UploadValues (string requestUri, string method, NameValueCollection parameters)
		{
			return _theClient.UploadValues (requestUri, method, parameters);
		}
		virtual public string DownloadString (string requestUri)
		{
			return _theClient.DownloadString (requestUri);
		}

		public void Dispose ()
		{
			if (_theClient != null) {
				_theClient.Dispose ();
				_theClient = null;
			}
		}
	}

	public interface IHttpWebResponse
	{
		Stream GetResponseStream ();
		HttpStatusCode StatusCode { get; }
		string StatusDescription { get; }
	}

	class HttpWebResponseWrapper : IHttpWebResponse
	{
		private HttpWebResponse _wrapped;
		public HttpWebResponseWrapper (HttpWebResponse wrapped)
		{
			this._wrapped = wrapped;
		}
		static public IHttpWebResponse create (object response)
		{
			//if it already implements IHttpWebResponse, return it directory
			if(typeof(IHttpWebResponse).IsAssignableFrom(response.GetType())){
				return (IHttpWebResponse)response;
			} else if (typeof(HttpWebResponse).IsAssignableFrom(response.GetType())) {
				return new HttpWebResponseWrapper((HttpWebResponse)response);
			} else {
				throw new NotImplementedException("Don't know how to transmute " + response.GetType().ToString() + " into IHttpWebResponse");
			}
		}

		public Stream GetResponseStream ()
		{
			return _wrapped.GetResponseStream();
		}

		public HttpStatusCode StatusCode
		{
			get { return _wrapped.StatusCode; }
		}

		public string StatusDescription
		{
			get { return _wrapped.StatusDescription; }
		}
	}

}

