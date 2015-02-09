using System;
using System.Collections.Generic;
using Toopher;

namespace Toopher
{
	class ToopherZeroStorageDemo
	{
		public const string DEFAULT_USERNAME = "demo@toopher.com";
		public const string DEFAULT_TERMINAL_NAME = "my computer";

		public static ToopherAPI api;
		static Random random = new Random (DateTime.Now.Millisecond);

		enum STATE
		{
			AUTHENTICATE,
			POLL_FOR_AUTHENTICATION,
			ENTER_OTP,
			EVALUATE_AUTHENTICATION_STATUS,
			PAIR,
			POLL_FOR_PAIRING,
			USER_DISABLED,
			NAME_TERMINAL,
			RESET_PAIRING,
		}

		static STATE state;

		private static bool yesNoPrompt(string prompt, string defaultResponse = "Y")
		{
			Console.Write(String.Format("{0} [{1}] ", prompt, defaultResponse));
			string response = Console.ReadLine ().Trim ().ToUpper ();
			if (response.Length == 0) {
				response = defaultResponse;
			}
			return response.StartsWith("Y");
		}

		public static void Main (string[] args)
		{
			Console.WriteLine ("======================================");
			Console.WriteLine ("Zero-Storage Library Usage Demo");
			Console.WriteLine ("======================================");
			Console.WriteLine ("");
			string consumerKey = System.Environment.GetEnvironmentVariable ("TOOPHER_CONSUMER_KEY");
			string consumerSecret = System.Environment.GetEnvironmentVariable ("TOOPHER_CONSUMER_SECRET");
			if ((consumerKey == null) || (consumerSecret == null)) {
				Console.WriteLine ("Setup Credentials");
				Console.WriteLine ("--------------------------------------");
				Console.WriteLine ("Enter your requester credentials (from https://dev.toopher.com).");
				Console.WriteLine ("Hint: set the TOOPHER_CONSUMER_SECRET and TOOPHER_CONSUMER_SECRET environment variables to avoid this prompt.");
				Console.Write ("Consumer key: ");
				consumerKey = Console.ReadLine ();
				Console.Write ("Consumer secret: ");
				consumerSecret = Console.ReadLine ();
			}
			string baseUrl = System.Environment.GetEnvironmentVariable ("TOOPHER_BASE_URL");

			api = new Toopher.ToopherAPI (consumerKey, consumerSecret, baseUrl);

			Console.Write(String.Format("Enter a username to authenticate with Toopher [{0}]: ", DEFAULT_USERNAME));
			string userName = Console.ReadLine ();
			if (userName.Length == 0) {
				userName = DEFAULT_USERNAME;
			}
			string terminalIdentifier = random.Next().ToString();

			state = STATE.AUTHENTICATE;
			AuthenticationStatus authStatus = null;
			Pairing pairing = null;

			while (true) {
				switch (state) {
					case STATE.AUTHENTICATE: {
						try {
							Console.WriteLine (String.Format ("\n\nAuthenticating user \"{0}\"", userName));
							authStatus = api.AuthenticateByUserName (userName, terminalIdentifier);
							state = STATE.EVALUATE_AUTHENTICATION_STATUS;
						} catch (UserDisabledError e) {
							state = STATE.USER_DISABLED;
						} catch (UserUnknownError e) {
							Console.WriteLine ("That username is not yet paired.");
							state = STATE.PAIR;
						} catch (TerminalUnknownError e) {
							Console.WriteLine ("First attempt to authenticate from an unknown terminal.");
							state = STATE.NAME_TERMINAL;
						} catch (PairingDeactivatedError e) {
							Console.WriteLine("The pairing has been deactivatied.");
							state = STATE.PAIR;
						} catch (RequestError e) {
							Console.WriteLine("Error communicating with Toopher API: " + e.Message);
						}
						break;
					};
					case STATE.POLL_FOR_AUTHENTICATION: {
						Console.WriteLine ("Waiting for Authentication.  Please select an option below:");
						Console.WriteLine ("  (1) Check Authentication Status (default)");
						Console.WriteLine ("  (2) Enter OTP");
						Console.WriteLine ("  (3) Reset user pairing status");
						Console.Write ("Input [1,2,3] : ");
						string response = Console.ReadLine ().Trim ();
						if (response == "2") {
							state = STATE.ENTER_OTP;
						} else if (response == "3") {
							state = STATE.RESET_PAIRING; 
						} else {

							Console.WriteLine ("Checking status of authentication request...");
							try {
								authStatus = api.GetAuthenticationStatus (authStatus.id);
								state = STATE.EVALUATE_AUTHENTICATION_STATUS;
							} catch (RequestError err) {
								Console.WriteLine (String.Format ("Could not check authentication status (reason:{0})", err.Message));
							}
						}
						
						break;
					};
					case STATE.EVALUATE_AUTHENTICATION_STATUS: {
						if (authStatus.pending) {
							Console.WriteLine ("The authentication request has not received a response from the phone yet.");
							state = STATE.POLL_FOR_AUTHENTICATION;
						} else {
							string automation = authStatus.automated ? "automatically " : "";
							string result = authStatus.granted ? "granted" : "denied";
							Console.WriteLine ("The request was " + automation + result + "!");
							Console.WriteLine ("This request " + ((bool)authStatus["totp_valid"] ? "had" : "DID NOT HAVE") + " a valid authenticator OTP.");

							Console.WriteLine ();
							if (yesNoPrompt("Simulate moving to a new terminal for next authentication request? (Ctrl-C to Exit) ", "N")){
								// generate a new random terminalIdentifier
								terminalIdentifier = random.Next ().ToString ();
							}
							state = STATE.AUTHENTICATE;
						}
						break;	
					};
					case STATE.ENTER_OTP: {
						Console.Write ("Please enter the Pairing OTP value generated in the Toopher Mobile App: ");
						string otp = Console.ReadLine ().Trim ();
						authStatus = api.GetAuthenticationStatus (authStatus.id, otp: otp);
						state = STATE.EVALUATE_AUTHENTICATION_STATUS;
						break;
					};
					case STATE.USER_DISABLED: {
						if(yesNoPrompt("Toopher Authentication is disabled for that user.  Do you want to enable Toopher?", "Y")) {
							api.SetToopherEnabledForUser (userName, true);
						}
						state = STATE.AUTHENTICATE;
						break;
					};

					case STATE.PAIR: {
						string pairingPhrase;
						while (true) {
							Console.WriteLine ("Pair user with phone");
							Console.WriteLine ("--------------------------------------");
							Console.WriteLine ("Pairing phrases are generated on the mobile app");
							Console.Write ("Enter pairing phrase: ");
							pairingPhrase = Console.ReadLine ();
							if (pairingPhrase.Length == 0) {
								Console.WriteLine ("Please enter a pairing phrase to continue");
							} else {
								break;
							}
						}

						try {
							pairing = api.Pair (pairingPhrase, userName);
							state = STATE.POLL_FOR_PAIRING;
							break;
						} catch (RequestError err) {
							System.Console.WriteLine (String.Format ("The pairing phrase was not accepted (reason:{0})", err.Message));
						}
						break;
					};
					case STATE.POLL_FOR_PAIRING: {
						pairing = api.GetPairing(pairing.id);
						if (pairing.enabled) {
							Console.WriteLine ("Pairing complete");
							state = STATE.AUTHENTICATE;
						} else {
							Console.WriteLine ("The pairing has not been authorized by the phone yet.");
						}
						break;
					};
					case STATE.RESET_PAIRING: {
						string securityQuestion = string.Empty;
						string securityAnswer = string.Empty;
						if (yesNoPrompt ("Use Security Question/Answer for pairing reset?", "N")) {
							securityQuestion = "What is your favorite color?";
							Console.WriteLine (String.Format ("Question: {0}", securityQuestion));
							Console.Write ("Answer  : ");
							securityAnswer = Console.ReadLine ().Trim ();
						}
						string reset_url = api.GetPairingResetLink (((IDictionary<String, Object>)authStatus["pairing"])["id"].ToString (), securityQuestion, securityAnswer);
						Console.WriteLine (String.Format ("Created a One-Time Pairing Reset link: {0}", reset_url));
						if (yesNoPrompt ("Would you like to open this link in a browser?", "Y")) {
							System.Diagnostics.Process.Start (reset_url);
						}
						Console.WriteLine ("Press [Enter] to continue...");
						Console.ReadLine ();
						state = STATE.AUTHENTICATE;
							
						break;
					};
					case STATE.NAME_TERMINAL: {
						Console.Write (String.Format ("Enter a terminal name for this authentication request [\"{0}\"]: ", DEFAULT_TERMINAL_NAME));
						string terminalName = Console.ReadLine ();
						if (terminalName.Length == 0) {
							terminalName = DEFAULT_TERMINAL_NAME;
						}
						try {
							api.CreateUserTerminal(userName, terminalName, terminalIdentifier);
						} catch(RequestError e) {
							Console.WriteLine (String.Format ("could not create terminal (reason:{0})", e.Message));
						}
						state = STATE.AUTHENTICATE;
						break;
					};
					default: {
						Console.WriteLine (String.Format ("Unknown state {0}", state));
						state = STATE.AUTHENTICATE;
						break;	
					};
				}

			}
			
		}
			
	}
}
