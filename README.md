#ToopherDotNet

[![Build
Status](https://travis-ci.org/toopher/toopher-dotnet.png?branch=master)](https://travis-ci.org/toopher/toopher-dotnet)

#### Introduction
ToopherDotNet is a Toopher API library that simplifies the task of interfacing with the Toopher API from DotNet programs.  It does not depend on any external libraries, and preconfigures the required OAuth and JSON functionality so you can focus on just using the API.

#### Learn the Toopher API
Make sure you visit (http://dev.toopher.com) to get acquainted with the Toopher API fundamentals.  The documentation there will tell you the details about the operations this API wrapper library provides.

#### OAuth Authentication

The first step to accessing the Toopher API is to sign up for an account at the development portal (http://dev.toopher.com) and create a "requester". When that process is complete, your requester is issued OAuth 1.0a credentials in the form of a consumer key and secret. Your key is used to identify your quester when Toopher interacts with your customers, and the secret is used to sign each request so that we know it is generated by you.  This library properly formats each request with your credentials automatically.

#### The Toopher Two-Step
Interacting with the Toopher web service involves two steps: pairing, and authenticating.

##### Pair
Before you can enhance your website's actions with Toopher, your customers will need to pair their phone's Toopher app with your website.  To do this, they generate a unique, nonsensical "pairing phrase" from within the app on their phone.  You will need to prompt them for a pairing phrase as part of the Toopher enrollment process.  Once you have a pairing phrase, just send it to the Toopher API along with your requester credentials and we'll return a pairing ID that you can use whenever you want to authenticate an action for that user.

##### Authenticate
You have complete control over what actions you want to authenticate using Toopher (for example: logging in, changing account information, making a purchase, etc.).  Just send us the user's pairing ID, a name for the terminal they're using, and a description of the action they're trying to perform and we'll make sure they actually want it to happen.

#### Librarified
This library makes it super simple to do the Toopher two-step.  Check it out:

```csharp
using Toopher;

// Create an API object using your credentials
ToopherApi api = new ToopherApi("<your consumer key>", "<your consumer secret>");

// Step 1 - Pair with their phone's Toopher app
Pairing pairing = api.Pair("pairing phrase", "username@yourservice.com");

// Step 2 - Authenticate a log in
AuthenticationRequest auth = api.Authenticate(pairing.id, "my computer");

// Once they've responded you can then check the status
AuthenticationRequest status = api.GetAuthenticationRequest(auth.id);
if (status.pending == false && status.granted == true) {
    // Success!
}
```

#### Handling Errors

If any request runs into an error a `RequestError` will be thrown with more details on what went wrong.

#### Try it out
Check out the `ToopherDotNetDemo` project for an example program that walks you through the whole process!
