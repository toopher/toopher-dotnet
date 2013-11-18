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

namespace Toopher
{
	public class ToopherAPI
	{
		public const string VERSION = "1.0.0";
		public const string DEFAULT_BASE_URL = "https://api.toopher.com/v1/";

		string consumerKey;
		string consumerSecret;
		string baseUrl;

		// Create the ToopherAPI object tied to your requester credentials
		// 
		// Credentials are available on https://dev.toopher.com
		public ToopherAPI (string consumerKey, string consumerSecret, string baseUrl = null)
		{
			this.consumerKey = consumerKey;
			this.consumerSecret = consumerSecret;
			if (baseUrl != null) {
				this.baseUrl = baseUrl;
			} else {
				this.baseUrl = ToopherAPI.DEFAULT_BASE_URL;
			}
		}

		// Pair your requester with a user's Toopher application
		//
		// Must provide a pairing request (generated on their phone) and
		// the user's name
		public PairingStatus Pair (string pairingPhrase, string userName, Dictionary<string, string> extras = null)
		{
			string endpoint = "pairings/create";

			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("pairing_phrase", pairingPhrase);
			parameters.Add ("user_name", userName);

			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}

			var json = post (endpoint, parameters);
			return new PairingStatus (json);
		}

		// Check on status of a pairing request
		// 
		// Must provide the ID returned when the pairing request was initiated
		public PairingStatus GetPairingStatus (string pairingRequestId)
		{
			string endpoint = String.Format ("pairings/{0}", pairingRequestId);

			var json = get (endpoint);
			return new PairingStatus (json);
		}

		// Authenticate an action with Toopher
		//
		// Provide pairing ID, a name for the terminal (displayed to user) and
		// an option action name (displayed to user) [defaults to "log in"]
		public AuthenticationStatus Authenticate (string pairingId, string terminalName, string actionName = null, Dictionary<string, string> extras = null)
		{
			string endpoint = "authentication_requests/initiate";

			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("pairing_id", pairingId);
			parameters.Add ("terminal_name", terminalName);
			if (actionName != null) {
				parameters.Add ("action_name", actionName);
			}
			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}

