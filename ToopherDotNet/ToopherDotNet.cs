using System;
using OAuth;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Runtime.Serialization;
using SimpleJson;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Toopher
{
	public class ToopherApi
	{

		public const string VERSION = "2.0.0";
		public const string DEFAULT_BASE_URL = "https://api.toopher.com/v1/";
		public ToopherApi.AdvancedApiUsageFactory advanced;

		string consumerKey;
		string consumerSecret;
		string baseUrl;
		Type webClientProxyType;

		// Create the ToopherApi object tied to your requester credentials
		//
		// Credentials are available on https://dev.toopher.com
		/// <summary>
		/// Create a new instance of the ToopherApi client tied to your requester
		/// credentials.  Credentials are available at https://dev.toopher.com
		/// </summary>
		/// <param name="consumerKey">OAuth Consumer Key</param>
		/// <param name="consumerSecret">OAuth Consumer Secret</param>
		/// <param name="baseUrl">Override url for ToopherApi webservice (default=https://api.toopher.com/v1/) </param>
		/// <param name="webClientType">Override WebClient class for testing purposes</param>
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

		// Create an SMS pairing, QR pairing or regular pairing
		//
		// Must provide a username and phone number for an SMS pairing
		// Must provide a username for a QR pairing
		// Must provide a username and pairing phrase for a regular pairing
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
			return new Pairing (json);
		}

		// Initiate an authentication request by pairing id or username
		//
		// Provide pairing ID or username, a name for the terminal (displayed to user) or requester-specified ID,
		// an optional action name (displayed to user) [defaults to "log in"] and
		// an optional Dictionary of extras to be sent to the API
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
			return new AuthenticationRequest (json);
		}

		/// <summary>
		/// Create a named terminal in the Toopher API for the (userName, terminalIdentifier) combination
		/// </summary>
		/// <param name="userName">Name of the user</param>
		/// <param name="terminalName">User-assigned "friendly" terminal name</param>
		/// <param name="terminalIdentifier">Unique terminal identifier for this terminal.  Does not need to be human-readable.</param>
		/// <exception cref="RequestError">Thrown when there is a problem contacting the Toopher API</exception>
		public void CreateUserTerminal (string userName, string terminalName, string terminalIdentifier)
		{
			string endpoint = "user_terminals/create";
			NameValueCollection parameters = new NameValueCollection();
			parameters["user_name"] = userName;
			parameters["name"] = terminalName;
			parameters["name_extra"] = terminalIdentifier;
			advanced.raw.post (endpoint, parameters);
		}

		/// <summary>
		/// Enable or Disable Toopher Authentication for an individual user.  If the user is
		/// disabled, future attempts to authenticate the user with Toopher will return
		/// a UserDisabledError
		/// </summary>
		/// <param name="userName">Name of the user to modify</param>
		/// <param name="toopherEnabled">True if the user should be authenticated with Toopher</param>
		/// <exception cref="RequestError">Thrown when there is a problem contacting the Toopher API</exception>
		public void SetToopherEnabledForUser (string userName, bool toopherEnabled)
		{
			string searchEndpoint = "users";
			NameValueCollection parameters = new NameValueCollection ();
			parameters["user_name"] = userName;

			JsonArray jArr = advanced.raw.getArray (searchEndpoint, parameters);
			if (jArr.Count > 1) {
				throw new RequestError ("Multiple users with name = " + userName);
			}
			if (jArr.Count == 0) {
				throw new RequestError ("No users with name = " + userName);
			}

			string userId = (string)((JsonObject)jArr[0])["id"];

			string updateEndpoint = "users/" + userId;
			parameters = new NameValueCollection ();
			parameters["disable_toopher_auth"] = toopherEnabled ? "false" : "true";
			advanced.raw.post (updateEndpoint, parameters);
		}

		public string GetPairingResetLink (string pairingId, string securityQuestion = null, string securityAnswer = null)
		{
			string endpoint = "pairings/" + pairingId + "/generate_reset_link";
			NameValueCollection parameters = new NameValueCollection ();
			parameters["security_question"] = securityQuestion;
			parameters["security_answer"] = securityAnswer;

			JsonObject pairingResetLink = advanced.raw.post (endpoint, parameters);
			return (string)pairingResetLink["url"];
		}

		public class AdvancedApiUsageFactory
		{
			public ToopherApi.AdvancedApiUsageFactory.Pairings pairings;
			public ToopherApi.AdvancedApiUsageFactory.ApiRawRequester raw;

			public AdvancedApiUsageFactory (ToopherApi toopherApi)
			{
				this.pairings = new ToopherApi.AdvancedApiUsageFactory.Pairings(toopherApi);
				this.raw = new ToopherApi.AdvancedApiUsageFactory.ApiRawRequester(toopherApi);
			}

			public class Pairings
			{
				private ToopherApi api;

				public Pairings (ToopherApi toopherApi)
				{
					this.api = toopherApi;
				}

				public Pairing GetById (string pairingId)
				{
					string endpoint = string.Format ("pairings/{0}", pairingId);
					var json = api.advanced.raw.get (endpoint);
					return new Pairing (json);
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


	// Status information for a pairing request
	public class Pairing
	{
		private IDictionary<string, Object> _dict;

		public object this[string key]
		{
			get
			{
				return _dict[key];
			}
		}

		public string id
		{
			get;
			private set;
		}
		public string userId
		{
			get;
			private set;
		}
		public string userName
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

		public override string ToString ()
		{
			return string.Format ("[Pairing: id={0}; userId={1}; userName={2}, enabled={3}, pending={4}]", id, userId, userName, enabled, pending);
		}

		public Pairing (IDictionary<string, object> _dict)
		{
			try {
				this._dict = _dict;
				this.id = (string)_dict["id"];
				this.pending = (bool)_dict["pending"];
				this.enabled = (bool)_dict["enabled"];

				var user = (JsonObject)_dict["user"];
				this.userId = (string)user["id"];
				this.userName = (string)user["name"];
			} catch (Exception ex) {
				throw new RequestError ("Could not parse pairing status from response", ex);
			}
		}

	}

	// Status information for an authentication request
	public class AuthenticationRequest
	{
		private IDictionary<string, object> _dict;
		public object this[string key]
		{
			get
			{
				return _dict[key];
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
		public string reason
		{
			get;
			private set;
		}
		public string terminalId
		{
			get;
			private set;
		}
		public string terminalName
		{
			get;
			private set;
		}

		public override string ToString ()
		{
			return string.Format ("[AuthenticationRequest: id={0}; pending={1}; granted={2}; automated={3}; reason={4}; terminalId={5}; terminalName={6}]", id, pending, granted, automated, reason, terminalId, terminalName);
		}

		public AuthenticationRequest (IDictionary<string, object> _dict)
		{
			this._dict = _dict;
			try {
				// validate that the json has the minimum keys we need
				this.id = (string)_dict["id"];
				this.pending = (bool)_dict["pending"];
				this.granted = (bool)_dict["granted"];
				this.automated = (bool)_dict["automated"];
				this.reason = (string)_dict["reason"];

				var terminal = (JsonObject)_dict["terminal"];
				this.terminalId = (string)terminal["id"];
				terminalName = (string)terminal["name"];
			} catch (Exception ex) {
				throw new RequestError ("Could not parse authentication status from response", ex);
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

