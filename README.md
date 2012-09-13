
#ToopherDotNet

#### Introduction
ToopherDotNet is a Toopher API library that simplifies the task of interfacing with the Toopher API from DotNet programs.  It does not depend on any external libraries, and preconfigures the required OAuth and JSON functionality so you can focus on just using the API.

#### Learn the Toopher API
Make sure you visit (http://dev.toopher.com) to get acquainted with the Toopher API fundamentals.  The documentation there will tell you the details about the operations this API wrapper library provides.

#### OAuth Authentication

The first step to accessing the Twitter API is to sign up for an account at the development portal (http://dev.toopher.com) and set up a requester. When that process is complete, your requester is issued a Consumer Key and Consumer Secret. These tokens are responsible for identifying your requester when Toopher interacts with your customers. Once you have these values, you can create a new service and pass them in.

#### The Toopher Two-Step
Interacting with the Toopher web service involves two steps: pairing, and authenticating.

##### Pair
Before you can enhance your website's actions with Toopher, your customers will need to pair their phone's Toopher app with your website.  To do this, they generate a unique, nonsensical "pairing phrase" from within the app on their phone.  You will need to prompt them for a pairing phrase as part of the Toopher enrollment process.  Once you have it, just send to us along with your requester credentials and we'll provide you a pairing ID that you can use whenever you want to authenticate something for that user.

##### Authenticate
Once you have a pairing ID, you can choose what actions you want to authenticate (for example: logging in, changing account information, etc.).  Just tell us what they're trying to do and we'll ask them if they want it to happen.

##### Librarified
This library makes it super simple to do the Toopher two-step.  Check it out:

```csharp
using Toopher;

// Pass your credentials to the service
ToopherApi api = new ToopherApi("consumerKey", "consumerSecret");

// Step 1 - Pair with their phone's Toopher app
PairingStatus pairing = api.Pair("pairing phrase", "username@yourservice.com");

// Step 2 - Authenticate a log in
AuthenticationStatus auth = api.Authenticate(pairing.id, "my computer");

// Once they've responded you can then check the status
AuthenticationStatus status = api.GetAuthenticationStatus(auth.id);
if (auth.pending == false && auth.granted == true) {
    // Success!
}
```

#### Handling Errors

If any request runs into an error a `RequestError` will be thrown with more details on what went wrong.

#### Try it out
Check out the `ToopherDotNetDemo` project for an example program that walks you through the whole process!