			var json = post (endpoint, parameters);
			return new AuthenticationStatus (json);
		}

		/// <summary>
		/// Authenticate an action with Toopher
		/// </summary>
		/// <param name="userName">Name of the user</param>
		/// <param name="terminalIdentifier">Unique terminal identifier for this terminal.  Does not need to be human-readable.</param>
		/// <param name="actionName">Name of the action to authenticate.  default = "Login"</param>
		/// <param name="extras">Dictionary of arbitray key/value pairs to add to the webservice call</param>
		/// <returns>AuthenticationStatus object</returns>
		/// <exception cref="UserDisabledError">Thrown when Toopher Authentication is disabled for the user</exception>
		/// <exception cref="UnknownUserError">Thrown when the user has no active pairings</exception>
		/// <exception cref="UnknownTerminalError">Thrown when the terminal cannot be identified</exception>
		/// <exception cref="PairingDeactivatedError">Thrown when the user has deleted the pairing from their mobile device</exception>
		/// <exception cref="RequestError">Thrown when there is a problem contacting the Toopher API</exception>
		public AuthenticationStatus AuthenticateByUserName (string userName, string terminalIdentifier, string actionName, Dictionary<string, string> extras = null)
		{
			if (extras == null) {
				extras = new Dictionary<string, string> ();
			}
			extras["user_name"] = userName;
			extras["terminal_name_extra"] = terminalIdentifier;

			return this.Authenticate (null, null, actionName, extras);
		}

		// Check on status of authentication request
		//
		// Provide authentication request ID returned when authentication request was
		// started.
		public AuthenticationStatus GetAuthenticationStatus (string authenticationRequestId)
		{
			string endpoint = String.Format ("authentication_requests/{0}", authenticationRequestId);

			var json = get (endpoint);
			return new AuthenticationStatus (json);
		}

		/// <summary>
		/// Associate a per-user Terminal Name with a given terminalIdentifier
		/// </summary>
		/// <param name="userName">Name of the user</param>
		/// <param name="terminalName">User-assigned "friendly" terminal name</param>
		/// <param name="terminalIdentifier">Unique terminal identifier for this terminal.  Does not need to be human-readable.</param>
		/// <exception cref="RequestError">Thrown when there is a problem contacting the Toopher API</exception>
		public void AssignUserFriendlyNameToTerminal (string userName, string terminalName, string terminalIdentifier)
		{
			string endpoint = "user_terminals/create";
			NameValueCollection parameters = new NameValueCollection();
			parameters["user_name"] = userName;
			parameters["name"] = terminalName;
			parameters["name_extra"] = terminalIdentifier;
			post (endpoint, parameters);
		}

		/// <summary>
		/// Enable or Disable Toopher Authentication for an individual user.  If the user is
		/// disabled, future attempts to authenticate the user with Toopher will return
		/// a UserDisabledError
		/// </summary>
		/// <param name="userName">Name of the user to modify</param>
		/// <param name="toopherEnabled">True if the user should be authenticated with Toopher</param>
		/// <exception cref="RequestError">Thrown when there is a problem contacting the Toopher API</exception>
		private void SetToopherEnabledForUser (string userName, bool toopherEnabled)
		{
			string searchEndpoint = "users";
			NameValueCollection parameters = new NameValueCollection ();
			parameters["user_name"] = userName;

			JsonArray jArr = getArray (searchEndpoint, parameters);
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
			post (updateEndpoint, parameters);
		}

		private object request (string method, string endpoint, NameValueCollection parameters = null)
		{
			// Normalize method string
			method = method.ToUpper ();

			// Build an empty collection for parameters (if necessary)
			if (parameters == null) {
				parameters = new NameValueCollection ();
			}

			var client = OAuthRequest.ForRequestToken (this.consumerKey, this.consumerSecret);
			client.RequestUrl = this.baseUrl + endpoint;
			client.Method = method;

			string auth = client.GetAuthorizationHeader (parameters);
			// FIXME: OAuth library puts extraneous comma at end, workaround: remove it if present
			auth = auth.TrimEnd (new char[] { ',' });

			WebClient wClient = new WebClient ();
			wClient.Headers.Add ("Authorization", auth);
			wClient.Headers.Add ("User-Agent", 
				string.Format("Toopher-DotNet/{0} (DotNet {1})", VERSION, Environment.Version.ToString()));
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
					HttpWebResponse httpResp = (HttpWebResponse)wex.Response;
				string error_message;
				using (Stream stream = wex.Response.GetResponseStream ()) {
					StreamReader reader = new StreamReader (stream, Encoding.UTF8);
					error_message = reader.ReadToEnd ();
				}
				String statusLine =  httpResp.StatusCode.ToString () + " : " + httpResp.StatusDescription;

				if (String.IsNullOrEmpty (error_message)) {
					throw new RequestError (statusLine);
				} else {

					try {
						// Attempt to parse JSON response
						var json = (JsonObject)SimpleJson.SimpleJson.DeserializeObject (error_message);
						parseRequestError (json);
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

		
		private JsonObject get (string endpoint, NameValueCollection parameters = null)
		{
			return (JsonObject)request ("GET", endpoint, parameters);
		}
		private JsonArray getArray (string endpoint, NameValueCollection parameters = null)
		{
			return (JsonArray)request ("GET", endpoint, parameters);
		}

		private JsonObject post (string endpoint, NameValueCollection parameters = null)
		{
			return (JsonObject) request ("POST", endpoint, parameters);
		}

		private void parseRequestError (JsonObject err)
		{
			int errCode = (int)err["error_code"];
			string errMessage = (string)err["error_message"];
			if (errCode == UserDisabledError.ERROR_CODE) {
				throw new UserDisabledError ();
			} else if (errCode == UnknownUserError.ERROR_CODE) {
				throw new UnknownUserError ();
			} else if (errCode == UnknownTerminalError.ERROR_CODE) {
				throw new UnknownTerminalError ();
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

	// Status information for a pairing request
	public class PairingStatus
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
		public bool enabled
		{
			get;
			private set;
		}

		public override string ToString ()
		{
			return string.Format ("[PairingStatus: id={0}; userId={1}; userName={2}, enabled={3}]", id, userId, userName, enabled);
		}

		public PairingStatus (IDictionary<string, object> _dict)
		{
			try {
				this._dict = _dict;
				this.id = (string)_dict["id"];
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
	public class AuthenticationStatus
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
			return string.Format ("[AuthenticationStatus: id={0}; pending={1}; granted={2}; automated={3}; reason={4}; terminalId={5}; terminalName={6}]", id, pending, granted, automated, reason, terminalId, terminalName);
		}

		public AuthenticationStatus (IDictionary<string, object> _dict)
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
	public class UnknownUserError : RequestError
	{
		static public int ERROR_CODE = 705;
	}

	/// <summary>
	/// Thrown when Toopher API encounters an unknown (user, requesterTerminalIdentifier) tuple.
	/// Requester should respond by assigning a friendly name to the terminal
	/// </summary>
	public class UnknownTerminalError : RequestError
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


}

